namespace InvoiceFlow.Application.Invoices.Requests;

public sealed record InvoiceListQuery(string SearchTerm, InvoiceSortOption Sort, int Page, int PageSize)
{
    public const int DefaultPageSize = 10;
}
