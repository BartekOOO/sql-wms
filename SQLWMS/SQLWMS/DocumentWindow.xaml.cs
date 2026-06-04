using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SQLWMS.Models;
using SQLWMS.Services;

namespace SQLWMS
{
    partial class DocumentWindow : Window
    {
        private const string DetachSectorKey = "ODEPNIJ";

        private readonly DocumentCatalogService _documentCatalogService;
        private readonly string _operatorCode;
        private readonly bool _isReadOnlyMode;
        private readonly int _documentId;
        private readonly string _documentType;
        private readonly string _documentStatus;
        private readonly List<SectorLookupItem> _allSectors;
        private readonly ObservableCollection<DocumentPositionItem> _positions;
        private readonly SemaphoreSlim _persistSemaphore = new(1, 1);
        private bool _allowClose;
        private bool _isFinalizing;
        private bool _isInitializing;
        private bool _suppressAutoPersist;
        private bool _pendingSourceSectorDetach;
        private bool _pendingDestinationSectorDetach;
        private DateTime? _persistedDocumentDate;
        private string _persistedDescription = string.Empty;
        private string? _persistedSourceWarehouseValue;
        private string? _persistedDestinationWarehouseValue;
        private string? _persistedSourceSectorValue;
        private string? _persistedDestinationSectorValue;
        private string _lastPersistedStateSignature = string.Empty;

        internal DocumentWindow(
            DocumentCatalogService documentCatalogService,
            string operatorCode,
            DocumentDetailsItem details,
            IReadOnlyList<DocumentPositionItem> positions,
            IReadOnlyList<WarehouseLookupItem> warehouses,
            IReadOnlyList<SectorLookupItem> sectors,
            bool isReadOnlyMode,
            string readOnlyMessage)
        {
            InitializeComponent();

            _isInitializing = true;

            _documentCatalogService = documentCatalogService;
            _operatorCode = operatorCode;
            _isReadOnlyMode = isReadOnlyMode;
            _documentId = details.Id;
            _documentType = details.TypDokumentu.Trim().ToUpperInvariant();
            _documentStatus = details.StatusDokumentu.Trim();
            _allSectors = sectors.ToList();
            _positions = new ObservableCollection<DocumentPositionItem>(positions);
            _persistedDocumentDate = details.DataRealizacji.Date;
            _persistedDescription = NormalizeDescription(details.OpisDokumentu);
            _persistedSourceWarehouseValue = _documentType == "PM"
                ? null
                : NormalizeValue(details.MagazynZrodlowyKod);
            _persistedDestinationWarehouseValue = _documentType == "WM"
                ? null
                : NormalizeValue(details.MagazynDocelowyKod);
            _persistedSourceSectorValue = _documentType == "PM"
                ? null
                : NormalizeValue(details.SektorZrodlowyKod);
            _persistedDestinationSectorValue = _documentType == "WM"
                ? null
                : NormalizeValue(details.SektorDocelowyKod);

            DocumentTitleTextBlock.Text = details.NumerDokumentu;

            NumerDokumentuTextBlock.Text = details.NumerDokumentu;
            TypDokumentuTextBlock.Text = details.TypDokumentu;
            StatusDokumentuTextBlock.Text = details.StatusDokumentu;
            DocumentDateEdit.EditValue = details.DataRealizacji.Date;
            DescriptionTextBox.Text = details.OpisDokumentu;

            SourceWarehouseComboBox.ItemsSource = warehouses;
            DestinationWarehouseComboBox.ItemsSource = warehouses;
            SourceWarehouseComboBox.SelectedValue = NormalizeValue(details.MagazynZrodlowyKod);
            DestinationWarehouseComboBox.SelectedValue = NormalizeValue(details.MagazynDocelowyKod);
            RefreshSourceSectors(NormalizeValue(details.SektorZrodlowyKod));
            RefreshDestinationSectors(NormalizeValue(details.SektorDocelowyKod));

            DocumentPositionsDataGrid.ItemsSource = _positions;
            ApplyEditability();
            UpdateSectorClearButtons();
            _lastPersistedStateSignature = CreatePersistedStateSignature();
            _isInitializing = false;

            if (isReadOnlyMode)
            {
                ReadOnlyBannerBorder.Visibility = Visibility.Visible;
                ReadOnlyBannerTextBlock.Text = string.IsNullOrWhiteSpace(readOnlyMessage)
                    ? "Dokument jest otwarty przez innego uzytkownika. Widok tylko do odczytu."
                    : readOnlyMessage;
            }

            Closing += DocumentWindow_Closing;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isReadOnlyMode)
            {
                CloseDocumentWindow(false);
                return;
            }

