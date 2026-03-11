using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NkplmErp.Infrastructure.Persistence.Migrations.Application
{
    public partial class AddGetBuyerOrderYears : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE OR ALTER PROCEDURE [dbo].[GetBuyerOrderYears]
                    @CustomerId INT = NULL
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SELECT DISTINCT YEAR(OrderDate) AS Year
                    FROM [dbo].[Orders]
                    WHERE (@CustomerId IS NULL OR CustomerId = @CustomerId)
                    ORDER BY Year DESC
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS [dbo].[GetBuyerOrderYears]");
        }
    }
}
