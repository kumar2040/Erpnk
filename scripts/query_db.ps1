$connString = $env:NK_DB_CONNECTION; if ([string]::IsNullOrWhiteSpace($connString)) { Write-Error "Set the NK_DB_CONNECTION environment variable to the DB connection string before running this script."; exit 1 }
$conn = New-Object System.Data.SqlClient.SqlConnection($connString)
$conn.Open()

$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id, Name, Description FROM [identity].[Permissions] ORDER BY Name"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "Id: $($reader['Id']) | Name: $($reader['Name']) | Description: $($reader['Description'])"
}
$reader.Close()

$conn.Close()
