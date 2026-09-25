namespace InvoiceFlow.Application.Invoices.Dtos;

public sealed record InvoiceDetailsDto(
    Guid Id,
    string InvoiceNumber,
    string CustomerName,
    DateOnly IssueDate,
    string CurrencyCode,
    IReadOnlyList<InvoiceLineItemDto> LineItems,
    decimal Subtotal,
    decimal TotalDiscount,
    decimal GrandTotal);
