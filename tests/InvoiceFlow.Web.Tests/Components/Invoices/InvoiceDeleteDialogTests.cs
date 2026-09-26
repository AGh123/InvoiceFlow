using Bunit;
using InvoiceFlow.Application.Invoices;
using InvoiceFlow.Application.Invoices.Abstractions;
using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Web.Components.Invoices;
using InvoiceFlow.Web.Tests.TestDoubles;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Web.Tests.Components.Invoices;

public class InvoiceDeleteDialogTests
{
    private const string ModulePath =
        "./js/dialog.js";

    [Fact]
    public async Task Show_RendersInvoiceInformationAndCancelClosesDialog()
    {
        using var context = CreateContext(out _);
        var module = context.JSInterop.SetupModule(ModulePath);
        module.Mode = JSRuntimeMode.Loose;
        var cut = context.Render<InvoiceDeleteDialog>();

        await cut.InvokeAsync(() => cut.Instance.ShowAsync(Summary()));

        Assert.Contains("INV-100", cut.Markup);
        Assert.Contains("Acme Ltd", cut.Markup);
        cut.Find("button.button--secondary").Click();
        module.VerifyInvoke("closeDialog");
    }

    [Fact]
    public async Task Delete_SuccessUsesServiceAndRaisesInvoiceDeleted()
    {
        using var context = CreateContext(out var repository);
        var invoice = Invoice();
        repository.Invoices.Add(invoice);
        var deleted = new List<InvoiceSummaryDto>();
        var module = context.JSInterop.SetupModule(ModulePath);
        module.Mode = JSRuntimeMode.Loose;
        var cut = context.Render<InvoiceDeleteDialog>(parameters => parameters
            .Add(component => component.InvoiceDeleted, deleted.Add));
        await cut.InvokeAsync(() => cut.Instance.ShowAsync(Summary(invoice.Id)));

        cut.Find("button.button--danger").Click();

        Assert.Equal(1, repository.DeleteCallCount);
        Assert.Equal(invoice.Id, Assert.Single(deleted).Id);
        module.VerifyInvoke("closeDialog");
    }

    [Fact]
    public async Task Delete_NotFoundShowsSafeFeedback()
    {
        using var context = CreateContext(out _);
        var module = context.JSInterop.SetupModule(ModulePath);
        module.Mode = JSRuntimeMode.Loose;
        var cut = context.Render<InvoiceDeleteDialog>();
        await cut.InvokeAsync(() => cut.Instance.ShowAsync(Summary()));

        cut.Find("button.button--danger").Click();

        Assert.Contains("We couldn't find this invoice. Refresh the list and try again.", cut.Markup);
    }

    [Fact]
    public async Task Delete_ThrownFailureShowsSafeFeedback()
    {
        using var context = CreateContext(out var repository);
        var invoice = Invoice();
        repository.Invoices.Add(invoice);
        repository.DeleteHandler = (_, _) => throw new InvalidOperationException("database details");
        var module = context.JSInterop.SetupModule(ModulePath);
        module.Mode = JSRuntimeMode.Loose;
        var cut = context.Render<InvoiceDeleteDialog>();
        await cut.InvokeAsync(() => cut.Instance.ShowAsync(Summary(invoice.Id)));

        cut.Find("button.button--danger").Click();

        Assert.Contains("We couldn't delete the invoice. Your list is unchanged. Try again.", cut.Markup);
        Assert.DoesNotContain("database details", cut.Markup);
    }

    [Fact]
    public async Task Delete_WhileBusyPreventsDuplicateDelete()
    {
        using var context = CreateContext(out var repository);
        var invoice = Invoice();
        repository.Invoices.Add(invoice);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.DeleteHandler = (_, _) => completion.Task;
        var module = context.JSInterop.SetupModule(ModulePath);
        module.Mode = JSRuntimeMode.Loose;
        var cut = context.Render<InvoiceDeleteDialog>();
        await cut.InvokeAsync(() => cut.Instance.ShowAsync(Summary(invoice.Id)));

        var deleteTask = cut.Find("button.button--danger").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, repository.DeleteCallCount);
            Assert.True(cut.Find("button.button--danger").HasAttribute("disabled"));
        });

        cut.Find("button.button--danger").Click();
        Assert.Equal(1, repository.DeleteCallCount);
        completion.SetResult();
        await deleteTask;
    }

    private static BunitContext CreateContext(out FakeInvoiceRepository repository)
    {
        var context = new BunitContext();
        context.Services.AddLogging();
        repository = new FakeInvoiceRepository();
        context.Services.AddSingleton<IInvoiceRepository>(repository);
        context.Services.AddSingleton<InvoiceService>();
        return context;
    }

    private static Invoice Invoice() =>
        new(Guid.NewGuid(), "INV-100", "Acme Ltd", new DateOnly(2026, 9, 25), "USD");

    private static InvoiceSummaryDto Summary(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "INV-100", "Acme Ltd", new DateOnly(2026, 9, 25), "USD", 125m);
}
