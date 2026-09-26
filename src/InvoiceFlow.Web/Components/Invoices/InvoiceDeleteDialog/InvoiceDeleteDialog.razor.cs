using InvoiceFlow.Application.Invoices.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace InvoiceFlow.Web.Components.Invoices;

public partial class InvoiceDeleteDialog
{
    private ElementReference _dialog;
    private ElementReference _cancelButton;
    private IJSObjectReference? _module;
    private InvoiceSummaryDto? _invoice;
    private string? _errorMessage;
    private bool _isBusy;
    private bool _showRequested;

    [Parameter]
    public EventCallback<InvoiceSummaryDto> InvoiceDeleted { get; set; }

    public async Task ShowAsync(InvoiceSummaryDto invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        _invoice = invoice;
        _errorMessage = null;
        _showRequested = true;
        await InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_showRequested)
        {
            return;
        }

        _showRequested = false;
        _module ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            "./js/dialog.js");
        await _module.InvokeVoidAsync("showDialog", _dialog, _cancelButton);
    }

    private async Task DeleteAsync()
    {
        if (_invoice is null || _isBusy)
        {
            return;
        }

        var invoice = _invoice;
        _isBusy = true;
        _errorMessage = null;

        try
        {
            var deleted = await InvoiceService.DeleteInvoiceAsync(invoice.Id);
            if (!deleted)
            {
                _errorMessage = "We couldn't find this invoice. Refresh the list and try again.";
                return;
            }

            await InvoiceDeleted.InvokeAsync(invoice);
            _isBusy = false;
            await CloseAsync();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to delete invoice {InvoiceId}.", invoice.Id);
            _errorMessage = "We couldn't delete the invoice. Your list is unchanged. Try again.";
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task CloseAsync()
    {
        if (_isBusy || _module is null)
        {
            return;
        }

        await _module.InvokeVoidAsync("closeDialog", _dialog);
    }

    private void HandleClosed()
    {
        _invoice = null;
        _errorMessage = null;
        _isBusy = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("disposeDialog", _dialog);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit has already disconnected.
            }
        }
    }
}
