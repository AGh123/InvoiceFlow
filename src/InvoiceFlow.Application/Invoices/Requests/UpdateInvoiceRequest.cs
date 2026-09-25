namespace InvoiceFlow.Application.Invoices.Requests;

public sealed record UpdateInvoiceRequest(
    string InvoiceNumber,
    string CustomerName,
    DateOnly IssueDate,
    string CurrencyCode,
    IReadOnlyList<InvoiceLineItemInput> LineItems);
