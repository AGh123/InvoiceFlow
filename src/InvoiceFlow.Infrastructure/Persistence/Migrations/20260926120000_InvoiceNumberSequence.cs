using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InvoiceFlow.Infrastructure.Persistence.Migrations;

[DbContext(typeof(InvoiceFlowDbContext))]
[Migration("20260926120000_InvoiceNumberSequence")]
public sealed class InvoiceNumberSequence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            "CREATE TABLE InvoiceNumberSequence (Id INTEGER PRIMARY KEY AUTOINCREMENT);");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE InvoiceNumberSequence;");
}
