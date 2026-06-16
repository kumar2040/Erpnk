$connString = "server=172.25.50.251;database=NatureKnit;user id=eflow;password=Efl0w@123#;MultipleActiveResultSets=true;TrustServerCertificate=True"
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
