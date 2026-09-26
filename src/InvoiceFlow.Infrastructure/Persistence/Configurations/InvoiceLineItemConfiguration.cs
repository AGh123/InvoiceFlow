using InvoiceFlow.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Persistence.Configurations;

public sealed class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    private const int AmountPrecision = 18;
    private const int DiscountPrecision = 5;

    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("InvoiceLineItems");

        builder.HasKey(lineItem => lineItem.Id);
        builder.Property(lineItem => lineItem.Id)
            .ValueGeneratedNever();

        builder.Property(lineItem => lineItem.InvoiceId)
            .IsRequired();

        builder.Property(lineItem => lineItem.Description)
            .HasMaxLength(InvoiceRules.LineItemDescriptionMaxLength)
            .IsRequired();

        builder.Property(lineItem => lineItem.Quantity)
            .HasPrecision(AmountPrecision, InvoiceRules.QuantityAndUnitPriceScale)
            .IsRequired();

        builder.Property(lineItem => lineItem.UnitPrice)
            .HasPrecision(AmountPrecision, InvoiceRules.QuantityAndUnitPriceScale)
            .IsRequired();

        builder.Property(lineItem => lineItem.DiscountPercent)
            .HasPrecision(DiscountPrecision, InvoiceRules.DiscountPercentScale)
            .IsRequired();

        builder.HasIndex(lineItem => lineItem.InvoiceId);
    }
}
