using InvoiceFlow.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Persistence.Configurations;

public sealed class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("InvoiceLineItems");

        builder.HasKey(lineItem => lineItem.Id);
        builder.Property(lineItem => lineItem.Id)
            .ValueGeneratedNever();

        builder.Property(lineItem => lineItem.InvoiceId)
            .IsRequired();

        builder.Property(lineItem => lineItem.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(lineItem => lineItem.Quantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(lineItem => lineItem.UnitPrice)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(lineItem => lineItem.DiscountPercent)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.HasIndex(lineItem => lineItem.InvoiceId);
    }
}
