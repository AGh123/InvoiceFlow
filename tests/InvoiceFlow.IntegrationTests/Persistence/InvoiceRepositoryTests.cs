using System.Collections.ObjectModel;
using InvoiceFlow.Application.Invoices;
using InvoiceFlow.Application.Invoices.Abstractions;
using InvoiceFlow.Application.Invoices.Exceptions;
using InvoiceFlow.Application.Invoices.Requests;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Infrastructure;
using InvoiceFlow.Infrastructure.Invoices;
using InvoiceFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.IntegrationTests.Persistence;

public sealed class InvoiceRepositoryTests
{
    [Fact]
    public async Task GeneratedNumberSequence_PersistsAcrossServiceProviderRestart()
    {
        await using var database = await TestDatabase.CreateAsync();
        var firstService = new InvoiceService(database.Repository);
        var first = await firstService.CreateInvoiceAsync(new CreateInvoiceRequest(
            null, "Acme", new DateOnly(2026, 9, 26), "USD", []));
        var custom = await firstService.CreateInvoiceAsync(new CreateInvoiceRequest(
            "EXT-100", "Globex", new DateOnly(2026, 9, 26), "CHF", []));

        var services = new ServiceCollection();
        services.AddInfrastructure(database.DatabasePath);
        await using var restarted = services.BuildServiceProvider();
        await restarted.ApplyDatabaseMigrationsAsync();
        var secondService = new InvoiceService(restarted.GetRequiredService<IInvoiceRepository>());
        var second = await secondService.CreateInvoiceAsync(new CreateInvoiceRequest(
            null, "Second", new DateOnly(2026, 9, 26), "EUR", []));

        Assert.Matches(@"^INV-\d{4}-\d{6}$", first.InvoiceNumber);
        Assert.Matches(@"^INV-\d{4}-\d{6}$", second.InvoiceNumber);
        Assert.NotEqual(first.InvoiceNumber, second.InvoiceNumber);
        Assert.Equal(first.InvoiceNumber, (await secondService.GetInvoiceAsync(first.Id))!.InvoiceNumber);
        Assert.Equal("EXT-100", (await secondService.GetInvoiceAsync(custom.Id))!.InvoiceNumber);
    }

