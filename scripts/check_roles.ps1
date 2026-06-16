$connString = "server=172.25.50.251;database=NatureKnit;user id=eflow;password=Efl0w@123#;MultipleActiveResultSets=true;TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($connString)
$conn.Open()

$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id, Name, NormalizedName FROM [identity].[Roles] ORDER BY Name"
$reader = $cmd.ExecuteReader()
Write-Host "=== identity.Roles ==="
while ($reader.Read()) {
    $id   = $reader.GetValue(0)
    $name = $reader.GetValue(1)
    $norm = $reader.GetValue(2)
    Write-Host ("Id=" + $id + " | Name=" + $name + " | NormalizedName=" + $norm)
}
$reader.Close()
$conn.Close()
Write-Host "Done."
