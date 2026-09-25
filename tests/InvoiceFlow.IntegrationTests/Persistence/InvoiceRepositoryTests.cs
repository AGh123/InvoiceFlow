using System.Collections.ObjectModel;
using InvoiceFlow.Application.Invoices.Exceptions;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Infrastructure.Invoices;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.IntegrationTests.Persistence;

public sealed class InvoiceRepositoryTests
{
    [Fact]
    public async Task Migrations_CreateUsableDatabaseFromCleanState()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var dbContext = await database.DbContextFactory.CreateDbContextAsync();

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();

        Assert.True(File.Exists(database.DatabasePath));
        Assert.Contains(
            appliedMigrations,
            migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
        Assert.True(await dbContext.Database.CanConnectAsync());
        Assert.Empty(await dbContext.Invoices.ToArrayAsync());
        Assert.Empty(await dbContext.InvoiceLineItems.ToArrayAsync());
    }

    [Fact]
    public async Task AddAndGetById_RoundTripCompleteAggregate()
    {
        await using var database = await TestDatabase.CreateAsync();
        var invoice = CreateInvoice(
            "INV-ROUNDTRIP",
            new DateOnly(2025, 2, 28),
            "eur");
        var lineItemId = Guid.NewGuid();
        invoice.AddLineItem(
            lineItemId,
            "Architecture services",
            1.2345m,
            1234.5678m,
            12.34m);

        await database.Repository.AddAsync(invoice);
        var loaded = await database.Repository.GetByIdAsync(invoice.Id);

        Assert.NotNull(loaded);
        Assert.NotSame(invoice, loaded);
        Assert.Equal(invoice.Id, loaded.Id);
        Assert.Equal("INV-ROUNDTRIP", loaded.InvoiceNumber);
        Assert.Equal(new DateOnly(2025, 2, 28), loaded.IssueDate);
        Assert.Equal("EUR", loaded.CurrencyCode);

        var loadedLineItem = Assert.Single(loaded.LineItems);
        Assert.Equal(lineItemId, loadedLineItem.Id);
        Assert.Equal(invoice.Id, loadedLineItem.InvoiceId);
        Assert.Equal(1.2345m, loadedLineItem.Quantity);
        Assert.Equal(1234.5678m, loadedLineItem.UnitPrice);
        Assert.Equal(12.34m, loadedLineItem.DiscountPercent);
        Assert.IsType<ReadOnlyCollection<InvoiceLineItem>>(loaded.LineItems);
        Assert.True(Assert.IsAssignableFrom<ICollection<InvoiceLineItem>>(
            loaded.LineItems).IsReadOnly);
    }

    [Fact]
    public async Task GetAll_ReturnsAggregatesInDeterministicOrder()
    {
        await using var database = await TestDatabase.CreateAsync();
        var oldest = CreateInvoice("INV-003", new DateOnly(2024, 12, 31));
        var newestSecond = CreateInvoice("INV-002", new DateOnly(2025, 1, 1));
        var newestFirst = CreateInvoice("INV-001", new DateOnly(2025, 1, 1));
        oldest.AddLineItem(Guid.NewGuid(), "Old", 1m, 10m, 0m);
        newestSecond.AddLineItem(Guid.NewGuid(), "Second", 1m, 20m, 0m);
        newestFirst.AddLineItem(Guid.NewGuid(), "First", 1m, 30m, 0m);

        await database.Repository.AddAsync(oldest);
        await database.Repository.AddAsync(newestSecond);
        await database.Repository.AddAsync(newestFirst);

        var invoices = await database.Repository.GetAllAsync();

        Assert.Equal(
            ["INV-001", "INV-002", "INV-003"],
            invoices.Select(invoice => invoice.InvoiceNumber));
        Assert.All(invoices, invoice => Assert.Single(invoice.LineItems));
    }

    [Fact]
    public async Task Update_SynchronizesDetachedHeaderAndLineItems()
    {
        await using var database = await TestDatabase.CreateAsync();
        var invoice = CreateInvoice("INV-BEFORE", new DateOnly(2025, 1, 1));
        var retainedId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        invoice.AddLineItem(retainedId, "Consulting", 1m, 100m, 0m);
        invoice.AddLineItem(removedId, "Support", 2m, 50m, 5m);
        await database.Repository.AddAsync(invoice);

        var detached = Assert.IsType<Invoice>(
            await database.Repository.GetByIdAsync(invoice.Id));
        detached.UpdateDetails(
            "INV-AFTER",
            "Globex Corp",
            new DateOnly(2026, 3, 15),
            "gbp");
        detached.LineItems.Single(item => item.Id == retainedId)
            .UpdateDetails("Premium consulting", 2.5m, 125.75m, 10.5m);
        Assert.True(detached.RemoveLineItem(removedId));
        var addedId = Guid.NewGuid();
        detached.AddLineItem(addedId, "Training", 3m, 80m, 2m);

        await database.Repository.UpdateAsync(detached);
        var reloaded = Assert.IsType<Invoice>(
            await database.Repository.GetByIdAsync(invoice.Id));

        Assert.Equal(invoice.Id, reloaded.Id);
        Assert.Equal("INV-AFTER", reloaded.InvoiceNumber);
        Assert.Equal("Globex Corp", reloaded.CustomerName);
        Assert.Equal(new DateOnly(2026, 3, 15), reloaded.IssueDate);
        Assert.Equal("GBP", reloaded.CurrencyCode);
        Assert.Equal(2, reloaded.LineItems.Count);

        var retained = Assert.Single(reloaded.LineItems, item => item.Id == retainedId);
        Assert.Equal(retainedId, retained.Id);
        Assert.Equal("Premium consulting", retained.Description);
        Assert.Equal(2.5m, retained.Quantity);
        Assert.Equal(125.75m, retained.UnitPrice);
        Assert.Equal(10.5m, retained.DiscountPercent);
        Assert.DoesNotContain(reloaded.LineItems, item => item.Id == removedId);

        var added = Assert.Single(reloaded.LineItems, item => item.Id == addedId);
        Assert.Equal(invoice.Id, added.InvoiceId);
        Assert.Equal("Training", added.Description);
    }

