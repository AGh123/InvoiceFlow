using Bunit;
using InvoiceFlow.Web.Components.Shared;

namespace InvoiceFlow.Web.Tests.Components.Shared;

public class StatusMessageTests
{
    [Theory]
    [InlineData(StatusMessageKind.Success, "status", "polite", "status-message--success")]
    [InlineData(StatusMessageKind.Error, "alert", "assertive", "status-message--error")]
    public void Kind_UsesAppropriateScreenReaderSemantics(
        StatusMessageKind kind, string role, string liveSetting, string cssClass)
    {
        using var context = new BunitContext();

        var cut = context.Render<StatusMessage>(parameters => parameters
            .Add(component => component.Kind, kind)
            .Add(component => component.Message, "Invoice status"));

        var message = cut.Find($".{cssClass}");
        Assert.Equal(role, message.GetAttribute("role"));
        Assert.Equal(liveSetting, message.GetAttribute("aria-live"));
    }
}
