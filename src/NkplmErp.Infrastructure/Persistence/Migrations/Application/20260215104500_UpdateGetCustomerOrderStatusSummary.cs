using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NkplmErp.Infrastructure.Persistence.Migrations.Application
{
    public partial class UpdateGetCustomerOrderStatusSummary : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to 2 parameters if necessary, but keep it at 3 for consistency with current design
        }
    }
}
