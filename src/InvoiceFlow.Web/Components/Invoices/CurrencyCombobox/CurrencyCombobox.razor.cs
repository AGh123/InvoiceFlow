using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace InvoiceFlow.Web.Components.Invoices;

public partial class CurrencyCombobox
{
    private static readonly (string Code, string Name)[] Common =
    [
        ("USD", "US Dollar"), ("EUR", "Euro"), ("GBP", "British Pound"),
        ("CAD", "Canadian Dollar"), ("AUD", "Australian Dollar"), ("JPY", "Japanese Yen"),
    ];
    private readonly string _listId = $"currency-list-{Guid.NewGuid():N}";
    private ElementReference _root;
    private ElementReference _input;
    private IJSObjectReference? _module;
    private DotNetObjectReference<CurrencyCombobox>? _callback;
    private bool _open;
    private bool _keysInstalled;
    private bool _disposed;
    private string _query = string.Empty;
    private string? _lastValue;
    private int _activeIndex = -1;

    [Parameter] public string Value { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool Invalid { get; set; }

    protected override void OnParametersSet()
    {
        if (_lastValue == Value) return;
        _lastValue = Value;
        _query = Value;
    }

    private List<(string Code, string Name)> Filtered => Common
        .Where(option => string.IsNullOrWhiteSpace(_query) ||
            option.Code.Contains(_query, StringComparison.OrdinalIgnoreCase) ||
            option.Name.Contains(_query, StringComparison.OrdinalIgnoreCase))
        .ToList();
    private string? ActiveOptionId => _open && _activeIndex >= 0 && _activeIndex < Filtered.Count
        ? OptionId(_activeIndex) : null;
    private string OptionId(int index) => $"{_listId}-{index}";

    private async Task InputAsync(ChangeEventArgs args)
    {
        var value = (args.Value?.ToString() ?? string.Empty).ToUpperInvariant();
        _query = value;
        _activeIndex = -1;
        _open = true;
        await ValueChanged.InvokeAsync(value);
    }

    private void Open()
    {
        _activeIndex = Filtered.FindIndex(option => option.Code == Value);
        _open = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;
        _module ??= await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/Invoices/CurrencyCombobox/CurrencyCombobox.razor.js");
        if (_disposed) return;
        if (!_keysInstalled)
        {
            await _module.InvokeVoidAsync("installComboboxKeys", _input);
            _keysInstalled = true;
        }
        if (!_open) return;
        _callback ??= DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync("watchOutside", _root, _callback);
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Escape") { await CloseAsync(); return; }
        var options = Filtered;
        if (args.Key is "ArrowDown" or "ArrowUp" && options.Count > 0)
        {
            _open = true;
            _activeIndex = args.Key == "ArrowDown"
                ? (_activeIndex + 1) % options.Count
                : (_activeIndex + options.Count - 1) % options.Count;
        }
        else if (args.Key == "Enter" && _open && _activeIndex >= 0 && _activeIndex < options.Count)
        {
            await SelectAsync(options[_activeIndex].Code);
        }
    }

    private async Task SelectAsync(string code)
    {
        _query = code;
        await ValueChanged.InvokeAsync(code);
        await CloseAsync();
        await _input.FocusAsync();
    }

    [JSInvokable] public Task CloseFromOutside() => _disposed ? Task.CompletedTask : CloseAsync();

    private async Task CloseAsync()
    {
        if (!_open) return;
        _open = false;
        _activeIndex = -1;
        if (_module is not null) await _module.InvokeVoidAsync("unwatchOutside", _root);
        await InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("unwatchOutside", _root);
                await _module.InvokeVoidAsync("uninstallComboboxKeys", _input);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        finally { _callback?.Dispose(); }
    }
}
