$connString = "server=172.25.50.251;database=NatureKnit;user id=eflow;password=Efl0w@123#;MultipleActiveResultSets=true;TrustServerCertificate=True"
$sqlPath = "c:\Users\Vijay.Anand\Documents\GitHub\Erpnk\database\gauge_migration.sql"

if (-not (Test-Path $sqlPath)) {
    Write-Error "Migration file not found at $sqlPath"
    exit
}

$sqlContent = Get-Content $sqlPath -Raw
# Split by GO on its own line
$batches = [regex]::Split($sqlContent, "(?m)^\s*GO\s*`r?`n?")

$conn = New-Object System.Data.SqlClient.SqlConnection($connString)
$conn.Open()
Write-Host "Connected to SQL Server."

foreach ($batch in $batches) {
    $cleanBatch = $batch.Trim()
    if ($cleanBatch -ne "") {
        try {
            $cmd = $conn.CreateCommand()
            $cmd.CommandText = $cleanBatch
            $res = $cmd.ExecuteNonQuery()
            Write-Host "Batch executed successfully."
        } catch {
            Write-Error "Error executing batch: $_"
            $conn.Close()
            exit
        }
    }
}

$conn.Close()
Write-Host "Migration completed successfully."
