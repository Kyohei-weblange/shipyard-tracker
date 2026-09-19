Imports Microsoft.AspNetCore.Builder
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Data.Sqlite
Imports Microsoft.AspNetCore.Http

' レスポンス用のデータ構造をクラスとして定義する
Public Class ApiResponse
    Public Property Message As String
End Class

Public Class BlocksItem
    Public Property Id As Integer
    Public Property BlockCode As String
    Public Property ShipId As String
    Public Property ProcessName As String
    Public Property Status As String
    Public Property WeightTons As Integer
    Public Property UpdatedAt As DateTime
End Class

Module Program
    Sub Main(args As String())
        Dim builder = WebApplication.CreateBuilder(args)
        Dim app = builder.Build()

        app.UseDefaultFiles()
        app.UseStaticFiles()

        Dim connectionString As String = "Data Source = shipyard.db"

        ' エンドポイント
        app.MapPost("/api/init-db", Function() As IResult
          Try
          ' DB初期化
          Using connection As New SqliteConnection(connectionString)

            connection.Open()
            Dim command = connection.CreateCommand()
            command.CommandText = "
              CREATE TABLE IF NOT EXISTS ShipBlocks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                BlockCode TEXT NOT NULL UNIQUE,
                ShipId TEXT NOT NULL,
                ProcessName TEXT NOT NULL,
                Status TEXT NOT NULL,
                WeightTons REAL DEFAULT 0.0,
                UpdatedAt TEXT DEFAULT (datetime('now', 'localtime'))
              )
            "
            command.ExecuteNonQuery()
          End Using
            Dim response As New ApiResponse With {.Message = "初期化成功！"}
              Return Results.Json(response)
            Catch ex As Exception
              Dim response As New ApiResponse With {.Message = "初期化失敗: " & ex.Message}
              Return Results.Json(response)
          End Try
        End Function)

        app.MapGet("/api/blocks", Function(shipId As String) As IResult
          Dim blocks As New List(Of BlocksItem)()
          Using connection As New SqliteConnection(connectionString)
            connection.Open()
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

        app.Run()
    End Sub
End Module
