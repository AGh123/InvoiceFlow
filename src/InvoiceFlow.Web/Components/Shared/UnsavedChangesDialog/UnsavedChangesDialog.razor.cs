using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace InvoiceFlow.Web.Components.Shared;

public partial class UnsavedChangesDialog
{
    private ElementReference _dialog;
    private ElementReference _keepButton;
    private IJSObjectReference? _module;
    private bool _showRequested;
    private bool _isOpen;

    [Parameter]
    public EventCallback OnDiscard { get; set; }

    public async Task ShowAsync()
    {
        _showRequested = true;
        _isOpen = true;
        await InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_showRequested) return;
        _showRequested = false;
        _module ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/dialog.js");
        await _module.InvokeVoidAsync("showDialog", _dialog, _keepButton);
    }

    private async Task CloseAsync()
    {
        _isOpen = false;
        if (_module is not null) await _module.InvokeVoidAsync("closeDialog", _dialog);
    }

    private async Task DiscardAsync()
    {
        await CloseAsync();
        await OnDiscard.InvokeAsync();
    }

    private void Closed()
    {
        _showRequested = false;
        _isOpen = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null) return;
        try
        {
            await _module.InvokeVoidAsync("disposeDialog", _dialog);
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
    }
}
