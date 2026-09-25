namespace InvoiceFlow.Application.Invoices.Requests;

public sealed record CreateInvoiceRequest(
    string InvoiceNumber,
    string CustomerName,
    DateOnly IssueDate,
    string CurrencyCode,
    IReadOnlyList<InvoiceLineItemInput> LineItems);
