using Bunit;
using InvoiceFlow.Application.Invoices;
using InvoiceFlow.Application.Invoices.Abstractions;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Web.Components.Invoices;
using InvoiceFlow.Web.Components.Invoices.Models;
using InvoiceFlow.Web.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Web.Tests.Components.Invoices;

public class InvoiceEditorTests
{
    [Fact]
    public void CreateMode_RendersExpectedCopyEditableNumberAndNoDangerZone()
    {
        using var context = CreateContext();

        var cut = Render(context, ValidModel());

        Assert.Contains("New Invoice", cut.Markup);
        Assert.Contains("Create a new invoice by filling out the details and items below.", cut.Markup);
        Assert.Empty(cut.FindAll(".invoice-editor__danger-zone"));
        Assert.False(cut.Find("#invoice-number").HasAttribute("disabled"));
        Assert.False(cut.Find("#invoice-number").HasAttribute("readonly"));
        Assert.Single(cut.FindAll("button[type='submit']"));
        Assert.Contains("Generated automatically. You can change it before saving.", cut.Markup);
        Assert.Equal("true", cut.Find("#invoice-number").GetAttribute("aria-required"));
        Assert.Equal("true", cut.Find("#customer-name").GetAttribute("aria-required"));
        Assert.Equal("true", cut.Find("#issue-date").GetAttribute("aria-required"));
        Assert.Equal("true", cut.Find("#currency-code").GetAttribute("aria-required"));
        Assert.Contains("No line items yet", cut.Markup);
        Assert.Equal("Cancel", cut.Find(".invoice-editor__final-actions button:first-child").TextContent.Trim());
        Assert.Equal("Save Invoice", cut.Find(".invoice-editor__final-actions button[type='submit']").TextContent.Trim());
    }

    [Fact]
    public void Submit_WithValidZeroLineInvoice_InvokesSave()
    {
        using var context = CreateContext();
        var saveCount = 0;
        var cut = Render(context, ValidModel(), onSave: () => saveCount++);

        cut.Find("form").Submit();

        Assert.Equal(1, saveCount);
    }

    [Fact]
    public void EditMode_RendersExpectedCopyAndDangerZone()
    {
        using var context = CreateContext();

        var cut = Render(context, ValidModel(), invoiceId: Guid.NewGuid());

        Assert.Contains("Edit Invoice", cut.Markup);
        Assert.Contains("Modify invoice details, customer information, or line items.", cut.Markup);
        Assert.Single(cut.FindAll(".invoice-editor__danger-zone"));
        Assert.True(cut.Find("#invoice-number").HasAttribute("readonly"));
        Assert.Contains("Invoice numbers can't be changed after creation.", cut.Markup);
    }

    [Fact]
    public void InvalidSubmit_ShowsCountDoesNotSaveAndDisablesSaveUntilCorrected()
    {
        using var context = CreateContext();
        var saveCount = 0;
        var model = ValidModel();
        model.InvoiceNumber = string.Empty;
        model.CustomerName = string.Empty;
        var cut = Render(context, model, onSave: () => saveCount++);

        cut.Find("form").Submit();

        Assert.Equal(0, saveCount);
        Assert.Contains("2 errors were found", cut.Find(".invoice-editor__validation-alert").TextContent);
        Assert.All(cut.FindAll("button[type='submit']"), button => Assert.True(button.HasAttribute("disabled")));

        cut.Find("#invoice-number").Change("INV-001");
        cut.Find("#customer-name").Change("Acme Ltd");

        Assert.Empty(cut.FindAll(".invoice-editor__validation-alert"));
        Assert.All(cut.FindAll("button[type='submit']"), button => Assert.False(button.HasAttribute("disabled")));
    }

    [Fact]
    public void NestedLineItemError_ParticipatesInValidation()
    {
        using var context = CreateContext();
        var model = ValidModel();
        model.LineItems.Add(new InvoiceLineItemEditorModel());
        var cut = Render(context, model);

        cut.Find("form").Submit();

        Assert.Contains("1 error was found", cut.Find(".invoice-editor__validation-alert").TextContent);
        Assert.Contains("Description is required.", cut.Markup);
        Assert.Equal("true", cut.Find(".line-items-editor__field--description input").GetAttribute("aria-required"));
        Assert.Equal("true", cut.Find(".line-items-editor__field--quantity input").GetAttribute("aria-required"));
        Assert.Equal("true", cut.Find(".line-items-editor__field--price input").GetAttribute("aria-required"));
        Assert.False(cut.Find(".line-items-editor__field--discount input").HasAttribute("aria-required"));
        Assert.Equal(3, cut.FindAll(".line-items-editor__field--quantity input, .line-items-editor__field--price input, .line-items-editor__field--discount input").Count);
    }

    [Fact]
    public void StructureChangesAfterValidation_PreserveUnrelatedErrors()
    {
        using var context = CreateContext();
        var model = ValidModel();
        model.CustomerName = string.Empty;
        var cut = Render(context, model);
        cut.Find("form").Submit();

        cut.Find(".line-items-editor__empty-add-button").Click();
        Assert.Contains("2 errors were found", cut.Find(".invoice-editor__validation-alert").TextContent);
        Assert.Contains("Customer name is required.", cut.Markup);

        cut.Find(".line-items-editor__remove-button").Click();
        Assert.Contains("1 error was found", cut.Find(".invoice-editor__validation-alert").TextContent);
        Assert.Contains("Customer name is required.", cut.Markup);
    }

