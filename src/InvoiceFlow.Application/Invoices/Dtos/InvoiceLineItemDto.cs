namespace InvoiceFlow.Application.Invoices.Dtos;

public sealed record InvoiceLineItemDto(
    Guid Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal LineTotal);
