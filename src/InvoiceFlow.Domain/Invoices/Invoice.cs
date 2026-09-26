using System.Collections.ObjectModel;

namespace InvoiceFlow.Domain.Invoices;

public class Invoice
{
    private readonly List<InvoiceLineItem> _lineItems = [];
    private readonly ReadOnlyCollection<InvoiceLineItem> _readOnlyLineItems;

    private Invoice()
    {
        _readOnlyLineItems = _lineItems.AsReadOnly();
    }

    public Invoice(
        Guid id,
        string invoiceNumber,
        string customerName,
        DateOnly issueDate,
        string currencyCode)
        : this()
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Invoice ID cannot be empty.", nameof(id));
        }

        Id = id;
        ValidateRequired(invoiceNumber, nameof(invoiceNumber), "Invoice number", InvoiceRules.InvoiceNumberMaxLength);
        InvoiceNumber = invoiceNumber;
        UpdateDetails(customerName, issueDate, currencyCode);
    }

    public Guid Id { get; private set; }

    public string InvoiceNumber { get; private set; } = null!;

    public string CustomerName { get; private set; } = null!;

    public DateOnly IssueDate { get; private set; }

    public string CurrencyCode { get; private set; } = null!;

    public IReadOnlyList<InvoiceLineItem> LineItems => _readOnlyLineItems;

    public void UpdateDetails(
        string customerName,
        DateOnly issueDate,
        string currencyCode)
    {
        ValidateRequired(customerName, nameof(customerName), "Customer name", InvoiceRules.CustomerNameMaxLength);
        var normalizedCurrencyCode = ValidateAndNormalizeCurrencyCode(currencyCode);

        CustomerName = customerName;
        IssueDate = issueDate;
        CurrencyCode = normalizedCurrencyCode;
    }

    public InvoiceLineItem AddLineItem(
        Guid id,
        string description,
        decimal quantity,
        decimal unitPrice,
        decimal discountPercent)
    {
        if (_lineItems.Any(lineItem => lineItem.Id == id))
        {
            throw new ArgumentException(
                "A line item with the same ID already exists on this invoice.",
                nameof(id));
        }

        var lineItem = new InvoiceLineItem(
            id,
            Id,
            description,
            quantity,
            unitPrice,
            discountPercent);

        _lineItems.Add(lineItem);

        return lineItem;
    }

    public bool RemoveLineItem(Guid lineItemId)
    {
        var lineItem = _lineItems.Find(item => item.Id == lineItemId);

        return lineItem is not null && _lineItems.Remove(lineItem);
    }

    public (decimal Subtotal, decimal TotalDiscount, decimal GrandTotal) CalculateTotals()
    {
        try
        {
            var subtotal = 0m;
            var totalDiscount = 0m;
            var grandTotal = 0m;

            foreach (var lineItem in _lineItems)
            {
                subtotal += lineItem.CalculateGrossAmount();
                totalDiscount += lineItem.CalculateDiscountAmount();
                grandTotal += lineItem.CalculateLineTotal();
            }

            return (subtotal, totalDiscount, grandTotal);
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException(
                "Invoice totals exceed the representable decimal range.",
                nameof(LineItems), exception);
        }
    }

    public decimal CalculateSubtotal() => CalculateTotals().Subtotal;

    public decimal CalculateTotalDiscount() => CalculateTotals().TotalDiscount;

    public decimal CalculateGrandTotal() => CalculateTotals().GrandTotal;

    private static void ValidateRequired(string value, string parameterName, string displayName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{displayName} is required.", parameterName);
        }

        if (value.Length > maximumLength)
        {
            throw new ArgumentException($"{displayName} must be {maximumLength} characters or fewer.", parameterName);
        }
    }

    private static string ValidateAndNormalizeCurrencyCode(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) ||
            currencyCode.Length != InvoiceRules.CurrencyCodeLength ||
            !currencyCode.All(char.IsAsciiLetter))
        {
            throw new ArgumentException(
                "Currency code must contain exactly three alphabetic characters.",
                nameof(currencyCode));
        }

        return currencyCode.ToUpperInvariant();
    }
}
