namespace InvoiceFlow.Domain.Invoices;

public static class InvoiceRules
{
    public const int InvoiceNumberMaxLength = 50;
    public const int CustomerNameMaxLength = 200;
    public const int CurrencyCodeLength = 3;
    public const int LineItemDescriptionMaxLength = 500;
    public const int QuantityAndUnitPriceScale = 4;
    public const int DiscountPercentScale = 2;
    public const decimal MaximumQuantity = 99999999999999.9999m;
    public const decimal MaximumUnitPrice = 99999999999999.9999m;
    public const decimal MaximumDiscountPercent = 100m;
}
