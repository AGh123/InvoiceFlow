using InvoiceFlow.Application.Invoices.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace InvoiceFlow.Web.Components.Invoices;

public partial class InvoiceActionsMenu
{
    private readonly string _menuId = $"invoice-menu-{Guid.NewGuid():N}";
    private string _triggerId => $"{_menuId}-trigger";
    private ElementReference _root;
    private ElementReference _trigger;
    private ElementReference _menu;
    private IJSObjectReference? _module;
    private DotNetObjectReference<InvoiceActionsMenu>? _callback;
    private int _activeIndex;
    private bool _focusOption;
    private bool _watching;
    private bool _closing;
    private bool _disposed;

    [Parameter, EditorRequired] public InvoiceSummaryDto Invoice { get; set; } = null!;
    [Parameter] public bool Open { get; set; }
    [Parameter] public EventCallback OnToggle { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback<InvoiceSummaryDto> OnDelete { get; set; }

    private async Task ToggleAsync()
    {
        if (Open) { await CloseAsync(true); return; }
        _activeIndex = 0;
        _focusOption = true;
        await OnToggle.InvokeAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!Open)
        {
            await UnwatchAsync();
            return;
        }
        _module ??= await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/Invoices/InvoiceList/InvoiceActionsMenu/InvoiceActionsMenu.razor.js");
        if (_disposed || !Open) return;
        _callback ??= DotNetObjectReference.Create(this);
        if (!_watching)
        {
            await _module.InvokeVoidAsync("watchOutside", _root, _callback);
            _watching = true;
        }
        if (_disposed || !Open)
        {
            await UnwatchAsync();
            return;
        }
        await _module.InvokeVoidAsync("placeMenu", _trigger, _menu);
        if (_disposed || !Open) return;
        if (_focusOption)
        {
            _focusOption = false;
            await _module.InvokeVoidAsync("focusById", $"{_menuId}-{(_activeIndex == 0 ? "open" : "delete")}");
        }
    }

    private async Task TriggerKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "ArrowDown")
        {
            if (!Open) await ToggleAsync();
        }
        else if (args.Key == "Escape") await CloseAsync(true);
    }

    private async Task MenuKeyDownAsync(KeyboardEventArgs args, int index)
    {
        if (args.Key == "Escape") { await CloseAsync(true); return; }
        if (args.Key is "ArrowDown" or "ArrowUp" or "Home" or "End")
        {
            _activeIndex = args.Key switch
            {
                "Home" => 0,
                "End" => 1,
                _ => 1 - index,
            };
            _focusOption = true;
        }
    }

    private Task CloseForNavigation() => CloseAsync(false);

    private async Task DeleteAsync()
    {
        var invoice = Invoice;
        await UnwatchAsync();
        try { await OnDelete.InvokeAsync(invoice); }
        finally { await CloseAsync(false); }
    }

    [JSInvokable] public Task CloseFromOutside() => _disposed ? Task.CompletedTask : CloseAsync(false);

    private async Task CloseAsync(bool restoreFocus)
    {
        if (_closing || (!Open && !_watching)) return;
        _closing = true;
        try
        {
            await UnwatchAsync();
            if (Open) await OnClose.InvokeAsync();
            if (restoreFocus && !_disposed && _module is not null)
                await _module.InvokeVoidAsync("focusIfConnected", _trigger);
        }
        finally
        {
            _closing = false;
        }
    }

    private async Task UnwatchAsync()
    {
        if (!_watching || _module is null) return;
        _watching = false;
        try { await _module.InvokeVoidAsync("unwatchOutside", _root); }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        try
        {
            if (_module is not null)
            {
                await UnwatchAsync();
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        finally { _callback?.Dispose(); }
    }
}
