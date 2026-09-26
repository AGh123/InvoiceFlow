using InvoiceFlow.Application.Invoices;
using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Application.Invoices.Requests;

namespace InvoiceFlow.Web.Components.Pages.Invoices;

public partial class InvoiceListPage
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private InvoiceListResultDto? _result;
    private InvoiceListQuery _query = new(string.Empty, InvoiceSortOption.Newest, 1,
        InvoiceListQuery.DefaultPageSize);
    private int _queryVersion;
    private bool _isLoading = true;
    private bool _loadError;
    private string? _successMessage;
    private long _successMessageVersion;

    private string PageClass =>
        $"invoice-page{(!_isLoading && !_loadError && _result?.TotalCount == 0 ? " invoice-page--empty" : string.Empty)}";

    protected override async Task OnInitializedAsync()
    {
        _successMessage = NotificationState.Consume();
        if (_successMessage is not null) _successMessageVersion++;
        await LoadInvoicesAsync(_query);
    }

    private Task RetryLoadAsync() => LoadInvoicesAsync(_query);

    private async Task LoadInvoicesAsync(InvoiceListQuery query)
    {
        _query = query;
        var version = ++_queryVersion;
        _isLoading = _result is null || _loadError;
        _loadError = false;

        try
        {
            var result = await InvoiceService.GetInvoicesAsync(query, _cancellationTokenSource.Token);
            if (version == _queryVersion) _result = result;
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            if (version == _queryVersion)
            {
                Logger.LogError(exception, "Failed to load invoices.");
                _loadError = true;
            }
        }
        finally
        {
            if (version == _queryVersion) _isLoading = false;
        }
    }

    private async Task HandleInvoiceDeleted(InvoiceSummaryDto invoice)
    {
        await LoadInvoicesAsync(_query);
        _successMessage = $"Invoice {invoice.InvoiceNumber} deleted successfully";
        _successMessageVersion++;
    }

    private void DismissSuccess() => _successMessage = null;

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
