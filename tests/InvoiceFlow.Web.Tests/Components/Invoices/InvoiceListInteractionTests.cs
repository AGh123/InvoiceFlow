using Bunit;
using InvoiceFlow.Application.Invoices;
using InvoiceFlow.Application.Invoices.Abstractions;
using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Web.Components.Invoices;
using InvoiceFlow.Web.Components.Invoices.Components;
using InvoiceFlow.Web.Components.Pages.Invoices;
using InvoiceFlow.Web.Tests.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Web.Tests.Components.Invoices;

public class InvoiceListInteractionTests
{
    [Theory]
    [InlineData("Newest first", "INV-B,INV-C,INV-A")]
    [InlineData("Oldest first", "INV-A,INV-C,INV-B")]
    [InlineData("Customer A–Z", "INV-C,INV-A,INV-B")]
    [InlineData("Invoice number", "INV-A,INV-B,INV-C")]
    public void SortMenu_PreservesExpectedOrdering(string option, string expected)
    {
        using var context = Context(out _);
        var cut = context.Render<InvoiceList>(parameters => parameters.Add(
            component => component.Invoices, Invoices()));
        cut.Find("#invoice-sort").Click();
        cut.FindAll(".sort-option").Single(item => item.TextContent.Contains(option)).Click();

        var actual = string.Join(',', cut.FindAll(".desktop-list tbody tr .invoice-link")
            .Select(link => link.TextContent.Trim()));
        Assert.Equal(expected, actual);
        Assert.Equal(option, cut.Find("#invoice-sort span").TextContent);
        Assert.Single(cut.FindAll(".desktop-list table"));
        Assert.Single(cut.FindAll(".mobile-list"));
        Assert.Equal(3, cut.FindAll(".mobile-list .invoice-card").Count);
    }

    [Fact]
    public void InvoiceActions_OnlyOneMenuOpensAndContainsOpenAndDelete()
    {
        using var context = Context(out _);
        var module = context.JSInterop.SetupModule("./js/floating-control.js");
        module.Mode = JSRuntimeMode.Loose;
        var cut = context.Render<InvoiceList>(parameters => parameters.Add(
            component => component.Invoices, Invoices()));
        var triggers = cut.FindAll(".desktop-list .actions-trigger");

        triggers[0].Click();
        Assert.Single(cut.FindAll(".desktop-list .actions-menu"));
        Assert.Equal("true", cut.FindAll(".desktop-list .actions-trigger")[0].GetAttribute("aria-expanded"));
        Assert.Contains("Open invoice", cut.Find(".desktop-list .actions-menu").TextContent);
        Assert.StartsWith("/invoices/", cut.Find(".desktop-list .actions-menu a").GetAttribute("href"));
        Assert.Contains("Delete", cut.Find(".desktop-list .actions-menu").TextContent);

        triggers[1].Click();
        Assert.Single(cut.FindAll(".desktop-list .actions-menu"));
        Assert.Equal("false", cut.FindAll(".desktop-list .actions-trigger")[0].GetAttribute("aria-expanded"));
        Assert.Equal("true", cut.FindAll(".desktop-list .actions-trigger")[1].GetAttribute("aria-expanded"));
        module.VerifyInvoke("unwatchOutside");
    }