            _suppressAutoPersist = true;
            SaveButton.IsEnabled = false;

            try
            {
                bool updateSucceeded = await PersistDocumentChangesAsync(force: true, showErrors: true);
                if (!updateSucceeded)
                {
                    _suppressAutoPersist = false;
                    SaveButton.IsEnabled = true;
                    SetDocumentActionButtonsEnabled(true);
                    return;
                }

                _isFinalizing = true;
                DocumentProcedureResult closeResult = await _documentCatalogService.CloseDocumentAsync(_documentId, _operatorCode);
                if (!closeResult.IsSuccess)
                {
                    _isFinalizing = false;
                    _suppressAutoPersist = false;
                    AppDialogWindow.Show(this, "Zamykanie dokumentu", closeResult.Message, AppDialogKind.Warning);
                    SaveButton.IsEnabled = true;
                    SetDocumentActionButtonsEnabled(true);
                    return;
                }

                CloseDocumentWindow(true);
            }
            catch (Exception ex)
            {
                _isFinalizing = false;
                _suppressAutoPersist = false;
                AppDialogWindow.Show(this, "Edycja dokumentu", ex.Message, AppDialogKind.Error);
                SaveButton.IsEnabled = true;
                SetDocumentActionButtonsEnabled(true);
            }
        }

        private async void DocumentActionButton_Click(object sender, RoutedEventArgs e)
        {
            string? action = (sender as System.Windows.Controls.Button)?.Tag as string;
            if (_isReadOnlyMode || string.IsNullOrWhiteSpace(action))
            {
                return;
            }

            if (!ConfirmDocumentAction(action))
            {
                return;
            }

            _suppressAutoPersist = true;
            SaveButton.IsEnabled = false;
            SetDocumentActionButtonsEnabled(false);

            try
            {
                _isFinalizing = true;
                DocumentProcedureResult actionResult = await _documentCatalogService.CloseDocumentAsync(_documentId, _operatorCode, action);
                if (!actionResult.IsSuccess)
                {
                    _isFinalizing = false;
                    _suppressAutoPersist = false;
                    AppDialogWindow.Show(this, "Zamykanie dokumentu", actionResult.Message, AppDialogKind.Warning);
                    SaveButton.IsEnabled = true;
                    SetDocumentActionButtonsEnabled(true);
                    return;
                }

                CloseDocumentWindow(true);
            }
            catch (Exception ex)
            {
                _isFinalizing = false;
                _suppressAutoPersist = false;
                AppDialogWindow.Show(this, "Zamykanie dokumentu", ex.Message, AppDialogKind.Error);
                SaveButton.IsEnabled = true;
                SetDocumentActionButtonsEnabled(true);
            }
        }

        private async void AddPositionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AddDocumentPositionWindow addPositionWindow = new(_documentCatalogService, _documentId, _operatorCode)
            {
                Owner = this
            };

            bool? result = addPositionWindow.ShowDialog();
            if (result != true)
            {
                return;
            }

