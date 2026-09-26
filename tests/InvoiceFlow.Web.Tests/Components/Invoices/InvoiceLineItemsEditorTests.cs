using Bunit;
using InvoiceFlow.Web.Components.Invoices;
using InvoiceFlow.Web.Components.Invoices.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace InvoiceFlow.Web.Tests.Components.Invoices;

public class InvoiceLineItemsEditorTests
{
    [Fact]
    public void ZeroItems_ShowsSingleAddActionAndZeroSummaryWithoutDiscount()
    {
        using var context = new BunitContext();
        var model = ValidModel();

        var cut = Render(context, model);

        Assert.Contains("No line items yet", cut.Markup);
        Assert.Single(cut.FindAll(".line-items-editor__empty button"));
        Assert.Empty(cut.FindAll(".invoice-summary__row--discount"));
        Assert.Equal(2, cut.Markup.Split("$0.00").Length - 1);
    }

    [Fact]
    public void Add_CreatesDefaultEditableItemAndShowsHeaderAction()
    {
        using var context = new BunitContext();
        var model = ValidModel();

        var cut = Render(context, model);
        cut.Find(".line-items-editor__empty-add-button").Click();

        var added = Assert.Single(model.LineItems);
        Assert.Null(added.Id);
        Assert.Equal(string.Empty, added.Description);
        Assert.Equal(1m, added.Quantity);
        Assert.Equal(0m, added.UnitPrice);
        Assert.Equal(0m, added.DiscountPercent);
        Assert.Single(cut.FindAll(".line-items-editor__add-button"));
        Assert.Equal(4, cut.FindAll(".line-items-editor__row input").Count);
    }

    [Fact]
    public void LineItemNumericInputs_OptIntoSelectOnFocus()
    {
        using var context = new BunitContext();
        var model = ValidModel();
        model.LineItems.Add(Item("Service"));

        var cut = Render(context, model);

        var numericInputs = cut.FindComponents<InputNumberOnInput<decimal>>();
        Assert.Equal(3, numericInputs.Count);
        Assert.All(numericInputs, input => Assert.True(input.Instance.SelectOnFocus));
        Assert.All(cut.FindAll(".line-items-editor__row input"), input =>
            Assert.Contains("form-control", input.GetAttribute("class")));
    }

    [Fact]
    public void LineTotalAndSummary_UseSameFormattingForArbitraryCurrency()
    {
        using var context = new BunitContext();
        var model = ValidModel();
        model.CurrencyCode = "CHF";
        model.LineItems.Add(Item("Service"));

        var cut = Render(context, model);

        Assert.Contains("CHF 10.00", cut.Find(".line-items-editor__total").TextContent);
        Assert.Contains("CHF 10.00", cut.Find(".invoice-summary__row--total").TextContent);
    }

    [Fact]
    public void Remove_RemovesOnlySelectedItemAndPreservesRemainingItems()
    {
        using var context = new BunitContext();
        var model = ValidModel();
        var first = Item("First");
        var second = Item("Second");
        var third = Item("Third");
        model.LineItems.AddRange([first, second, third]);

        var cut = Render(context, model);
        cut.FindAll(".line-items-editor__remove-button")[1].Click();

        Assert.Equal([first, third], model.LineItems);
        Assert.DoesNotContain("Second", cut.Markup);
        Assert.Contains("First", cut.Markup);
        Assert.Contains("Third", cut.Markup);
    }

    [Fact]
    public void RemoveFinalItem_ReturnsToZeroLineState()
    {
        using var context = new BunitContext();
        var model = ValidModel();
        model.LineItems.Add(Item("Only item"));

        var cut = Render(context, model);
        cut.Find(".line-items-editor__remove-button").Click();

        Assert.Empty(model.LineItems);
        Assert.Contains("No line items yet", cut.Markup);
        Assert.Single(cut.FindAll(".line-items-editor__empty button"));
    }

    [Fact]
    public void Disabled_DisablesAddRemoveAndEditableFields()
    {
        using var context = new BunitContext();
        var model = ValidModel();
        model.LineItems.Add(Item("Locked"));

        var cut = Render(context, model, disabled: true);

        Assert.All(cut.FindAll("button"), button => Assert.True(button.HasAttribute("disabled")));
        Assert.All(cut.FindAll("input"), input => Assert.True(input.HasAttribute("disabled")));
    }

    private static IRenderedComponent<InvoiceLineItemsEditor> Render(
        BunitContext context,
        InvoiceEditorModel model,
        bool disabled = false)
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var editContext = new EditContext(model);
        return context.Render<InvoiceLineItemsEditor>(parameters => parameters
            .AddCascadingValue(editContext)
            .Add(component => component.Model, model)
            .Add(component => component.Disabled, disabled));
    }

    private static InvoiceEditorModel ValidModel() => new()
    {
        InvoiceNumber = "INV-001",
        CustomerName = "Acme Ltd",
        CurrencyCode = "USD",
    };

    private static InvoiceLineItemEditorModel Item(string description) => new()
    {
        Description = description,
        Quantity = 1m,
        UnitPrice = 10m,
    };
}