    [Fact]
    public void InvoiceActions_ExternalOpenFalseUnwatchesOutsideListener()
    {
        using var context = Context(out _);
        var module = context.JSInterop.SetupModule("./js/floating-control.js");
        module.Mode = JSRuntimeMode.Loose;
        var invoice = Invoices()[0];
        var cut = context.Render<InvoiceActionsMenu>(parameters => parameters
            .Add(component => component.Invoice, invoice)
            .Add(component => component.Open, true));

        module.VerifyInvoke("watchOutside");
        cut.Render(parameters => parameters
            .Add(component => component.Invoice, invoice)
            .Add(component => component.Open, false));

        module.VerifyInvoke("unwatchOutside");
        Assert.Equal("false", cut.Find(".actions-trigger").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void DeleteMenuAction_OpensConfirmationAndDeletesSelectedInvoice()
    {
        using var context = Context(out var repository);
        var dialogModule = context.JSInterop.SetupModule(
            "./Components/Invoices/Components/InvoiceDeleteDialog.razor.js");
        dialogModule.Mode = JSRuntimeMode.Loose;
        var invoice = Invoices()[0];
        repository.Invoices.Add(new Invoice(invoice.Id, invoice.InvoiceNumber,
            invoice.CustomerName, invoice.IssueDate, invoice.CurrencyCode));
        var invoices = new List<InvoiceSummaryDto> { invoice };
        InvoiceSummaryDto? deleted = null;
        var cut = context.Render<InvoiceList>(parameters => parameters
            .Add(component => component.Invoices, invoices)
            .Add(component => component.InvoiceDeleted, value =>
            {
                deleted = value;
                invoices.RemoveAll(existing => existing.Id == value.Id);
            }));

        cut.Find(".desktop-list .actions-trigger").Click();
        cut.Find(".desktop-list .actions-menu button").Click();
        dialogModule.VerifyInvoke("showDialog");
        Assert.Empty(cut.FindAll(".actions-menu"));
        Assert.Contains("Delete invoice?", cut.Markup);
        Assert.Contains(invoice.InvoiceNumber, cut.Find(".delete-dialog").TextContent);
        Assert.Contains(invoice.CustomerName, cut.Find(".delete-dialog").TextContent);

        cut.Find(".delete-dialog .button-secondary").Click();
        dialogModule.VerifyInvoke("closeDialog");
        Assert.Single(repository.Invoices);
        Assert.Null(deleted);

        cut.Find(".desktop-list .actions-trigger").Click();
        cut.Find(".desktop-list .actions-menu button").Click();
        cut.Find(".delete-dialog .button-danger").Click();

        cut.WaitForAssertion(() => Assert.Equal(invoice.Id, deleted?.Id));
        Assert.Empty(repository.Invoices);
        Assert.Empty(cut.FindAll(".desktop-list .invoice-link"));
    }

    [Fact]
    public void CleanEditorCancel_ThenListMenuDelete_CompletesWithoutStaleComponentCallbacks()
    {
        using var context = Context(out var repository);
        context.AddWebValidation();
        context.Services.AddSingleton<InvoiceNotificationState>();
        var invoice = new Invoice(Guid.NewGuid(), "INV-RETURN", "Return customer",
            new DateOnly(2026, 9, 25), "USD");
        repository.Invoices.Add(invoice);
        var editor = context.Render<InvoiceEditorPage>(parameters => parameters
            .Add(component => component.Id, invoice.Id));

        editor.Find(".final-actions .button-secondary").Click();
        Assert.EndsWith("/invoices", context.Services.GetRequiredService<NavigationManager>().Uri);
        editor.Dispose();

        var summary = new InvoiceSummaryDto(invoice.Id, invoice.InvoiceNumber,
            invoice.CustomerName, invoice.IssueDate, invoice.CurrencyCode, 0m);
        var list = context.Render<InvoiceList>(parameters => parameters
            .Add(component => component.Invoices, new[] { summary }));
        list.Find(".desktop-list .actions-trigger").Click();
        list.Find(".desktop-list .actions-menu button").Click();
        list.Find(".delete-dialog .button-danger").Click();

        list.WaitForAssertion(() => Assert.Empty(repository.Invoices));
    }

    private static BunitContext Context(out FakeInvoiceRepository repository)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        repository = new FakeInvoiceRepository();
        context.Services.AddSingleton<IInvoiceRepository>(repository);
        context.Services.AddSingleton<InvoiceService>();
        return context;
    }

    private static InvoiceSummaryDto[] Invoices() =>
    [
        new(Guid.NewGuid(), "INV-A", "Beta", new DateOnly(2026, 1, 1), "USD", 1m),
        new(Guid.NewGuid(), "INV-B", "Zulu", new DateOnly(2026, 3, 1), "USD", 2m),
        new(Guid.NewGuid(), "INV-C", "Alpha", new DateOnly(2026, 2, 1), "CHF", 3m),
    ];
}
