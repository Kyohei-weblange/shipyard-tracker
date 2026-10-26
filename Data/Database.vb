Imports Microsoft.Data.Sqlite

Public Class Database
  Public Shared ReadOnly connectionString As String = "Data Source = shipyard.db"

  Public Shared Function GetConnection() As SqliteConnection
    Dim connection = New SqliteConnection(connectionString)
    connection.Open()
    Return connection
  End Function

  Public Shared Sub InitializeDatabase()
    ' DB初期化
      Using connection As SqliteConnection = GetConnection()

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
          );
          CREATE TABLE IF NOT EXISTS AuditLogs (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            BlockId INTEGER NOT NULL,
            ActionType TEXT NOT NULL,
            Details TEXT,
            CreatedAt TEXT DEFAULT (datetime('now', 'localtime'))
          );
          CREATE INDEX IF NOT EXISTS idex_blocks_status ON ShipBlocks(status);
        "
        command.ExecuteNonQuery()
      End Using
  End Sub
End Class
