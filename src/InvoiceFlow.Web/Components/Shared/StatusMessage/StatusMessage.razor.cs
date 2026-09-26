using Microsoft.AspNetCore.Components;

namespace InvoiceFlow.Web.Components.Shared;

public partial class StatusMessage
{
    [Parameter, EditorRequired]
    public string Message { get; set; } = string.Empty;

    [Parameter]
    public string? Detail { get; set; }

    [Parameter]
    public StatusMessageKind Kind { get; set; } = StatusMessageKind.Success;

    [Parameter]
    public bool Floating { get; set; }

    [Parameter]
    public int AutoDismissMilliseconds { get; set; }

    [Parameter]
    public string? ActionText { get; set; }

    [Parameter]
    public EventCallback OnAction { get; set; }

    [Parameter]
    public EventCallback OnDismiss { get; set; }

    private CancellationTokenSource? _autoDismissCancellation;
    private string? _scheduledMessage;
    private int _scheduledMilliseconds;
    private bool _disposed;

    protected override void OnParametersSet()
    {
        var duration = Floating && !IsError && OnDismiss.HasDelegate
            ? AutoDismissMilliseconds : 0;
        if (_scheduledMessage == Message && _scheduledMilliseconds == duration) return;

        CancelAutoDismiss();
        _scheduledMessage = Message;
        _scheduledMilliseconds = duration;
        if (duration <= 0) return;

        _autoDismissCancellation = new CancellationTokenSource();
        _ = DismissLaterAsync(duration, _autoDismissCancellation.Token);
    }

    private async Task DismissLaterAsync(int duration, CancellationToken token)
    {
        try
        {
            await Task.Delay(duration, token);
            await InvokeAsync(async () =>
            {
                if (!_disposed && !token.IsCancellationRequested)
                    await OnDismiss.InvokeAsync();
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private void CancelAutoDismiss()
    {
        _autoDismissCancellation?.Cancel();
        _autoDismissCancellation?.Dispose();
        _autoDismissCancellation = null;
    }

    public void Dispose()
    {
        _disposed = true;
        CancelAutoDismiss();
    }

    private bool IsError => Kind == StatusMessageKind.Error;
    private string CssClass => $"status-message {(IsError ? "status-message--error" : "status-message--success")}{(Floating ? " status-message--floating" : string.Empty)}";
    private string IconPath => IsError ? "assets/icons/alert-circle.svg" : "assets/icons/check-circle.svg";
    private string Role => IsError ? "alert" : "status";
    private string LiveSetting => IsError ? "assertive" : "polite";
}
