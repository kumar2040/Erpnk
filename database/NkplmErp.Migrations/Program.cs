using Microsoft.Data.SqlClient;
using System.Data;

var connectionString = "Server=localhost,1433;Database=NkplmErp;User Id=SA;Password=Password123;MultipleActiveResultSets=True;TrustServerCertificate=True";

using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

Console.WriteLine("--- Stored Procedures ---");
using var cmdProc = new SqlCommand("SELECT name FROM sys.procedures WHERE name LIKE '%OrderSummary%'", connection);
using var readerProc = await cmdProc.ExecuteReaderAsync();
while (await readerProc.ReadAsync())
{
    Console.WriteLine($"Procedure: {readerProc["name"]}");
}
await readerProc.CloseAsync();

Console.WriteLine("\n--- Procedure Output (2026, All) ---");
using var cmdExec = new SqlCommand("EXEC dbo.GetCustomerOrderStatusSummary @Year=2026, @Type='All'", connection);
using var readerExec = await cmdExec.ExecuteReaderAsync();
while (await readerExec.ReadAsync())
{
    Console.WriteLine($"Customer: {readerExec["CustomerName"]}, New: {readerExec["NotStartedOrder"]}, Running: {readerExec["RunningOrder"]}, Total: {readerExec["TotalOrder"]}");
}
await readerExec.CloseAsync();
