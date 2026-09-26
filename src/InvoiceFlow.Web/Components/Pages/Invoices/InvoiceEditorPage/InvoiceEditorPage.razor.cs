using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Web.Components.Invoices;
using InvoiceFlow.Web.Components.Invoices.Models;
using InvoiceFlow.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace InvoiceFlow.Web.Components.Pages.Invoices;

public partial class InvoiceEditorPage
{
    private const string DuplicateInvoiceNumberMessage =
        "An invoice with this number already exists.";

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private InvoiceEditor? _editor;
    private UnsavedChangesDialog? _discardDialog;
    private InvoiceEditorModel? _model;
    private bool _isDirty;
    private string? _pendingNavigation;
    private Guid? _loadedRouteId;
    private bool _routeInitialized;
    private bool _isLoading;
    private bool _isSaving;
    private bool _notFound;
    private bool _loadError;
    private string? _saveError;

    [Parameter]
    public Guid? Id { get; set; }

    private string PageTitleText => (_notFound, _loadError, Id.HasValue) switch
    {
        (true, _, _) => "Invoice Not Found",
        (_, true, _) => "Invoice Load Error",
        (_, _, true) => "Edit Invoice",
        _ => "New Invoice",
    };

    private bool IsDirty => _isDirty;

    protected override async Task OnParametersSetAsync()
    {
        if (_routeInitialized && _loadedRouteId == Id)
        {
            return;
        }

        _routeInitialized = true;
        _loadedRouteId = Id;
        await LoadEditorAsync();
    }

    private async Task LoadEditorAsync()
    {
        _pendingNavigation = null;
        _isDirty = false;
        _notFound = false;
        _loadError = false;
        _saveError = null;

        if (!Id.HasValue)
        {
            _isLoading = true;
            try
            {
                var generatedNumber = await InvoiceService.ReserveInvoiceNumberAsync(
                    _cancellationTokenSource.Token);
                _model = new InvoiceEditorModel
                {
                    InvoiceNumber = generatedNumber,
                    GeneratedInvoiceNumber = generatedNumber,
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Logger.LogError(exception, "Failed to prepare a new invoice.");
                _model = null;
                _loadError = true;
            }
            catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }
            finally
            {
                _isLoading = false;
            }
            return;
        }

        _isLoading = true;

        try
        {
            var invoice = await InvoiceService.GetInvoiceAsync(
                Id.Value,
                _cancellationTokenSource.Token);

            if (invoice is null)
            {
                _model = null;
                _notFound = true;
                return;
            }

            _model = InvoiceEditorModel.FromDetails(invoice);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to load invoice {InvoiceId}.", Id);
            _model = null;
            _loadError = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (_model is null || _isSaving)
        {
            return;
        }

        _isSaving = true;
        _saveError = null;
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            string savedInvoiceNumber;

            if (Id.HasValue)
            {
                var updated = await InvoiceService.UpdateInvoiceAsync(
                    Id.Value,
                    _model.ToUpdateRequest(),
                    _cancellationTokenSource.Token);

                if (!updated)
                {
                    _model = null;
                    _notFound = true;
                    return;
                }

                savedInvoiceNumber = _model.InvoiceNumber;
            }
            else
            {
                var savedInvoice = await InvoiceService.CreateInvoiceAsync(
                    _model.ToCreateRequest(),
                    _cancellationTokenSource.Token);
                savedInvoiceNumber = savedInvoice.InvoiceNumber;
            }

            NotificationState.Publish($"Invoice {savedInvoiceNumber} saved successfully");
            _isDirty = false;
            NavigationManager.NavigateTo("/invoices");
        }
        catch (InvoiceFlow.Application.Invoices.Exceptions.DuplicateInvoiceNumberException)
        {
            _editor?.SetInvoiceNumberError(DuplicateInvoiceNumberMessage);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to save invoice {InvoiceId}.", Id);
            _saveError = "Your changes are still here. Try again.";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private Task RetrySaveAsync() => _editor?.SubmitAsync() ?? Task.CompletedTask;

    private void Cancel() => NavigationManager.NavigateTo("/invoices");

    private void HandleEditorChanged() => _isDirty = true;

    private async Task ConfirmNavigationAsync(LocationChangingContext context)
    {
        if (!IsDirty)
        {
            return;
        }

        context.PreventNavigation();
        _pendingNavigation = context.TargetLocation;
        if (_discardDialog is not null)
        {
            await _discardDialog.ShowAsync();
        }
    }

    private void DiscardAndNavigate()
    {
        if (_pendingNavigation is null)
        {
            return;
        }

        _isDirty = false;
        NavigationManager.NavigateTo(_pendingNavigation);
    }

    private void HandleDeleted(InvoiceSummaryDto invoice)
    {
        _isDirty = false;
        NotificationState.Publish($"Invoice {invoice.InvoiceNumber} deleted successfully");
        NavigationManager.NavigateTo("/invoices");
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