    [Fact]
    public async Task Delete_RemovesInvoiceAndCascadeDeletesLineItems()
    {
        await using var database = await TestDatabase.CreateAsync();
        var invoice = CreateInvoice("INV-DELETE");
        invoice.AddLineItem(Guid.NewGuid(), "Consulting", 1m, 100m, 0m);
        invoice.AddLineItem(Guid.NewGuid(), "Support", 1m, 50m, 0m);
        await database.Repository.AddAsync(invoice);
        var detached = Assert.IsType<Invoice>(
            await database.Repository.GetByIdAsync(invoice.Id));

        await database.Repository.DeleteAsync(detached);

        Assert.Null(await database.Repository.GetByIdAsync(invoice.Id));
        await using var dbContext = await database.DbContextFactory.CreateDbContextAsync();
        Assert.Empty(await dbContext.Invoices.ToArrayAsync());
        Assert.Empty(await dbContext.InvoiceLineItems.ToArrayAsync());
    }

    [Fact]
    public async Task Add_WithDuplicateInvoiceNumber_ThrowsApplicationException()
    {
        await using var database = await TestDatabase.CreateAsync();
        var storedInvoice = CreateInvoice("INV-UNIQUE");
        await database.Repository.AddAsync(storedInvoice);

        var exception = await Assert.ThrowsAsync<DuplicateInvoiceNumberException>(() =>
            database.Repository.AddAsync(CreateInvoice("INV-UNIQUE")));

        Assert.Equal("INV-UNIQUE", exception.InvoiceNumber);
        var invoices = await database.Repository.GetAllAsync();
        var persistedInvoice = Assert.Single(invoices);
        Assert.Equal(storedInvoice.Id, persistedInvoice.Id);
        Assert.Equal("INV-UNIQUE", persistedInvoice.InvoiceNumber);
    }

    [Fact]
    public async Task Update_WithDuplicateInvoiceNumber_ThrowsApplicationException()
    {
        await using var database = await TestDatabase.CreateAsync();
        var firstInvoice = CreateInvoice("INV-FIRST");
        var secondInvoice = CreateInvoice("INV-SECOND");
        await database.Repository.AddAsync(firstInvoice);
        await database.Repository.AddAsync(secondInvoice);

        var duplicate = Assert.IsType<Invoice>(
            await database.Repository.GetByIdAsync(secondInvoice.Id));
        duplicate.UpdateDetails(
            firstInvoice.InvoiceNumber,
            "Changed customer",
            new DateOnly(2026, 9, 25),
            "EUR");

        var exception = await Assert.ThrowsAsync<DuplicateInvoiceNumberException>(() =>
            database.Repository.UpdateAsync(duplicate));

        Assert.Equal("INV-FIRST", exception.InvoiceNumber);
        var invoices = await database.Repository.GetAllAsync();
        Assert.Equal(2, invoices.Count);
        var persistedFirst = Assert.Single(invoices, invoice => invoice.Id == firstInvoice.Id);
        var persistedSecond = Assert.Single(invoices, invoice => invoice.Id == secondInvoice.Id);
        Assert.Equal("INV-FIRST", persistedFirst.InvoiceNumber);
        Assert.Equal("INV-SECOND", persistedSecond.InvoiceNumber);
        Assert.Equal("Acme Ltd", persistedSecond.CustomerName);
        Assert.Equal("USD", persistedSecond.CurrencyCode);
    }

    [Fact]
    public async Task RepositoryOperations_CreateAndDisposeSeparateDbContexts()
    {
        await using var database = await TestDatabase.CreateAsync();
        var countingFactory = new CountingDbContextFactory(database.DbContextFactory);
        var repository = new InvoiceRepository(countingFactory);
        var invoice = CreateInvoice("INV-CONTEXTS");

        await repository.AddAsync(invoice);
        var loaded = Assert.IsType<Invoice>(await repository.GetByIdAsync(invoice.Id));
        await repository.GetAllAsync();
        await repository.DeleteAsync(loaded);

        Assert.Equal(4, countingFactory.CreatedContexts.Count);
        Assert.Equal(4, countingFactory.CreatedContexts.Distinct().Count());
        Assert.All(
            countingFactory.CreatedContexts,
            context => Assert.Throws<ObjectDisposedException>(() => context.ChangeTracker.Clear()));
    }

    [Fact]
    public async Task UpdateAndDelete_WithMissingInvoice_FailClearly()
    {
        await using var database = await TestDatabase.CreateAsync();
        var missing = CreateInvoice("INV-MISSING");

        var updateException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            database.Repository.UpdateAsync(missing));
        var deleteException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            database.Repository.DeleteAsync(missing));

        Assert.Contains("not stored", updateException.Message);
        Assert.Contains("not stored", deleteException.Message);
    }

    private static Invoice CreateInvoice(
        string invoiceNumber,
        DateOnly? issueDate = null,
        string currencyCode = "USD") =>
        new(
            Guid.NewGuid(),
            invoiceNumber,
            "Acme Ltd",
            issueDate ?? new DateOnly(2025, 1, 1),
            currencyCode);
}
