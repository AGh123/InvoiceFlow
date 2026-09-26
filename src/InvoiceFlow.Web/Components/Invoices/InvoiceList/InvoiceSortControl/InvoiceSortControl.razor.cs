using InvoiceFlow.Application.Invoices;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace InvoiceFlow.Web.Components.Invoices;

public partial class InvoiceSortControl
{
    private static readonly (InvoiceSortOption Key, string Label)[] Options =
    [
        (InvoiceSortOption.Newest, "Newest first"), (InvoiceSortOption.Oldest, "Oldest first"),
        (InvoiceSortOption.Customer, "Customer A–Z"), (InvoiceSortOption.InvoiceNumber, "Invoice number"),
    ];
    private readonly string _listId = $"sort-list-{Guid.NewGuid():N}";
    private ElementReference _root;
    private ElementReference _trigger;
    private IJSObjectReference? _module;
    private DotNetObjectReference<InvoiceSortControl>? _callback;
    private bool _open;
    private bool _focusOption;
    private bool _disposed;
    private int _activeIndex;

    [Parameter] public InvoiceSortOption Value { get; set; } = InvoiceSortOption.Newest;
    [Parameter] public EventCallback<InvoiceSortOption> ValueChanged { get; set; }

    private string OptionId(int index) => $"{_listId}-{index}";

    private async Task ToggleAsync()
    {
        if (_open) { await CloseAsync(true); return; }
        Open();
    }

    private void Open()
    {
        _activeIndex = Math.Max(0, Array.FindIndex(Options, option => option.Key == Value));
        _open = true;
        _focusOption = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || !_open) return;
        _module ??= await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/Invoices/InvoiceList/InvoiceSortControl/InvoiceSortControl.razor.js");
        if (_disposed || !_open) return;
        _callback ??= DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync("watchOutside", _root, _callback);
        if (_focusOption)
        {
            _focusOption = false;
            await _module.InvokeVoidAsync("focusById", OptionId(_activeIndex));
        }
    }

    private async Task TriggerKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key is "ArrowDown" or "ArrowUp") Open();
        else if (args.Key == "Escape") await CloseAsync(true);
    }

    private async Task OptionKeyDownAsync(KeyboardEventArgs args, int index)
    {
        if (args.Key == "Escape") { await CloseAsync(true); return; }
        if (args.Key is "ArrowDown" or "ArrowUp" or "Home" or "End")
        {
            _activeIndex = args.Key switch
            {
                "Home" => 0,
                "End" => Options.Length - 1,
                "ArrowDown" => (index + 1) % Options.Length,
                _ => (index + Options.Length - 1) % Options.Length,
            };
            _focusOption = true;
        }
    }

    private async Task SelectAsync(InvoiceSortOption key)
    {
        await ValueChanged.InvokeAsync(key);
        await CloseAsync(true);
    }

    [JSInvokable] public Task CloseFromOutside() => _disposed ? Task.CompletedTask : CloseAsync(false);

    private async Task CloseAsync(bool restoreFocus)
    {
        if (!_open) return;
        _open = false;
        if (_module is not null) await _module.InvokeVoidAsync("unwatchOutside", _root);
        await InvokeAsync(StateHasChanged);
        if (restoreFocus && !_disposed && _module is not null)
            await _module.InvokeVoidAsync("focusIfConnected", _trigger);
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("unwatchOutside", _root);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        finally { _callback?.Dispose(); }
    }
}
