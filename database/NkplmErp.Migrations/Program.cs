using Microsoft.Data.SqlClient;
using System.Data;

var connectionString = "Server=localhost,1433;Database=NkplmErp;User Id=SA;Password=Password123;MultipleActiveResultSets=True;TrustServerCertificate=True";

using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

Console.WriteLine("--- Updating Stored Procedure ---");
var spSql = @"
CREATE OR ALTER PROCEDURE [dbo].[GetCustomerOrderStatusSummary]
    @Year INT,
    @Type NVARCHAR(50) = NULL,
    @Limit INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (ISNULL(@Limit, 2147483647))
        u.Id AS CustomerId,
        ISNULL(u.FirstName + ' ' + u.LastName, u.UserName) AS CustomerName,
        ISNULL(SUM(CASE WHEN o.Status = 'NotStarted' THEN 1 ELSE 0 END), 0) AS NotStartedOrder,
        ISNULL(SUM(CASE WHEN o.Status = 'Running' THEN 1 ELSE 0 END), 0) AS RunningOrder,
        COUNT(o.Id) AS TotalOrder
    FROM [identity].[Users] u
    LEFT JOIN [dbo].[Orders] o ON u.Id = o.CustomerId 
        AND YEAR(o.OrderDate) = @Year
        AND (@Type IS NULL OR @Type = 'All' OR o.Status = @Type)
    GROUP BY u.Id, u.FirstName, u.LastName, u.UserName
    ORDER BY TotalOrder DESC
END";

using var cmdUpdate = new SqlCommand(spSql, connection);
await cmdUpdate.ExecuteNonQueryAsync();
Console.WriteLine("Procedure GetCustomerOrderStatusSummary updated successfully.");

Console.WriteLine("\n--- Audit Logs (identity.AuditLogs) ---");
try {
    using var cmdLogs = new SqlCommand("SELECT TOP 20 [Type], [TableName], [PrimaryKey], [NewValues], [DateTime] FROM [identity].[AuditLogs] ORDER BY [DateTime] DESC", connection);
    using var readerLogs = await cmdLogs.ExecuteReaderAsync();
    while (await readerLogs.ReadAsync())
    {
        Console.WriteLine($"[{readerLogs["DateTime"]}] {readerLogs["Type"]} | {readerLogs["TableName"]}:{readerLogs["PrimaryKey"]} | {readerLogs["NewValues"]}");
    }
    await readerLogs.CloseAsync();
} catch (Exception ex) {
    Console.WriteLine($"Error reading audit logs: {ex.Message}");
}

Console.WriteLine("\n--- Tables ---");
using var cmdTables = new SqlCommand("SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES", connection);
using var readerTables = await cmdTables.ExecuteReaderAsync();
while (await readerTables.ReadAsync())
{
    Console.WriteLine($"Table: {readerTables["TABLE_SCHEMA"]}.{readerTables["TABLE_NAME"]}");
}
await readerTables.CloseAsync();

Console.WriteLine("\n--- Stored Procedures ---");
using var cmdProc = new SqlCommand("SELECT name FROM sys.procedures WHERE name LIKE '%OrderSummary%'", connection);
using var readerProc = await cmdProc.ExecuteReaderAsync();
while (await readerProc.ReadAsync())
{
    Console.WriteLine($"Procedure: {readerProc["name"]}");
}
await readerProc.CloseAsync();

Console.WriteLine("\n--- Procedure Output (2026, All, Limit 5) ---");
using var cmdExec = new SqlCommand("EXEC dbo.GetCustomerOrderStatusSummary @Year=2026, @Type='All', @Limit=5", connection);
using var readerExec = await cmdExec.ExecuteReaderAsync();
while (await readerExec.ReadAsync())
{
    Console.WriteLine($"Customer: {readerExec["CustomerName"]}, New: {readerExec["NotStartedOrder"]}, Running: {readerExec["RunningOrder"]}, Total: {readerExec["TotalOrder"]}");
}
await readerExec.CloseAsync();
