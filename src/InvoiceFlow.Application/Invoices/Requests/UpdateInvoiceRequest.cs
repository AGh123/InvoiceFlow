namespace InvoiceFlow.Application.Invoices.Requests;

public sealed record UpdateInvoiceRequest(
    string CustomerName,
    DateOnly IssueDate,
    string CurrencyCode,
    IReadOnlyList<InvoiceLineItemInput> LineItems);
