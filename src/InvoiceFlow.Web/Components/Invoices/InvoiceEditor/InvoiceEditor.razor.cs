using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Web.Components.Invoices.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace InvoiceFlow.Web.Components.Invoices;

public partial class InvoiceEditor
{
    private const string AggregateTotalError =
        "Invoice totals are too large to calculate. Reduce the line item amounts.";

    private InvoiceEditorModel? _boundModel;
    private EditContext _editContext = null!;
    private ValidationMessageStore _serverMessages = null!;
    private ValidationMessageStore _aggregateMessages = null!;
    private InvoiceDeleteDialog? _deleteDialog;
    private ElementReference _validationAlert;
    private int _validationErrorCount;
    private bool _focusValidationAlert;
    private bool _hasDuplicateInvoiceNumberError;
    private bool _hasAggregateError;
    private bool _validationAttempted;
    private bool _notifyingValidation;

    [Parameter, EditorRequired]
    public InvoiceEditorModel Model { get; set; } = null!;

    [Parameter]
    public Guid? InvoiceId { get; set; }

    [Parameter]
    public bool IsSaving { get; set; }

    [Parameter]
    public string? ErrorMessage { get; set; }

    [Parameter]
    public EventCallback OnSave { get; set; }

    [Parameter]
    public EventCallback OnChanged { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    [Parameter]
    public EventCallback OnRetry { get; set; }

    [Parameter]
    public EventCallback<InvoiceSummaryDto> OnDelete { get; set; }

    private bool IsEditMode => InvoiceId.HasValue;

    private bool IsSaveDisabled =>
        IsSaving || _hasAggregateError || (_validationAttempted && _validationErrorCount > 0);

    private bool HasError(string fieldName) =>
        _editContext.GetValidationMessages(new FieldIdentifier(Model, fieldName)).Any();

    private string ValidationAlertText =>
        $"{_validationErrorCount} {(_validationErrorCount == 1 ? "error was" : "errors were")} found — " +
        "Please fix the highlighted fields before saving.";

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_boundModel, Model))
        {
            ResetEditContext();
        }
    }

    public void SetInvoiceNumberError(string message)
    {
        var field = new FieldIdentifier(Model, nameof(Model.InvoiceNumber));
        _serverMessages.Clear(field);
        _serverMessages.Add(field, message);
        _hasDuplicateInvoiceNumberError = true;
        _validationAttempted = true;
        _focusValidationAlert = true;
        UpdateValidationState();
    }

    public async Task SubmitAsync()
    {
        await ValidateAndSaveAsync();
    }

    private Task HandleSubmitAsync(EditContext editContext) => ValidateAndSaveAsync();

    private async Task ValidateAndSaveAsync()
    {
        if (IsSaving)
        {
            return;
        }

        ClearServerMessages();
        _editContext.Validate();
        NotifyAllEditableFieldsChanged();
        RefreshAggregateError();

        if (!_editContext.GetValidationMessages().Any())
        {
            _validationAttempted = false;
            _validationErrorCount = 0;
            await OnSave.InvokeAsync();
            return;
        }

        _validationAttempted = true;
        _focusValidationAlert = true;
        UpdateValidationState();
    }

    private void NotifyAllEditableFieldsChanged(bool includeInvoiceNumber = true)
    {
        _notifyingValidation = true;
        try
        {
            if (includeInvoiceNumber)
            {
                _editContext.NotifyFieldChanged(new FieldIdentifier(Model, nameof(Model.InvoiceNumber)));
            }

            _editContext.NotifyFieldChanged(new FieldIdentifier(Model, nameof(Model.CustomerName)));
            _editContext.NotifyFieldChanged(new FieldIdentifier(Model, nameof(Model.IssueDate)));
            _editContext.NotifyFieldChanged(new FieldIdentifier(Model, nameof(Model.CurrencyCode)));

            foreach (var lineItem in Model.LineItems)
            {
                _editContext.NotifyFieldChanged(new FieldIdentifier(lineItem, nameof(lineItem.Description)));
                _editContext.NotifyFieldChanged(new FieldIdentifier(lineItem, nameof(lineItem.Quantity)));
                _editContext.NotifyFieldChanged(new FieldIdentifier(lineItem, nameof(lineItem.UnitPrice)));
                _editContext.NotifyFieldChanged(new FieldIdentifier(lineItem, nameof(lineItem.DiscountPercent)));
            }
        }
        finally
        {
            _notifyingValidation = false;
        }
    }

    private void HandleStructureChanged()
    {
        _ = OnChanged.InvokeAsync();
        RefreshAggregateError();
        if (_validationAttempted)
        {
            _editContext.Validate();
            NotifyAllEditableFieldsChanged(includeInvoiceNumber: false);
            UpdateValidationState();
            return;
        }

        UpdateValidationState();
    }

    private async Task RetrySaveAsync()
    {
        if (OnRetry.HasDelegate)
        {
            await OnRetry.InvokeAsync();
            return;
        }

        await SubmitAsync();
    }

    private Task ShowDeleteDialogAsync()
    {
        if (!InvoiceId.HasValue || _deleteDialog is null)
        {
            return Task.CompletedTask;
        }

        var invoice = new InvoiceSummaryDto(
            InvoiceId.Value,
            Model.InvoiceNumber,
            Model.CustomerName,
            Model.IssueDate,
            Model.CurrencyCode,
            Model.TryCalculateTotals(out var totals) ? totals.GrandTotal : 0m);

        return _deleteDialog.ShowAsync(invoice);
    }

    private Task HandleInvoiceDeletedAsync(InvoiceSummaryDto invoice) =>
        OnDelete.InvokeAsync(invoice);

    private void ResetEditContext()
    {
        if (_editContext is not null)
        {
            _editContext.OnFieldChanged -= HandleFieldChanged;
            _editContext.OnValidationStateChanged -= HandleValidationStateChanged;
        }

        _boundModel = Model;
        _editContext = new EditContext(Model);
        _serverMessages = new ValidationMessageStore(_editContext);
        _aggregateMessages = new ValidationMessageStore(_editContext);
        _editContext.OnFieldChanged += HandleFieldChanged;
        _editContext.OnValidationStateChanged += HandleValidationStateChanged;
        _validationErrorCount = 0;
        _validationAttempted = false;
        _hasDuplicateInvoiceNumberError = false;
        _hasAggregateError = false;
        RefreshAggregateError();
    }

    private void HandleFieldChanged(object? sender, FieldChangedEventArgs args)
    {
        if (!_notifyingValidation)
        {
            _ = OnChanged.InvokeAsync();
            if (args.FieldIdentifier.Model is InvoiceLineItemEditorModel &&
                (args.FieldIdentifier.FieldName is nameof(InvoiceLineItemEditorModel.Quantity)
                    or nameof(InvoiceLineItemEditorModel.UnitPrice)
                    or nameof(InvoiceLineItemEditorModel.DiscountPercent)) &&
                RefreshAggregateError())
            {
                _editContext.NotifyValidationStateChanged();
            }
        }
        if (ReferenceEquals(args.FieldIdentifier.Model, Model) &&
            args.FieldIdentifier.FieldName == nameof(Model.InvoiceNumber) &&
            _hasDuplicateInvoiceNumberError)
        {
            ClearDuplicateInvoiceNumberError();
        }

        _validationErrorCount = _editContext.GetValidationMessages().Count();
        InvokeAsync(StateHasChanged);
    }

    private void HandleInvoiceNumberInput(ChangeEventArgs args)
    {
        Model.InvoiceNumber = args.Value?.ToString() ?? string.Empty;
        ClearDuplicateInvoiceNumberError();
        _editContext.NotifyFieldChanged(new FieldIdentifier(Model, nameof(Model.InvoiceNumber)));
    }

    private void HandleCustomerInput(ChangeEventArgs args)
    {
        Model.CustomerName = args.Value?.ToString() ?? string.Empty;
        _editContext.NotifyFieldChanged(new FieldIdentifier(Model, nameof(Model.CustomerName)));
    }

    private Task ChangeDate(DateOnly value)
    {
        Model.IssueDate = value;
        _editContext.NotifyFieldChanged(new FieldIdentifier(Model, nameof(Model.IssueDate)));
        return Task.CompletedTask;
    }

    private Task ChangeCurrency(string value)
    {
        Model.CurrencyCode = value;
        _editContext.NotifyFieldChanged(new FieldIdentifier(Model, nameof(Model.CurrencyCode)));
        return Task.CompletedTask;
    }

    private void ClearDuplicateInvoiceNumberError()
    {
        if (!_hasDuplicateInvoiceNumberError)
        {
            return;
        }

        var field = new FieldIdentifier(Model, nameof(Model.InvoiceNumber));
        _serverMessages.Clear(field);
        _hasDuplicateInvoiceNumberError = false;
        _validationErrorCount = _editContext.GetValidationMessages().Count();
        _editContext.NotifyValidationStateChanged();
    }

    private void HandleValidationStateChanged(object? sender, ValidationStateChangedEventArgs args)
    {
        _validationErrorCount = _editContext.GetValidationMessages().Count();
        InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusValidationAlert)
        {
            _focusValidationAlert = false;
            await _validationAlert.FocusAsync();
        }
    }

    private void ClearServerMessages()
    {
        _serverMessages.Clear();
        _hasDuplicateInvoiceNumberError = false;
        _editContext.NotifyValidationStateChanged();
    }

    private bool RefreshAggregateError()
    {
        var hasError = !Model.TryCalculateTotals(out _);
        if (_hasAggregateError == hasError)
        {
            return false;
        }

        var field = new FieldIdentifier(Model, nameof(Model.LineItems));
        _aggregateMessages.Clear(field);
        if (hasError)
        {
            _aggregateMessages.Add(field, AggregateTotalError);
        }

        _hasAggregateError = hasError;
        return true;
    }

    private void UpdateValidationState()
    {
        _validationErrorCount = _editContext.GetValidationMessages().Count();
        _editContext.NotifyValidationStateChanged();
        StateHasChanged();
    }

    public void Dispose()
    {
        if (_editContext is not null)
        {
            _editContext.OnFieldChanged -= HandleFieldChanged;
            _editContext.OnValidationStateChanged -= HandleValidationStateChanged;
        }
    }
}
