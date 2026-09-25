using System.Collections.ObjectModel;

namespace InvoiceFlow.Domain;

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
        UpdateDetails(invoiceNumber, customerName, issueDate, currencyCode);
    }

    public Guid Id { get; private set; }

    public string InvoiceNumber { get; private set; } = null!;

    public string CustomerName { get; private set; } = null!;

    public DateOnly IssueDate { get; private set; }

    public string CurrencyCode { get; private set; } = null!;

    public IReadOnlyList<InvoiceLineItem> LineItems => _readOnlyLineItems;

    public void UpdateDetails(
        string invoiceNumber,
        string customerName,
        DateOnly issueDate,
        string currencyCode)
    {
        ValidateRequired(invoiceNumber, nameof(invoiceNumber), "Invoice number");
        ValidateRequired(customerName, nameof(customerName), "Customer name");
        var normalizedCurrencyCode = ValidateAndNormalizeCurrencyCode(currencyCode);

        InvoiceNumber = invoiceNumber;
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

    public decimal CalculateSubtotal() =>
        _lineItems.Sum(lineItem => lineItem.CalculateGrossAmount());

    public decimal CalculateTotalDiscount() =>
        _lineItems.Sum(lineItem => lineItem.CalculateDiscountAmount());

    public decimal CalculateGrandTotal() =>
        _lineItems.Sum(lineItem => lineItem.CalculateLineTotal());

    private static void ValidateRequired(string value, string parameterName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{displayName} is required.", parameterName);
        }
    }

    private static string ValidateAndNormalizeCurrencyCode(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) ||
            currencyCode.Length != 3 ||
            !currencyCode.All(char.IsAsciiLetter))
        {
            throw new ArgumentException(
                "Currency code must contain exactly three alphabetic characters.",
                nameof(currencyCode));
        }

        return currencyCode.ToUpperInvariant();
    }
}
