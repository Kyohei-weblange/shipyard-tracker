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

Public Class CreateBlockRequest
    Public Property BlockId As Integer
    Public Property BlockCode As String
    Public Property ShipId As String
    Public Property ProcessName As String
    Public Property Status As String
    Public Property WeightTons As Integer
    Public Property UpdatedAt As DateTime
End Class

Public Class UpdateStatusRequest
    Public Property Id As Integer
    Public Property Status As String
    Public Property CurrentUpdatedAt As String
End Class

Public Class BatchUpdateStatusRequest
    Public Property BlockIds As List(Of Integer)
    Public Property Status As String
End Class
