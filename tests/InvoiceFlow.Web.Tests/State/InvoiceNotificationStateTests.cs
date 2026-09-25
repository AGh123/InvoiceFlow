using InvoiceFlow.Web.Components.Invoices;

namespace InvoiceFlow.Web.Tests.State;

public class InvoiceNotificationStateTests
{
    [Fact]
    public void Consume_ReturnsPublishedMessageOnlyOnce()
    {
        var state = new InvoiceNotificationState();
        state.Publish("Saved");

        Assert.Equal("Saved", state.Consume());
        Assert.Null(state.Consume());
    }

    [Fact]
    public void Publish_ReplacesPendingMessage()
    {
        var state = new InvoiceNotificationState();
        state.Publish("First");
        state.Publish("Newest");

        Assert.Equal("Newest", state.Consume());
    }
}
