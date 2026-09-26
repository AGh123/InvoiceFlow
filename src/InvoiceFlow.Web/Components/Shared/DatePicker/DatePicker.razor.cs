using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace InvoiceFlow.Web.Components.Shared;

public partial class DatePicker
{
    private readonly string _instanceId = $"date-{Guid.NewGuid():N}";
    private ElementReference _root;
    private ElementReference _trigger;
    private IJSObjectReference? _module;
    private DotNetObjectReference<DatePicker>? _callback;
    private DateOnly _viewMonth;
    private DateOnly _focusedDate;
    private bool _open;
    private bool _focusDay;
    private bool _disposed;

    [Parameter, EditorRequired] public string Id { get; set; } = null!;
    [Parameter, EditorRequired] public string LabelId { get; set; } = null!;
    [Parameter] public DateOnly Value { get; set; }
    [Parameter] public EventCallback<DateOnly> ValueChanged { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool Invalid { get; set; }
    [Parameter] public string? DescribedBy { get; set; }

    private DateOnly FirstGridDate => _viewMonth.AddDays(-((int)_viewMonth.DayOfWeek + 6) % 7);
    private string DayId(DateOnly date) => $"{_instanceId}-{date:yyyyMMdd}";
    private string DayClass(DateOnly date) => $"date-picker__day{(date.Month != _viewMonth.Month ? " date-picker__day--adjacent" : "")}{(date == Value ? " date-picker__day--selected" : "")}{(date == DateOnly.FromDateTime(DateTime.Today) ? " date-picker__day--today" : "")}";

    private async Task ToggleAsync()
    {
        if (_open) { await CloseAsync(true); return; }
        _viewMonth = new DateOnly(Value.Year, Value.Month, 1);
        _focusedDate = Value;
        _open = true;
        _focusDay = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || !_open) return;
        _module ??= await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/Shared/DatePicker/DatePicker.razor.js");
        if (_disposed || !_open) return;
        _callback ??= DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync("watchOutside", _root, _callback);
        await _module.InvokeVoidAsync("installCalendarKeys", _root);
        if (_focusDay)
        {
            _focusDay = false;
            await _module.InvokeVoidAsync("focusById", DayId(_focusedDate));
        }
    }

    private async Task SelectAsync(DateOnly date)
    {
        await ValueChanged.InvokeAsync(date);
        await CloseAsync(true);
    }

    private void MoveMonth(int offset)
    {
        _viewMonth = _viewMonth.AddMonths(offset);
        _focusedDate = _viewMonth;
        _focusDay = true;
    }

    private void HandleDayKeyDown(KeyboardEventArgs args, DateOnly date)
    {
        var next = args.Key switch
        {
            "ArrowLeft" => date.AddDays(-1),
            "ArrowRight" => date.AddDays(1),
            "ArrowUp" => date.AddDays(-7),
            "ArrowDown" => date.AddDays(7),
            "Home" => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
            "End" => date.AddDays(6 - (((int)date.DayOfWeek + 6) % 7)),
            "PageUp" => date.AddMonths(-1),
            "PageDown" => date.AddMonths(1),
            _ => date,
        };
        if (next == date) return;
        _focusedDate = next;
        _viewMonth = new DateOnly(next.Year, next.Month, 1);
        _focusDay = true;
    }

    private async Task HandlePopupKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape") await CloseAsync(true);
    }

    [JSInvokable] public Task CloseFromOutside() => _disposed ? Task.CompletedTask : CloseAsync(false);

    private async Task CloseAsync(bool restoreFocus)
    {
        if (!_open) return;
        _open = false;
        if (_module is not null) await _module.InvokeVoidAsync("unwatchOutside", _root);
        if (_module is not null) await _module.InvokeVoidAsync("uninstallCalendarKeys", _root);
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
                await _module.InvokeVoidAsync("uninstallCalendarKeys", _root);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        finally { _callback?.Dispose(); }
    }
}