    [Fact]
    public async Task DuplicateNumberError_PersistsAcrossUnrelatedChangesAndClearsWhenNumberEdited()
    {
        using var context = CreateContext();
        var cut = Render(context, ValidModel());
        const string message = "An invoice with this number already exists.";

        await cut.InvokeAsync(() => cut.Instance.SetInvoiceNumberError(message));
        Assert.Contains(message, cut.Markup);

        cut.Find("#customer-name").Change("Updated Customer");
        cut.Find(".line-items-editor__empty-add-button").Click();
        cut.Find(".line-items-editor__remove-button").Click();
        Assert.Contains(message, cut.Markup);

        cut.Find("#invoice-number").Input("INV-002");
        Assert.DoesNotContain(message, cut.Markup);
    }

    [Fact]
    public void SavingState_DisablesAllMutationsAndShowsSavingUi()
    {
        using var context = CreateContext();
        var model = ValidModel();
        model.LineItems.Add(new InvoiceLineItemEditorModel
        {
            Description = "Consulting",
            Quantity = 1m,
            UnitPrice = 100m,
        });

        var cut = Render(context, model, invoiceId: Guid.NewGuid(), isSaving: true);

        Assert.Contains("Saving…", cut.Markup);
        Assert.All(cut.FindAll("input:not([readonly])"), input => Assert.True(input.HasAttribute("disabled")));
        Assert.True(cut.Find("#invoice-number").HasAttribute("readonly"));
        Assert.All(cut.FindAll("button"), button => Assert.True(button.HasAttribute("disabled")));
        Assert.Equal("true", cut.Find("form").GetAttribute("aria-busy"));
    }

    [Fact]
    public void AggregateOverflow_ShowsValidationAndPreventsSaveWithoutRenderingFailure()
    {
        using var context = CreateContext();
        var model = ValidModel();
        for (var index = 0; index < 8; index++)
        {
            model.LineItems.Add(new InvoiceLineItemEditorModel
            {
                Description = "Maximum",
                Quantity = InvoiceRules.MaximumQuantity,
                UnitPrice = InvoiceRules.MaximumUnitPrice,
            });
        }
        var saveCount = 0;

        var cut = Render(context, model, onSave: () => saveCount++);

        Assert.Contains("Invoice totals are too large to calculate", cut.Markup);
        Assert.Contains("Unavailable", cut.Find(".invoice-summary").TextContent);
        Assert.True(cut.Find("button[type='submit']").HasAttribute("disabled"));
        Assert.Equal(0, saveCount);

        cut.Find(".line-items-editor__field--price input").Input("0");

        Assert.DoesNotContain("Invoice totals are too large to calculate", cut.Markup);
        Assert.False(cut.Find("button[type='submit']").HasAttribute("disabled"));
    }

    [Fact]
    public void InvalidNumericScale_ShowsLineItemFieldErrorAndPreventsSave()
    {
        using var context = CreateContext();
        var model = ValidModel();
        model.LineItems.Add(new InvoiceLineItemEditorModel
        {
            Description = "Consulting",
            Quantity = 1.00001m,
            UnitPrice = 10m,
        });
        var saveCount = 0;
        var cut = Render(context, model, onSave: () => saveCount++);

        cut.Find("form").Submit();

        Assert.Contains("Quantity must have at most 4 decimal places.",
            cut.Find(".line-items-editor__field--quantity").TextContent);
        Assert.True(cut.Find("button[type='submit']").HasAttribute("disabled"));
        Assert.Equal(0, saveCount);
    }

    [Fact]
    public void NumericInput_ShowsScaleErrorAsFieldChanges()
    {
        using var context = CreateContext();
        var model = ValidModel();
        model.LineItems.Add(new InvoiceLineItemEditorModel
        {
            Description = "Consulting",
            Quantity = 1m,
            UnitPrice = 10m,
        });
        var cut = Render(context, model);

        cut.Find(".line-items-editor__field--quantity input").Input("1.00001");

        Assert.Contains("Quantity must have at most 4 decimal places.",
            cut.Find(".line-items-editor__field--quantity").TextContent);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.AddWebValidation();
        context.Services.AddSingleton<IInvoiceRepository, FakeInvoiceRepository>();
        context.Services.AddSingleton<InvoiceService>();
        return context;
    }

    private static IRenderedComponent<InvoiceEditor> Render(
        BunitContext context,
        InvoiceEditorModel model,
        Guid? invoiceId = null,
        bool isSaving = false,
        Action? onSave = null) =>
        context.Render<InvoiceEditor>(parameters => parameters
            .Add(component => component.Model, model)
            .Add(component => component.InvoiceId, invoiceId)
            .Add(component => component.IsSaving, isSaving)
            .Add(component => component.OnSave, onSave ?? (() => { })));

    private static InvoiceEditorModel ValidModel() => new()
    {
        InvoiceNumber = "INV-001",
        CustomerName = "Acme Ltd",
        IssueDate = new DateOnly(2026, 9, 25),
        CurrencyCode = "USD",
    };
}
