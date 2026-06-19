$connString = $env:NK_DB_CONNECTION; if ([string]::IsNullOrWhiteSpace($connString)) { Write-Error "Set the NK_DB_CONNECTION environment variable to the DB connection string before running this script."; exit 1 }
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
