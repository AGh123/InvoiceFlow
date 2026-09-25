namespace InvoiceFlow.Web.Components.Invoices;

public sealed class InvoiceNotificationState
{
    private string? _message;

    public void Publish(string message) => _message = message;

    public string? Consume()
    {
        var message = _message;
        _message = null;
        return message;
    }
}
