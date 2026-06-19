$connString = $env:NK_DB_CONNECTION; if ([string]::IsNullOrWhiteSpace($connString)) { Write-Error "Set the NK_DB_CONNECTION environment variable to the DB connection string before running this script."; exit 1 }
$conn = New-Object System.Data.SqlClient.SqlConnection($connString)
$conn.Open()

# 1. Get Admin UserId
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id FROM [identity].[Users] WHERE Email = 'admin@nkplm.erp'"
$userId = $cmd.ExecuteScalar()

# 2. Execute sp_GetUserPermissions
$cmd = $conn.CreateCommand()
$cmd.CommandText = "EXEC sp_GetUserPermissions @userId = '$userId'"
$reader = $cmd.ExecuteReader()
Write-Host "=== permissions returned by updated sp_GetUserPermissions ==="
while ($reader.Read()) {
    Write-Host "PageKey: $($reader['PageKey']) | CanView: $($reader['CanView']) | CanEdit: $($reader['CanEdit']) | CanDelete: $($reader['CanDelete'])"
}
$reader.Close()

$conn.Close()