            await ReloadPositionsAsync();
        }

        private async void OpenPositionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DocumentPositionsDataGrid.SelectedItem is not DocumentPositionItem selectedPosition)
            {
                return;
            }

            AddDocumentPositionWindow positionWindow = new(_documentCatalogService, _operatorCode, selectedPosition)
            {
                Owner = this
            };

            bool? result = positionWindow.ShowDialog();
            if (result != true)
            {
                return;
            }

            await ReloadPositionsAsync();
        }

        private async void DeletePositionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DocumentPositionsDataGrid.SelectedItem is not DocumentPositionItem selectedPosition)
            {
                return;
            }

            try
            {
                DocumentProcedureResult deleteResult = await _documentCatalogService.DeleteDocumentPositionAsync(selectedPosition.Id, _operatorCode);
                if (!deleteResult.IsSuccess)
                {
                    AppDialogWindow.Show(this, "Usuwanie pozycji", deleteResult.Message, AppDialogKind.Warning);
                    return;
                }

                await ReloadPositionsAsync();
            }
            catch (Exception ex)
            {
                AppDialogWindow.Show(this, "Usuwanie pozycji", ex.Message, AppDialogKind.Error);
            }
        }

        private void DocumentWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
            }
        }

        private void CloseDocumentWindow(bool dialogResult)
        {
            _isFinalizing = true;
            _suppressAutoPersist = true;
            _allowClose = true;
            Closing -= DocumentWindow_Closing;
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    DialogResult = dialogResult;
                }
                catch (InvalidOperationException)
                {
                }

                if (IsVisible)
                {
                    Close();
                }
            }, DispatcherPriority.Normal);
        }

        private void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void DocumentPositionsDataGridRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row)
            {
                row.IsSelected = true;
                row.Focus();
            }
        }

        private void DocumentPositionsDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject) is null)
            {
                DocumentPositionsDataGrid.SelectedItem = null;
            }
        }

        private void DocumentPositionsDataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            bool hasSelection = DocumentPositionsDataGrid.SelectedItem is DocumentPositionItem;
            bool canEdit = !_isReadOnlyMode;
            bool hasPositions = _positions.Count > 0;

            OpenPositionMenuItem.IsEnabled = hasSelection;
            AddPositionMenuItem.IsEnabled = canEdit;
            AddPositionMenuItem.Header = hasPositions ? "Dodaj pozycje" : "Nowa pozycja";
            DeletePositionMenuItem.IsEnabled = canEdit && hasSelection;
        }

        private async void SourceWarehouseComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            bool shouldPersist = CanAutoPersist();
            string? previousSelectedSector = GetSelectedSectorValue(SourceSectorComboBox, pendingDetach: false);
            _pendingSourceSectorDetach = false;

            _suppressAutoPersist = true;
            try
            {
                RefreshSourceSectors(previousSelectedSector);
            }
            finally
            {
                _suppressAutoPersist = false;
            }

            UpdateSectorClearButtons();

            if (shouldPersist)
            {
                await PersistDocumentChangesAsync(force: false, showErrors: true);
            }
        }

        private async void DestinationWarehouseComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            bool shouldPersist = CanAutoPersist();
            string? previousSelectedSector = GetSelectedSectorValue(DestinationSectorComboBox, pendingDetach: false);
            _pendingDestinationSectorDetach = false;

            _suppressAutoPersist = true;
            try
            {
                RefreshDestinationSectors(previousSelectedSector);
            }
            finally
            {
                _suppressAutoPersist = false;
            }

            UpdateSectorClearButtons();

            if (shouldPersist)
            {
                await PersistDocumentChangesAsync(force: false, showErrors: true);
            }
        }

        private async void DocumentField_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!CanAutoPersist())
            {
                return;
            }

            if (ReferenceEquals(sender, SourceSectorComboBox) && SourceSectorComboBox.SelectedItem is not null)
            {
                _pendingSourceSectorDetach = false;
                UpdateSectorClearButtons();
            }

            if (ReferenceEquals(sender, DestinationSectorComboBox) && DestinationSectorComboBox.SelectedItem is not null)
            {
                _pendingDestinationSectorDetach = false;
                UpdateSectorClearButtons();
            }

            await PersistDocumentChangesAsync(force: false, showErrors: true);
        }

        private async void EditableField_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!CanAutoPersist())
            {
                return;
            }

            await PersistDocumentChangesAsync(force: false, showErrors: true);
        }

        private async void SourceSectorClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isReadOnlyMode || SourceWarehouseComboBox.SelectedValue is null)
            {
                return;
            }

            _pendingSourceSectorDetach = true;
            SourceSectorComboBox.SelectedItem = null;
            UpdateSectorClearButtons();
            await PersistDocumentChangesAsync(force: false, showErrors: true);
        }

        private async void DestinationSectorClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isReadOnlyMode || DestinationWarehouseComboBox.SelectedValue is null)
            {
                return;
            }

            _pendingDestinationSectorDetach = true;
            DestinationSectorComboBox.SelectedItem = null;
            UpdateSectorClearButtons();
            await PersistDocumentChangesAsync(force: false, showErrors: true);
        }

        private void ApplyEditability()
        {
            bool canEdit = !_isReadOnlyMode;
            bool canEditSource = canEdit && _documentType != "PM";
            bool canEditDestination = canEdit && _documentType != "WM";

            DocumentDateEdit.IsEnabled = canEdit;
            DescriptionTextBox.IsReadOnly = !canEdit;
            SourceWarehouseComboBox.IsEnabled = canEditSource;
            SourceSectorComboBox.IsEnabled = canEditSource && SourceWarehouseComboBox.SelectedValue is not null;
            SourceSectorClearButton.IsEnabled = canEditSource && SourceSectorComboBox.IsEnabled && SourceSectorComboBox.SelectedItem is not null;
            DestinationWarehouseComboBox.IsEnabled = canEditDestination;
            DestinationSectorComboBox.IsEnabled = canEditDestination && DestinationWarehouseComboBox.SelectedValue is not null;
            DestinationSectorClearButton.IsEnabled = canEditDestination && DestinationSectorComboBox.IsEnabled && DestinationSectorComboBox.SelectedItem is not null;
            ConfigureDocumentActionButtons(canEdit);
            SaveButton.Content = canEdit ? "Zapisz i zamknij" : "Zamknij";
        }

        private void RefreshSourceSectors(string? selectedValue)
        {
            List<SectorSelectionItem> items = BuildSectorOptions(SourceWarehouseComboBox.SelectedValue as string);
            SourceSectorComboBox.ItemsSource = items;
            SourceSectorComboBox.SelectedItem = ResolveSectorSelection(items, selectedValue);
            SourceSectorComboBox.IsEnabled = !_isReadOnlyMode && _documentType != "PM" && SourceWarehouseComboBox.SelectedValue is not null;
            UpdateSectorClearButtons();
        }

        private void RefreshDestinationSectors(string? selectedValue)
        {
            List<SectorSelectionItem> items = BuildSectorOptions(DestinationWarehouseComboBox.SelectedValue as string);
            DestinationSectorComboBox.ItemsSource = items;
            DestinationSectorComboBox.SelectedItem = ResolveSectorSelection(items, selectedValue);
            DestinationSectorComboBox.IsEnabled = !_isReadOnlyMode && _documentType != "WM" && DestinationWarehouseComboBox.SelectedValue is not null;
            UpdateSectorClearButtons();
        }

        private List<SectorSelectionItem> BuildSectorOptions(string? warehouseCode)
        {
            List<SectorSelectionItem> items = [];
            if (string.IsNullOrWhiteSpace(warehouseCode))
            {
                return items;
            }

            items.AddRange(_allSectors
                .Where(item => string.Equals(item.WarehouseCode, warehouseCode, StringComparison.OrdinalIgnoreCase))
                .Select(item => new SectorSelectionItem
                {
                    Value = item.Code,
                    DisplayName = item.DisplayName
                }));

            return items;
        }

        private static SectorSelectionItem? ResolveSectorSelection(IEnumerable<SectorSelectionItem> items, string? selectedValue)
        {
            string? normalizedValue = NormalizeValue(selectedValue);
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                return null;
            }

            return items.FirstOrDefault(item => string.Equals(item.Value, normalizedValue, StringComparison.OrdinalIgnoreCase));
        }

        private DocumentUpdateRequest BuildUpdateRequest()
        {
            DateTime? selectedDate = DocumentDateEdit.EditValue is DateTime value ? value : null;
            if (!selectedDate.HasValue)
            {
                throw new InvalidOperationException("Wybierz date dokumentu.");
            }

            return new DocumentUpdateRequest
            {
                Id = _documentId,
                DataDokumentu = selectedDate.Value.Date,
                OpisDokumentu = DescriptionTextBox.Text.Trim(),
                Operator = _operatorCode
            };
        }

        private async Task ReloadPositionsAsync()
        {
            List<DocumentPositionItem> positions = await _documentCatalogService.LoadDocumentPositionsAsync(_documentId);
            _positions.Clear();
            foreach (DocumentPositionItem item in positions)
            {
                _positions.Add(item);
            }
        }

        private string? GetSelectedSectorValue(System.Windows.Controls.ComboBox comboBox, bool pendingDetach)
        {
            if (pendingDetach)
            {
                return DetachSectorKey;
            }

            string? normalizedValue = (comboBox.SelectedItem as SectorSelectionItem)?.Value;
            return string.IsNullOrWhiteSpace(normalizedValue) ? null : normalizedValue;
        }

        private static string? NormalizeValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private bool CanAutoPersist()
        {
            return !_isReadOnlyMode && !_isInitializing && !_suppressAutoPersist && !_isFinalizing && !_allowClose;
        }

        private async Task<bool> PersistDocumentChangesAsync(bool force, bool showErrors)
        {
            if (_isReadOnlyMode || _isFinalizing || _allowClose)
            {
                return true;
            }

            await _persistSemaphore.WaitAsync();
            try
            {
                if (_isReadOnlyMode || _isFinalizing || _allowClose)
                {
                    return true;
                }

                DocumentUpdateRequest request = BuildUpdateRequest();
                string? sourceWarehouseCode = GetCurrentSourceWarehouseCode();
                string? sourceSectorCode = GetCurrentSourceSectorCode();
                string? destinationWarehouseCode = GetCurrentDestinationWarehouseCode();
                string? destinationSectorCode = GetCurrentDestinationSectorCode();
                string currentStateSignature = CreateStateSignature(request, sourceWarehouseCode, sourceSectorCode, destinationWarehouseCode, destinationSectorCode);
                if (!force && string.Equals(currentStateSignature, _lastPersistedStateSignature, StringComparison.Ordinal))
                {
                    return true;
                }

                if (HasSimpleFieldChanges(request))
                {
                    DocumentProcedureResult updateResult = await _documentCatalogService.UpdateDocumentAsync(request);
                    if (!updateResult.IsSuccess)
                    {
                        if (showErrors)
                        {
                            AppDialogWindow.Show(this, "Edycja dokumentu", updateResult.Message, AppDialogKind.Warning);
                        }

                        return false;
                    }

                    ApplyPersistedSimpleState(request);
                }

                if (ShouldPersistWarehouseChange(sourceWarehouseCode, _persistedSourceWarehouseValue))
                {
                    DocumentProcedureResult sourceWarehouseResult = await _documentCatalogService.ChangeDocumentWarehouseAsync(_documentId, sourceWarehouseCode!, true, _operatorCode);
                    if (!sourceWarehouseResult.IsSuccess)
                    {
                        if (showErrors)
                        {
                            AppDialogWindow.Show(this, "Edycja dokumentu", sourceWarehouseResult.Message, AppDialogKind.Warning);
                        }

                        return false;
                    }

                    ApplyPersistedWarehouseState(true, sourceWarehouseCode!);
                }

                if (ShouldPersistWarehouseChange(destinationWarehouseCode, _persistedDestinationWarehouseValue))
                {
                    DocumentProcedureResult destinationWarehouseResult = await _documentCatalogService.ChangeDocumentWarehouseAsync(_documentId, destinationWarehouseCode!, false, _operatorCode);
                    if (!destinationWarehouseResult.IsSuccess)
                    {
                        if (showErrors)
                        {
                            AppDialogWindow.Show(this, "Edycja dokumentu", destinationWarehouseResult.Message, AppDialogKind.Warning);
                        }

                        return false;
                    }

                    ApplyPersistedWarehouseState(false, destinationWarehouseCode!);
                }

                if (ShouldPersistSectorChange(sourceSectorCode, _persistedSourceSectorValue))
                {
                    DocumentProcedureResult sourceSectorResult = await _documentCatalogService.ChangeDocumentSectorAsync(_documentId, sourceSectorCode!, true, _operatorCode);
                    if (!sourceSectorResult.IsSuccess)
                    {
                        if (showErrors)
                        {
                            AppDialogWindow.Show(this, "Edycja dokumentu", sourceSectorResult.Message, AppDialogKind.Warning);
                        }

                        return false;
                    }

                    ApplyPersistedSectorState(true, sourceSectorCode);
                }

                if (ShouldPersistSectorChange(destinationSectorCode, _persistedDestinationSectorValue))
                {
                    DocumentProcedureResult destinationSectorResult = await _documentCatalogService.ChangeDocumentSectorAsync(_documentId, destinationSectorCode!, false, _operatorCode);
                    if (!destinationSectorResult.IsSuccess)
                    {
                        if (showErrors)
                        {
                            AppDialogWindow.Show(this, "Edycja dokumentu", destinationSectorResult.Message, AppDialogKind.Warning);
                        }

                        return false;
                    }

                    ApplyPersistedSectorState(false, destinationSectorCode);
                }

                _lastPersistedStateSignature = currentStateSignature;
                return true;
            }
            catch (Exception ex)
            {
                if (showErrors)
                {
                    AppDialogWindow.Show(this, "Edycja dokumentu", ex.Message, AppDialogKind.Error);
                }

                return false;
            }
            finally
            {
                _persistSemaphore.Release();
            }
        }

        private static string CreateStateSignature(DocumentUpdateRequest request, string? sourceWarehouseCode, string? sourceSectorCode, string? destinationWarehouseCode, string? destinationSectorCode)
        {
            return string.Join("|",
                request.DataDokumentu?.ToString("O") ?? string.Empty,
                sourceWarehouseCode ?? string.Empty,
                sourceSectorCode ?? string.Empty,
                destinationWarehouseCode ?? string.Empty,
                destinationSectorCode ?? string.Empty,
                NormalizeDescription(request.OpisDokumentu));
        }

        private string CreatePersistedStateSignature()
        {
            return string.Join("|",
                _persistedDocumentDate?.ToString("O") ?? string.Empty,
                _persistedSourceWarehouseValue ?? string.Empty,
                _persistedSourceSectorValue ?? string.Empty,
                _persistedDestinationWarehouseValue ?? string.Empty,
                _persistedDestinationSectorValue ?? string.Empty,
                _persistedDescription);
        }

        private bool HasSimpleFieldChanges(DocumentUpdateRequest request)
        {
            return request.DataDokumentu?.Date != _persistedDocumentDate?.Date
                || !string.Equals(NormalizeDescription(request.OpisDokumentu), _persistedDescription, StringComparison.Ordinal);
        }

        private static bool ShouldPersistWarehouseChange(string? currentValue, string? persistedValue)
        {
            return !string.IsNullOrWhiteSpace(currentValue)
                && !string.Equals(currentValue, persistedValue, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldPersistSectorChange(string? currentValue, string? persistedValue)
        {
            return !string.Equals(currentValue, persistedValue, StringComparison.OrdinalIgnoreCase);
        }

        private string? GetCurrentSourceWarehouseCode()
        {
            return _documentType == "PM" ? null : NormalizeValue(SourceWarehouseComboBox.SelectedValue as string);
        }

        private string? GetCurrentDestinationWarehouseCode()
        {
            return _documentType == "WM" ? null : NormalizeValue(DestinationWarehouseComboBox.SelectedValue as string);
        }

        private string? GetCurrentSourceSectorCode()
        {
            return _documentType == "PM" ? null : GetSelectedSectorValue(SourceSectorComboBox, _pendingSourceSectorDetach);
        }

        private string? GetCurrentDestinationSectorCode()
        {
            return _documentType == "WM" ? null : GetSelectedSectorValue(DestinationSectorComboBox, _pendingDestinationSectorDetach);
        }

        private void ApplyPersistedSimpleState(DocumentUpdateRequest request)
        {
            _persistedDocumentDate = request.DataDokumentu?.Date;
            _persistedDescription = NormalizeDescription(request.OpisDokumentu);
            _lastPersistedStateSignature = CreatePersistedStateSignature();
        }

        private void ApplyPersistedWarehouseState(bool isSource, string warehouseCode)
        {
            if (isSource)
            {
                _persistedSourceWarehouseValue = warehouseCode;
                _persistedSourceSectorValue = null;
                _pendingSourceSectorDetach = false;
            }
            else
            {
                _persistedDestinationWarehouseValue = warehouseCode;
                _persistedDestinationSectorValue = null;
                _pendingDestinationSectorDetach = false;
            }

            _lastPersistedStateSignature = CreatePersistedStateSignature();
            UpdateSectorClearButtons();
        }

        private void ApplyPersistedSectorState(bool isSource, string? sectorCode)
        {
            string? persistedSectorCode = string.Equals(sectorCode, DetachSectorKey, StringComparison.Ordinal)
                ? null
                : NormalizeValue(sectorCode);

            if (isSource)
            {
                _persistedSourceSectorValue = persistedSectorCode;
                _pendingSourceSectorDetach = false;
            }
            else
            {
                _persistedDestinationSectorValue = persistedSectorCode;
                _pendingDestinationSectorDetach = false;
            }

            _lastPersistedStateSignature = CreatePersistedStateSignature();
            UpdateSectorClearButtons();
        }

        private static string NormalizeDescription(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private void UpdateSectorClearButtons()
        {
            SourceSectorClearButton.IsEnabled = !_isReadOnlyMode
                && _documentType != "PM"
                && SourceWarehouseComboBox.SelectedValue is not null
                && SourceSectorComboBox.SelectedItem is not null;

            DestinationSectorClearButton.IsEnabled = !_isReadOnlyMode
                && _documentType != "WM"
                && DestinationWarehouseComboBox.SelectedValue is not null
                && DestinationSectorComboBox.SelectedItem is not null;
        }

        private void ConfigureDocumentActionButtons(bool canEdit)
        {
            HideDocumentActionButtons();
            if (!canEdit)
            {
                return;
            }

            switch (_documentStatus)
            {
                case "Szkic":
                    ConfigureDocumentActionButton(SecondaryDocumentActionButton, "Usun", "Usun dokument");
                    ConfigureDocumentActionButton(DocumentActionButton, "Zatwierdz", "Zatwierdz dokument");
                    break;

                case "Zatwierdzony":
                    ConfigureDocumentActionButton(DocumentActionButton, "Anuluj", "Anuluj dokument");
                    break;
            }
        }

        private void ConfigureDocumentActionButton(System.Windows.Controls.Button button, string action, string content)
        {
            button.Tag = action;
            button.Content = content;
            button.Visibility = Visibility.Visible;
            button.IsEnabled = true;
        }

        private void HideDocumentActionButtons()
        {
            SecondaryDocumentActionButton.Visibility = Visibility.Collapsed;
            SecondaryDocumentActionButton.Tag = null;
            DocumentActionButton.Visibility = Visibility.Collapsed;
            DocumentActionButton.Tag = null;
        }

        private void SetDocumentActionButtonsEnabled(bool isEnabled)
        {
            DocumentActionButton.IsEnabled = isEnabled && DocumentActionButton.Visibility == Visibility.Visible;
            SecondaryDocumentActionButton.IsEnabled = isEnabled && SecondaryDocumentActionButton.Visibility == Visibility.Visible;
        }

        private bool ConfirmDocumentAction(string action)
        {
            string title = action switch
            {
                "Usun" => "Usuwanie dokumentu",
                "Zatwierdz" => "Zatwierdzanie dokumentu",
                "Anuluj" => "Anulowanie dokumentu",
                _ => "Potwierdzenie akcji"
            };

            string message = action switch
            {
                "Usun" => "Czy na pewno chcesz usunac ten dokument?",
                "Zatwierdz" => "Czy na pewno chcesz zatwierdzic ten dokument?",
                "Anuluj" => "Czy na pewno chcesz anulowac ten dokument?",
                _ => "Czy na pewno chcesz wykonac te akcje?"
            };

            string confirmText = action switch
            {
                "Usun" => "Usun",
                "Zatwierdz" => "Zatwierdz",
                "Anuluj" => "Anuluj",
                _ => "Potwierdz"
            };

            return AppDialogWindow.Confirm(this, title, message, confirmText, "Nie", AppDialogKind.Warning);
        }

        private static T? FindVisualParent<T>(DependencyObject? source)
            where T : DependencyObject
        {
            DependencyObject? current = source;
            while (current is not null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private sealed class SectorSelectionItem
        {
            public string? Value { get; init; }
            public string DisplayName { get; init; } = string.Empty;
        }
    }
}