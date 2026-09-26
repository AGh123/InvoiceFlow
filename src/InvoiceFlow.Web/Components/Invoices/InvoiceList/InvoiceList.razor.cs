using System.Globalization;
using InvoiceFlow.Application.Invoices;
using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Application.Invoices.Requests;
using Microsoft.AspNetCore.Components;

namespace InvoiceFlow.Web.Components.Invoices;

public partial class InvoiceList : IDisposable
{
    private const int PageSize = InvoiceListQuery.DefaultPageSize;
    private InvoiceDeleteDialog? _deleteDialog;
    private CancellationTokenSource? _searchDelay;
    private string _searchTerm = string.Empty;
    private InvoiceSortOption _sortOption = InvoiceSortOption.Newest;
    private int _currentPage = 1;
    private InvoiceListResultDto? _lastResult;
    private (InvoiceListView View, Guid Id)? _openMenuKey;

    private enum InvoiceListView { Desktop, Mobile }

    [Parameter, EditorRequired]
    public InvoiceListResultDto Result { get; set; } = null!;

    [Parameter]
    public EventCallback<InvoiceListQuery> QueryChanged { get; set; }

    [Parameter]
    public EventCallback<InvoiceSummaryDto> InvoiceDeleted { get; set; }

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_lastResult, Result))
        {
            _lastResult = Result;
            _currentPage = Result.Page;
        }
    }

    private async Task SearchChangedAsync(ChangeEventArgs args)
    {
        var value = args.Value?.ToString() ?? string.Empty;
        if (string.Equals(_searchTerm, value, StringComparison.Ordinal)) return;

        _searchTerm = value;
        _currentPage = 1;
        _searchDelay?.Cancel();
        _searchDelay?.Dispose();
        _searchDelay = new CancellationTokenSource();
        var token = _searchDelay.Token;
        try
        {
            await Task.Delay(250, token);
            await InvokeAsync(() => QueryChanged.InvokeAsync(CurrentQuery()));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private async Task SetSortOptionAsync(InvoiceSortOption value)
    {
        if (_sortOption == value) return;
        _sortOption = value;
        _currentPage = 1;
        _searchDelay?.Cancel();
        await QueryChanged.InvokeAsync(CurrentQuery());
    }

    private InvoiceListQuery CurrentQuery() =>
        new(_searchTerm, _sortOption, _currentPage, PageSize);

    private static int GetPageCount(int itemCount) =>
        Math.Max(1, (itemCount + PageSize - 1) / PageSize);

    private static string FormatDate(DateOnly date) =>
        date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    private static string FormatAmount(InvoiceSummaryDto invoice) =>
        $"{invoice.CurrencyCode} {invoice.GrandTotal.ToString("N2", CultureInfo.InvariantCulture)}";

    private static string InvoiceHref(InvoiceSummaryDto invoice) => $"/invoices/{invoice.Id}";

    private void OpenMenu(InvoiceListView view, Guid id) => _openMenuKey = (view, id);

    private void CloseMenu(InvoiceListView view, Guid id)
    {
        if (_openMenuKey == (view, id)) _openMenuKey = null;
    }

    private Task ShowDeleteDialogAsync(InvoiceSummaryDto invoice) =>
        _deleteDialog?.ShowAsync(invoice) ?? Task.CompletedTask;

    private Task HandleInvoiceDeletedAsync(InvoiceSummaryDto invoice) =>
        InvoiceDeleted.InvokeAsync(invoice);

    private async Task PreviousPageAsync()
    {
        if (_currentPage <= 1) return;
        _currentPage--;
        await QueryChanged.InvokeAsync(CurrentQuery());
    }

    private async Task NextPageAsync()
    {
        if (_currentPage >= GetPageCount(Result.FilteredCount)) return;
        _currentPage++;
        await QueryChanged.InvokeAsync(CurrentQuery());
    }

    public void Dispose()
    {
        _searchDelay?.Cancel();
        _searchDelay?.Dispose();
    }
}
