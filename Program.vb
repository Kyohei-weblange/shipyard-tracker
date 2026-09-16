Imports Microsoft.AspNetCore.Builder
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Data.Sqlite
Imports Microsoft.AspNetCore.Http

' レスポンス用のデータ構造をクラスとして定義する
Public Class ApiResponse
    Public Property Message As String
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

        app.Run()
    End Sub
End Module
