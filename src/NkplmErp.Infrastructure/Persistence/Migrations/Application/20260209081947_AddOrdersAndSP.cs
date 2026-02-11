using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NkplmErp.Infrastructure.Persistence.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddOrdersAndSP : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.Sql(@"
                CREATE OR ALTER PROCEDURE [dbo].[GetCustomerOrderStatusSummary]
                    @Year INT,
                    @Type NVARCHAR(50) = NULL
                AS
                BEGIN
                    SET NOCOUNT ON;

                    SELECT 
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
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS [dbo].[GetCustomerOrderStatusSummary]");
        }
    }
}
