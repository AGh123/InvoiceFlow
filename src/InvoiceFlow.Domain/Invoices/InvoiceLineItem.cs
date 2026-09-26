namespace InvoiceFlow.Domain.Invoices;

public class InvoiceLineItem
{
    private InvoiceLineItem()
    {
    }

    public InvoiceLineItem(
        Guid id,
        Guid invoiceId,
        string description,
        decimal quantity,
        decimal unitPrice,
        decimal discountPercent)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Line item ID cannot be empty.", nameof(id));
        }

        if (invoiceId == Guid.Empty)
        {
            throw new ArgumentException("Invoice ID cannot be empty.", nameof(invoiceId));
        }

        Id = id;
        InvoiceId = invoiceId;
        UpdateDetails(description, quantity, unitPrice, discountPercent);
    }

    public Guid Id { get; private set; }

    public Guid InvoiceId { get; private set; }

    public string Description { get; private set; } = null!;

    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal DiscountPercent { get; private set; }

    public void UpdateDetails(
        string description,
        decimal quantity,
        decimal unitPrice,
        decimal discountPercent)
    {
        ValidateDetails(description, quantity, unitPrice, discountPercent);

        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountPercent = discountPercent;
    }

    public decimal CalculateGrossAmount() => Quantity * UnitPrice;

    public decimal CalculateDiscountAmount() =>
        CalculateGrossAmount() / 100m * DiscountPercent;

    public decimal CalculateLineTotal() =>
        CalculateGrossAmount() - CalculateDiscountAmount();

    private static void ValidateDetails(
        string description,
        decimal quantity,
        decimal unitPrice,
        decimal discountPercent)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required.", nameof(description));
        }

        if (description.Length > InvoiceRules.LineItemDescriptionMaxLength)
        {
            throw new ArgumentException(
                $"Description must be {InvoiceRules.LineItemDescriptionMaxLength} characters or fewer.",
                nameof(description));
        }

        if (quantity <= 0 || quantity > InvoiceRules.MaximumQuantity ||
            decimal.Round(quantity, InvoiceRules.QuantityAndUnitPriceScale) != quantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                $"Quantity must be greater than zero, at most {InvoiceRules.MaximumQuantity}, and have at most {InvoiceRules.QuantityAndUnitPriceScale} decimal places.");
        }

        if (unitPrice < 0 || unitPrice > InvoiceRules.MaximumUnitPrice ||
            decimal.Round(unitPrice, InvoiceRules.QuantityAndUnitPriceScale) != unitPrice)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitPrice),
                unitPrice,
                $"Unit price must be between zero and {InvoiceRules.MaximumUnitPrice} with at most {InvoiceRules.QuantityAndUnitPriceScale} decimal places.");
        }

        if (discountPercent is < 0 or > InvoiceRules.MaximumDiscountPercent ||
            decimal.Round(discountPercent, InvoiceRules.DiscountPercentScale) != discountPercent)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discountPercent),
                discountPercent,
                $"Discount percent must be between 0 and 100 inclusive with at most {InvoiceRules.DiscountPercentScale} decimal places.");
        }
    }
}
