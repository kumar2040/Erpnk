$connString = $env:NK_DB_CONNECTION; if ([string]::IsNullOrWhiteSpace($connString)) { Write-Error "Set the NK_DB_CONNECTION environment variable to the DB connection string before running this script."; exit 1 }
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
