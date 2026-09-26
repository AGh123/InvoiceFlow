namespace InvoiceFlow.Application.Invoices.Dtos;

public sealed record InvoiceListResultDto(
    IReadOnlyList<InvoiceSummaryDto> Invoices,
    int TotalCount,
    int FilteredCount,
    DateOnly? LatestIssueDate,
    int Page);
