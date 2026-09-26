using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InvoiceFlow.Infrastructure.Persistence.Migrations;

[DbContext(typeof(InvoiceFlowDbContext))]
[Migration("20260926130000_CompactInvoiceNumberSequence")]
public sealed class CompactInvoiceNumberSequence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE InvoiceNumberSequence RENAME TO InvoiceNumberSequenceHistory;");
        migrationBuilder.Sql("CREATE TABLE InvoiceNumberSequence (Id INTEGER PRIMARY KEY CHECK (Id = 1), Value INTEGER NOT NULL);");
        migrationBuilder.Sql("INSERT INTO InvoiceNumberSequence (Id, Value) SELECT 1, COALESCE(MAX(Id), 0) FROM InvoiceNumberSequenceHistory;");
        migrationBuilder.Sql("DROP TABLE InvoiceNumberSequenceHistory;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE InvoiceNumberSequence RENAME TO InvoiceNumberSequenceCurrent;");
        migrationBuilder.Sql("CREATE TABLE InvoiceNumberSequence (Id INTEGER PRIMARY KEY AUTOINCREMENT);");
        migrationBuilder.Sql("INSERT INTO InvoiceNumberSequence (Id) SELECT Value FROM InvoiceNumberSequenceCurrent WHERE Value > 0;");
        migrationBuilder.Sql("DROP TABLE InvoiceNumberSequenceCurrent;");
    }
}