    [Fact]
    public async Task ConcurrentSequenceReservations_AreUnique()
    {
        await using var database = await TestDatabase.CreateAsync();

        var reservations = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => database.Repository.ReserveInvoiceSequenceAsync()));

        Assert.Equal(12, reservations.Distinct().Count());
        await using var dbContext = await database.DbContextFactory.CreateDbContextAsync();
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*), MAX(Value) FROM InvoiceNumberSequence";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1L, reader.GetInt64(0));
        Assert.Equal(12L, reader.GetInt64(1));
    }

    [Fact]
    public async Task CompactSequenceMigration_PreservesPreviouslyReservedNumbers()
    {
        await using var database = await TestDatabase.CreateAsync("20260926120000_InvoiceNumberSequence");
        await using (var dbContext = await database.DbContextFactory.CreateDbContextAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO InvoiceNumberSequence DEFAULT VALUES; " +
                "INSERT INTO InvoiceNumberSequence DEFAULT VALUES; " +
                "INSERT INTO InvoiceNumberSequence DEFAULT VALUES;");
            await dbContext.Database.MigrateAsync();
        }

        Assert.Equal(4, await database.Repository.ReserveInvoiceSequenceAsync());
    }
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
    public async Task MaximumAcceptedNumericValues_RoundTripWithinConfiguredPrecision()
    {
        await using var database = await TestDatabase.CreateAsync();
        var invoice = CreateInvoice("INV-NUMERIC-LIMIT");
        invoice.AddLineItem(Guid.NewGuid(), "Maximum values",
            InvoiceRules.MaximumQuantity, InvoiceRules.MaximumUnitPrice, 100m);

        await database.Repository.AddAsync(invoice);
        var loaded = Assert.IsType<Invoice>(await database.Repository.GetByIdAsync(invoice.Id));
        var item = Assert.Single(loaded.LineItems);

        Assert.Equal(InvoiceRules.MaximumQuantity, item.Quantity);
        Assert.Equal(InvoiceRules.MaximumUnitPrice, item.UnitPrice);
        Assert.Equal(item.CalculateGrossAmount(), item.CalculateDiscountAmount());
        Assert.Equal(0m, item.CalculateLineTotal());

        await using var dbContext = await database.DbContextFactory.CreateDbContextAsync();
        var entity = dbContext.Model.FindEntityType(typeof(InvoiceLineItem))!;
        Assert.Equal(18, entity.FindProperty(nameof(InvoiceLineItem.Quantity))!.GetPrecision());
        Assert.Equal(InvoiceRules.QuantityAndUnitPriceScale,
            entity.FindProperty(nameof(InvoiceLineItem.Quantity))!.GetScale());
        Assert.Equal(18, entity.FindProperty(nameof(InvoiceLineItem.UnitPrice))!.GetPrecision());
        Assert.Equal(InvoiceRules.QuantityAndUnitPriceScale,
            entity.FindProperty(nameof(InvoiceLineItem.UnitPrice))!.GetScale());
        Assert.Equal(5, entity.FindProperty(nameof(InvoiceLineItem.DiscountPercent))!.GetPrecision());
        Assert.Equal(InvoiceRules.DiscountPercentScale,
            entity.FindProperty(nameof(InvoiceLineItem.DiscountPercent))!.GetScale());
    }

    [Fact]
    public async Task Add_WithUnrepresentableTotals_DoesNotPersistInvoice()
    {
        await using var database = await TestDatabase.CreateAsync();
        var invoice = CreateInvoice("INV-OVERFLOW");
        for (var index = 0; index < 8; index++)
        {
            invoice.AddLineItem(Guid.NewGuid(), "Maximum",
                InvoiceRules.MaximumQuantity, InvoiceRules.MaximumUnitPrice, 0m);
        }

        await Assert.ThrowsAsync<ArgumentException>(() => database.Repository.AddAsync(invoice));

        Assert.Null(await database.Repository.GetByIdAsync(invoice.Id));
    }

    [Fact]
    public async Task Update_WithUnrepresentableTotals_DoesNotSaveEarlierMutations()
    {
        await using var database = await TestDatabase.CreateAsync();
        var invoice = CreateInvoice("INV-OVERFLOW-UPDATE");
        await database.Repository.AddAsync(invoice);

        await Assert.ThrowsAsync<ArgumentException>(() => database.Repository.UpdateAsync(invoice.Id, tracked =>
        {
            tracked.UpdateDetails("Changed", tracked.IssueDate, "EUR");
            for (var index = 0; index < 8; index++)
            {
                tracked.AddLineItem(Guid.NewGuid(), "Maximum",
                    InvoiceRules.MaximumQuantity, InvoiceRules.MaximumUnitPrice, 0m);
            }
        }));

        var persisted = Assert.IsType<Invoice>(await database.Repository.GetByIdAsync(invoice.Id));
        Assert.Equal("Acme Ltd", persisted.CustomerName);
        Assert.Equal("USD", persisted.CurrencyCode);
        Assert.Empty(persisted.LineItems);
    }

    [Fact]
    public async Task CustomerSort_UsesIdToBreakCaseInsensitiveInvoiceNumberTies()
    {
        await using var database = await TestDatabase.CreateAsync();
        var date = new DateOnly(2026, 9, 26);
        var higherId = new Invoice(Guid.Parse("00000000-0000-0000-0000-000000000002"),
            "inv-case", "Acme", date, "USD");
        var lowerId = new Invoice(Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "INV-CASE", "Acme", date, "USD");
        await database.Repository.AddAsync(higherId);
        await database.Repository.AddAsync(lowerId);

        var firstPage = await database.Repository.GetPageAsync(
            new InvoiceListQuery(string.Empty, InvoiceSortOption.Customer, 1, 1));
        var secondPage = await database.Repository.GetPageAsync(
            new InvoiceListQuery(string.Empty, InvoiceSortOption.Customer, 2, 1));

        Assert.Equal(lowerId.Id, Assert.Single(firstPage.Invoices).Id);
        Assert.Equal(higherId.Id, Assert.Single(secondPage.Invoices).Id);
    }

    [Fact]
    public async Task GetPage_ReturnsSummariesInDeterministicOrder()
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

        var result = await database.Repository.GetPageAsync(
            new InvoiceListQuery(string.Empty, InvoiceSortOption.Newest, 1, 10));

        Assert.Equal(
            ["INV-001", "INV-002", "INV-003"],
            result.Invoices.Select(invoice => invoice.InvoiceNumber));
        Assert.Equal([30m, 20m, 10m], result.Invoices.Select(invoice => invoice.GrandTotal));
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.FilteredCount);
        Assert.Equal(new DateOnly(2025, 1, 1), result.LatestIssueDate);
    }

    [Fact]
    public async Task GetPage_FiltersAndPagesHeadersWhileCalculatingVisibleTotals()
    {
        await using var database = await TestDatabase.CreateAsync();
        for (var number = 1; number <= 12; number++)
        {
            var invoice = new Invoice(Guid.NewGuid(), $"INV-{number:000}",
                number == 1 ? "Other" : "Acme", new DateOnly(2026, 1, number), "USD");
            invoice.AddLineItem(Guid.NewGuid(), "Services", 2m, number, 25m);
            await database.Repository.AddAsync(invoice);
        }

        var result = await database.Repository.GetPageAsync(
            new InvoiceListQuery("  aCmE  ", InvoiceSortOption.Oldest, 2, 5));

        Assert.Equal(12, result.TotalCount);
        Assert.Equal(11, result.FilteredCount);
        Assert.Equal(new DateOnly(2026, 1, 12), result.LatestIssueDate);
        Assert.Equal(2, result.Page);
        Assert.Equal(["INV-007", "INV-008", "INV-009", "INV-010", "INV-011"],
            result.Invoices.Select(invoice => invoice.InvoiceNumber));
        Assert.Equal([10.5m, 12m, 13.5m, 15m, 16.5m],
            result.Invoices.Select(invoice => invoice.GrandTotal));
    }

    [Fact]
    public async Task GetPage_EscapesSearchWildcardsAndClampsAfterDeletion()
    {
        await using var database = await TestDatabase.CreateAsync();
        var literal = new Invoice(Guid.NewGuid(), "INV-100", "A%_Co",
            new DateOnly(2026, 1, 1), "USD");
        await database.Repository.AddAsync(literal);
        await database.Repository.AddAsync(CreateInvoice("INV-200"));

        var matching = await database.Repository.GetPageAsync(
            new InvoiceListQuery("%_", InvoiceSortOption.Newest, 99, 1));
        Assert.Equal(2, matching.TotalCount);
        Assert.Equal(1, matching.FilteredCount);
        Assert.Equal(1, matching.Page);
        Assert.Equal(literal.Id, Assert.Single(matching.Invoices).Id);

        await database.Repository.DeleteAsync(literal.Id);
        var empty = await database.Repository.GetPageAsync(
            new InvoiceListQuery("%_", InvoiceSortOption.Newest, 99, 1));
        Assert.Equal(1, empty.TotalCount);
        Assert.Equal(0, empty.FilteredCount);
        Assert.Empty(empty.Invoices);
    }

    [Fact]
    public async Task Update_SynchronizesTrackedHeaderAndLineItems()
    {
        await using var database = await TestDatabase.CreateAsync();
        var invoice = CreateInvoice("INV-BEFORE", new DateOnly(2025, 1, 1));
        var retainedId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        invoice.AddLineItem(retainedId, "Consulting", 1m, 100m, 0m);
        invoice.AddLineItem(removedId, "Support", 2m, 50m, 5m);
        await database.Repository.AddAsync(invoice);

        var addedId = Guid.NewGuid();
        Assert.True(await database.Repository.UpdateAsync(invoice.Id, tracked =>
        {
            tracked.UpdateDetails("Globex Corp", new DateOnly(2026, 3, 15), "gbp");
            tracked.LineItems.Single(item => item.Id == retainedId)
                .UpdateDetails("Premium consulting", 2.5m, 125.75m, 10.5m);
            Assert.True(tracked.RemoveLineItem(removedId));
            tracked.AddLineItem(addedId, "Training", 3m, 80m, 2m);
        }));
        var reloaded = Assert.IsType<Invoice>(
            await database.Repository.GetByIdAsync(invoice.Id));

        Assert.Equal(invoice.Id, reloaded.Id);
        Assert.Equal("INV-BEFORE", reloaded.InvoiceNumber);
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
    public async Task Update_WithInvalidLineItem_DoesNotSaveEarlierChanges()
    {
        await using var database = await TestDatabase.CreateAsync();
        var invoice = CreateInvoice("INV-ATOMIC");
        await database.Repository.AddAsync(invoice);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            database.Repository.UpdateAsync(invoice.Id, tracked =>
            {
                tracked.UpdateDetails("Changed", tracked.IssueDate, "EUR");
                tracked.AddLineItem(Guid.NewGuid(), "Invalid", 0m, 1m, 0m);
            }));

        var reloaded = Assert.IsType<Invoice>(await database.Repository.GetByIdAsync(invoice.Id));
        Assert.Equal("Acme Ltd", reloaded.CustomerName);
        Assert.Equal("USD", reloaded.CurrencyCode);
        Assert.Empty(reloaded.LineItems);
    }

    [Fact]
    public async Task Delete_RemovesInvoiceAndCascadeDeletesLineItems()
    {
        await using var database = await TestDatabase.CreateAsync();
        var invoice = CreateInvoice("INV-DELETE");
        invoice.AddLineItem(Guid.NewGuid(), "Consulting", 1m, 100m, 0m);
        invoice.AddLineItem(Guid.NewGuid(), "Support", 1m, 50m, 0m);
        await database.Repository.AddAsync(invoice);
        Assert.True(await database.Repository.DeleteAsync(invoice.Id));

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
        var invoices = await database.Repository.GetPageAsync(
            new InvoiceListQuery(string.Empty, InvoiceSortOption.Newest, 1, 10));
        var persistedInvoice = Assert.Single(invoices.Invoices);
        Assert.Equal(storedInvoice.Id, persistedInvoice.Id);
        Assert.Equal("INV-UNIQUE", persistedInvoice.InvoiceNumber);
    }

    [Fact]
    public async Task Update_CannotChangeInvoiceNumber()
    {
        await using var database = await TestDatabase.CreateAsync();
        var firstInvoice = CreateInvoice("INV-FIRST");
        var secondInvoice = CreateInvoice("INV-SECOND");
        await database.Repository.AddAsync(firstInvoice);
        await database.Repository.AddAsync(secondInvoice);

        Assert.True(await database.Repository.UpdateAsync(secondInvoice.Id, tracked =>
            tracked.UpdateDetails("Changed customer", new DateOnly(2026, 9, 25), "EUR")));
        var result = await database.Repository.GetPageAsync(
            new InvoiceListQuery(string.Empty, InvoiceSortOption.Newest, 1, 10));
        Assert.Equal(2, result.Invoices.Count);
        var persistedFirst = Assert.Single(result.Invoices, invoice => invoice.Id == firstInvoice.Id);
        var persistedSecond = Assert.Single(result.Invoices, invoice => invoice.Id == secondInvoice.Id);
        Assert.Equal("INV-FIRST", persistedFirst.InvoiceNumber);
        Assert.Equal("INV-SECOND", persistedSecond.InvoiceNumber);
        Assert.Equal("Changed customer", persistedSecond.CustomerName);
        Assert.Equal("EUR", persistedSecond.CurrencyCode);
    }

    [Fact]
    public async Task RepositoryOperations_CreateAndDisposeSeparateDbContexts()
    {
        await using var database = await TestDatabase.CreateAsync();
        var countingFactory = new CountingDbContextFactory(database.DbContextFactory);
        var repository = new InvoiceRepository(countingFactory);
        var invoice = CreateInvoice("INV-CONTEXTS");

        await repository.AddAsync(invoice);
        _ = Assert.IsType<Invoice>(await repository.GetByIdAsync(invoice.Id));
        await repository.GetPageAsync(new InvoiceListQuery(string.Empty, InvoiceSortOption.Newest, 1, 10));
        Assert.True(await repository.UpdateAsync(invoice.Id, tracked =>
            tracked.UpdateDetails("Updated", tracked.IssueDate, tracked.CurrencyCode)));
        await repository.DeleteAsync(invoice.Id);

        Assert.Equal(5, countingFactory.CreatedContexts.Count);
        Assert.Equal(5, countingFactory.CreatedContexts.Distinct().Count());
        Assert.All(
            countingFactory.CreatedContexts,
            context => Assert.Throws<ObjectDisposedException>(() => context.ChangeTracker.Clear()));
    }

    [Fact]
    public async Task UpdateAndDelete_WithMissingInvoice_ReturnFalse()
    {
        await using var database = await TestDatabase.CreateAsync();
        var missing = CreateInvoice("INV-MISSING");

        Assert.False(await database.Repository.UpdateAsync(missing.Id, _ =>
            throw new InvalidOperationException("Should not run")));
        Assert.False(await database.Repository.DeleteAsync(missing.Id));
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
