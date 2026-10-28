Imports Microsoft.AspNetCore.Builder
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Data.Sqlite
Imports Microsoft.AspNetCore.Http

Module Program

    Sub Main(args As String())
        Dim builder = WebApplication.CreateBuilder(args)
        Dim app = builder.Build()
        Dim connectionString As String = "Data Source = shipyard.db"

        app.UseDefaultFiles()
        app.UseStaticFiles()

        ' エンドポイント
        app.MapPost("/api/init-db", Function() As IResult
          Try
            Database.InitializeDatabase()
            Dim response As New ApiResponse With {.Message = "初期化成功！"}
              Return Results.Json(response)
            Catch ex As Exception
              Dim response As New ApiResponse With {.Message = "初期化失敗: " & ex.Message}
              Return Results.Json(response)
          End Try
        End Function)

        app.MapGet("/api/blocks", Function(shipId As String) As IResult
          Dim blocks As New List(Of BlocksItem)()
          Using connection As SqliteConnection = GetConnection()
            Dim command = connection.CreateCommand()
            Dim sql As String = "
              SELECT
                Id,
                BlockCode,
                ShipId,
                ProcessName,
                Status,
                WeightTons,
                UpdatedAt
              FROM
                ShipBlocks
            "

            If Not String.IsNullOrEmpty(shipId) Then
              sql &= " WHERE ShipId = @ShipId"
              command.Parameters.AddWithValue("@ShipId", shipId)
            End If

            command.CommandText = sql
            Using reader = command.ExecuteReader()
              While reader.Read()
                blocks.Add(New BlocksItem With {
                  .Id = reader.GetInt32(0),
                  .BlockCode = reader.GetString(1),
                  .ShipId = reader.GetString(2),
                  .ProcessName = reader.GetString(3),
                  .Status = reader.GetString(4),
                  .WeightTons = reader.GetInt32(5),
                  .UpdatedAt = reader.GetDateTime(6)
                })
              End While
            End Using
          End Using
          Return Results.Json(blocks)
        End Function)

        app.MapPost("/api/blocks/", Function(req As CreateBlockRequest) As IResult
          Using connection As SqliteConnection = GetConnection()
            Using insertCmd = connection.CreateCommand()
              insertCmd.CommandText = "
                INSERT INTO ShipBlocks (BlockCode, ShipId, ProcessName, Status, WeightTons, UpdatedAt)
                VALUES (@blockCode, @shipId, @processName, @status, @weightTons, @updatedAt)
              "
              insertCmd.Parameters.AddWithValue("@blockCode", req.BlockCode)
              insertCmd.Parameters.AddWithValue("@shipId", req.ShipId)
              insertCmd.Parameters.AddWithValue("@processName", req.ProcessName)
              insertCmd.Parameters.AddWithValue("@status", "未着手")
              insertCmd.Parameters.AddWithValue("@weightTons", req.WeightTons)
              insertCmd.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
              insertCmd.ExecuteNonQuery()
              Task.Run(Sub() WriteAuditLog(0, "ブロック追加", $"新規ブロック登録: {req.BlockCode}"))
              Return Results.Ok("登録が完了しました。")
            End Using
          End Using
        End Function)

        app.MapDelete("/api/blocks/{id}", Function(id As Integer) As IResult
          Using connection As SqliteConnection = GetConnection()
            Dim command = connection.CreateCommand()
            command.CommandText = "DELETE FROM ShipBlocks WHERE Id = @id"
            command.Parameters.AddWithValue("@id", id)
            Dim rowAffected As Integer = Command.ExecuteNonQuery()
            If rowAffected > 0 Then
              Task.Run(Sub() WriteAuditLog(0, "ブロック削除", $"削除ブロック：{id}"))
              Return Results.Ok("削除されました。")
            Else
              Return Results.NotFound($"ID: {id}は見つかりませんでした。")
            End If
          End Using
        End Function)

        app.MapPut("/api/blocks/status", Function(req As UpdateStatusRequest) As IResult
          Using connection As SqliteConnection = GetConnection()
            If Not New String(){"未着手", "加工中", "組立中", "塗装中", "完成"}.Contains(req.Status) Then
              Return Results.BadRequest("不正なステータスです。")
            End If
            If req.CurrentUpdatedAt Is Nothing orElse req.CurrentUpdatedAt = "" Then
              Return Results.BadRequest("現在の更新日時が指定されていません。")
            End If
            Dim currentDbUpdatedAtStr As String = Nothing

            Using selectCmd = connection.CreateCommand()
              selectCmd.CommandText = "
                SELECT
                  UpdatedAt
                FROM
                  ShipBlocks
                WHERE
                  Id = @id
              "
              selectCmd.parameters.AddWithValue("@id", req.Id)
              Dim result = selectCmd.ExecuteScalar()
              If result Is Nothing Then
                Return Results.NotFound("対象のデータが存在しません。")
              End If
              currentDbUpdatedAtStr = result.ToString()
            End Using
            Dim str As DateTime = DateTime.Parse(currentDbUpdatedAtStr)
            Dim at As DateTime = DateTime.Parse(req.CurrentUpdatedAt)
            If str <> at Then
              Return Results.Conflict("他のユーザーによってデータが更新されました。")
            End If

            Using updateCmd = connection.CreateCommand()
              updateCmd.CommandText = "
                UPDATE ShipBlocks SET
                  Status = @status,
                  UpdatedAt = @updatedAt
                WHERE
                  Id = @id
              "
              updateCmd.Parameters.AddWithValue("@id", req.Id)
              updateCmd.Parameters.AddWithValue("@status", req.Status)
              updateCmd.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
              Dim rows = updateCmd.ExecuteNonQuery()
              If rows = 0 Then
                Return Results.Problem("更新に失敗しました。")
              End If
            End Using
            Task.Run(Sub() WriteAuditLog(req.Id, "ステータス更新", $"新しいステータス: {req.Status}"))
            Return Results.Ok("更新が完了しました。")
          End Using
        End Function)

        app.MapPut("/api/blocks/batch-status", Function(req As BatchUpdateStatusRequest) As IResult
          Using connection As SqliteConnection = GetConnection()
            If req.BlockIds Is Nothing OrElse req.BlockIds.Count = 0 Then
              Return Results.BadRequest("更新対象のIDが指定されていません。")
            End If
            If Not New String(){"未着手", "加工中", "組立中", "塗装中", "完成"}.Contains(req.Status) Then
              Return Results.BadRequest("不正なステータスです。")
            End If
            Dim transaction = connection.BeginTransaction()
            Dim updatedCount As Integer = 0
            Try
              For Each blockId In req.BlockIds
                Dim command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText = "
                  UPDATE ShipBlocks SET
                    Status = @status,
                    UpdatedAt = @updatedAt
                  WHERE
                    Id = @id
                "
                command.Parameters.AddWithValue("@status", req.Status)
                command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                command.Parameters.AddWithValue("@id", blockId)
                updatedCount += command.ExecuteNonQuery()
              Next
                transaction.Commit()
                Return Results.Ok($"{updatedCount}件のブロックステータスを一括更新しました。")
              Catch
                transaction.Rollback()
                Throw
            End Try
          End Using
        End Function)

        ' ブロック進捗集計・ボトルネック分析
        app.MapGet ("/api/blocks/summary", Function() As IResult
          Using connection As SqliteConnection = GetConnection()
            Dim totalBlocks As Integer = 0
            Dim totalWeightTons As Double = 0.0
            Dim statusSummary As New Dictionary(Of String, Integer) From {
              {"未着手", 0},
              {"加工中", 0},
              {"組立中", 0},
              {"塗装中", 0},
              {"完成", 0}
            }
            Dim bottleneckProcess As String = "滞留なし"

            ' 総ブロック数と総重量を取得
            Dim command = connection.CreateCommand()
            command.CommandText = "SELECT COUNT(*), SUM(WeightTons) FROM ShipBlocks"
            Using reader = command.ExecuteReader()
              If reader.Read() Then
                totalBlocks = reader.GetInt32(0)
                totalWeightTons = If(reader.IsDBNull(1), 0.0, reader.GetDouble(1))
              End If
            End Using

            ' ステータスごとのブロック数を取得
            command.CommandText = "SELECT Status, COUNT(*) FROM ShipBlocks GROUP BY Status"
            Using reader = command.ExecuteReader()
              While reader.Read()
                Dim status As String = reader.GetString(0)
                Dim count As Integer = reader.GetInt32(1)
                If statusSummary.ContainsKey(status) Then
                  statusSummary(status) = count
                End If
              End While
            End Using

            ' ボトルネックとなる工程を特定（最も多いステータスの工程をボトルネックとする）
            command.CommandText = "SELECT ProcessName, COUNT(*) FROM ShipBlocks WHERE Status <> '完成' GROUP BY ProcessName ORDER BY COUNT(*) DESC LIMIT 1"
            Using reader = command.ExecuteReader()
              If reader.Read() Then
                bottleneckProcess = reader.GetString(0)
              End If
            End Using
            Return Results.Ok(New With { Key .totalBlocks = totalBlocks, Key .totalWeightTons = totalWeightTons, Key .statusSummary = statusSummary, Key .bottleneckProcess = bottleneckProcess})
          End Using
        End Function)

        ' リードタイム分析
        app.MapGet("/api/blocks/lead-time", Async Function() As Task(Of IResult)
          Dim blockList As New List(Of BlockRequest)
          Using connection As New SqliteConnection(connectionString)
            Await connection.OpenAsync()
            Dim command = connection.CreateCommand()
            command.CommandText = "
              SELECT
                Id As BlockId,
                BlockCode,
                Status,
                ROUND(julianday('now') - julianday(UpdatedAt), 1) As DaysInStatus
              FROM
                ShipBlocks
            "
            Using reader = Await command.ExecuteReaderAsync()
              While Await reader.ReadAsync()
                blockList.Add(New BlockRequest With {
                  .BlockId = reader.GetInt32(0),
                  .BlockCode = reader.GetString(1),
                  .Status = reader.GetString(2),
                  .DaysInStatus = reader.GetDouble(3)
                })
              End While
            End Using
          End Using
          Return Results.Ok(blockList)
        End Function)

        app.Run()
    End Sub

    Function GetConnection() As SqliteConnection
      Dim conn As New SqliteConnection(Database.connectionString)
      conn.Open()
      Return conn
    End Function

    ' ログ書き込み用のSub関数(非同期)
    Private Sub WriteAuditLog(blockId As Integer, actionType As String, details As String)
        Using conn As SqliteConnection = Database.GetConnection()
            Dim command = conn.CreateCommand()
            command.CommandText = "
              INSERT INTO AuditLogs (BlockId, ActionType, Details) VALUES (@blockId, @actionType, @details)
            "
            command.Parameters.AddWithValue("@blockId", blockId)
            command.Parameters.AddWithValue("@actionType", actionType)
            command.Parameters.AddWithValue("@details", details)
            command.ExecuteNonQuery()
        End Using
    End Sub

End Module
