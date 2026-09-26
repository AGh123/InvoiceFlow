using InvoiceFlow.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Persistence.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.HasKey(invoice => invoice.Id);
        builder.Property(invoice => invoice.Id)
            .ValueGeneratedNever();

        builder.Property(invoice => invoice.InvoiceNumber)
            .HasMaxLength(InvoiceRules.InvoiceNumberMaxLength)
            .IsRequired();

        builder.HasIndex(invoice => invoice.InvoiceNumber)
            .IsUnique();

        builder.Property(invoice => invoice.CustomerName)
            .HasMaxLength(InvoiceRules.CustomerNameMaxLength)
            .IsRequired();

        builder.Property(invoice => invoice.IssueDate)
            .IsRequired();

        builder.Property(invoice => invoice.CurrencyCode)
            .HasMaxLength(InvoiceRules.CurrencyCodeLength)
            .IsFixedLength()
            .IsRequired();

        builder.HasMany(invoice => invoice.LineItems)
            .WithOne()
            .HasForeignKey(lineItem => lineItem.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.Navigation(invoice => invoice.LineItems)
            .HasField("_lineItems")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
