namespace InvoiceFlow.Application.Invoices.Dtos;

public sealed record InvoiceSummaryDto(
    Guid Id,
    string InvoiceNumber,
    string CustomerName,
    DateOnly IssueDate,
    string CurrencyCode,
    decimal GrandTotal);
