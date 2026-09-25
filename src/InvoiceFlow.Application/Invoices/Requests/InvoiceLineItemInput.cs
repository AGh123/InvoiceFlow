namespace InvoiceFlow.Application.Invoices.Requests;

public sealed record InvoiceLineItemInput(
    Guid? Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent);
