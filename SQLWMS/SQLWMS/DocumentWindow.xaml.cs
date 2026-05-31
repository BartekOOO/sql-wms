using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
            _persistedSourceSectorValue = NormalizeValue(details.SektorZrodlowyKod);
            _persistedDestinationSectorValue = NormalizeValue(details.SektorDocelowyKod);

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
            _lastPersistedStateSignature = CreateStateSignature();
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
                    DocumentActionButton.IsEnabled = true;
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
                    DocumentActionButton.IsEnabled = true;
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
                DocumentActionButton.IsEnabled = true;
            }
        }

        private async void DocumentActionButton_Click(object sender, RoutedEventArgs e)
        {
            string? action = GetDocumentAction();
            if (_isReadOnlyMode || string.IsNullOrWhiteSpace(action))
            {
                return;
            }

            _suppressAutoPersist = true;
            SaveButton.IsEnabled = false;
            DocumentActionButton.IsEnabled = false;

            try
            {
                bool updateSucceeded = await PersistDocumentChangesAsync(force: true, showErrors: true);
                if (!updateSucceeded)
                {
                    _suppressAutoPersist = false;
                    SaveButton.IsEnabled = true;
                    DocumentActionButton.IsEnabled = true;
                    return;
                }

                _isFinalizing = true;
                DocumentProcedureResult actionResult = await _documentCatalogService.CloseDocumentAsync(_documentId, _operatorCode, action);
                if (!actionResult.IsSuccess)
                {
                    _isFinalizing = false;
                    _suppressAutoPersist = false;
                    AppDialogWindow.Show(this, "Zamykanie dokumentu", actionResult.Message, AppDialogKind.Warning);
                    SaveButton.IsEnabled = true;
                    DocumentActionButton.IsEnabled = true;
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
                DocumentActionButton.IsEnabled = true;
            }
        }

        private async void AddPositionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AddDocumentPositionWindow addPositionWindow = new(_documentCatalogService)
            {
                Owner = this
            };

            bool? result = addPositionWindow.ShowDialog();
            if (result != true)
            {
                return;
            }

            try
            {
                DocumentProcedureResult addResult = await _documentCatalogService.AddDocumentPositionAsync(new DocumentPositionCreateRequest
                {
                    DocumentId = _documentId,
                    TowarKod = addPositionWindow.SelectedProductCode,
                    Ilosc = addPositionWindow.Quantity,
                    JednostkaKod = addPositionWindow.SelectedUnitCode,
                    Cecha = addPositionWindow.Feature,
                    Operator = _operatorCode
                });

                if (!addResult.IsSuccess)
                {
                    AppDialogWindow.Show(this, "Dodawanie pozycji", addResult.Message, AppDialogKind.Warning);
                    return;
                }

                await ReloadPositionsAsync();
            }
            catch (Exception ex)
            {
                AppDialogWindow.Show(this, "Dodawanie pozycji", ex.Message, AppDialogKind.Error);
            }
        }

        private async void OpenPositionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DocumentPositionsDataGrid.SelectedItem is not DocumentPositionItem selectedPosition)
            {
                return;
            }

            AddDocumentPositionWindow positionWindow = new(_documentCatalogService, selectedPosition)
            {
                Owner = this
            };

            bool? result = positionWindow.ShowDialog();
            if (result != true)
            {
                return;
            }

            try
            {
                DocumentProcedureResult updateResult = await _documentCatalogService.UpdateDocumentPositionAsync(new DocumentPositionUpdateRequest
                {
                    Id = selectedPosition.Id,
                    TowarKod = positionWindow.SelectedProductCode,
                    Ilosc = positionWindow.Quantity,
                    Operator = _operatorCode
                });

                if (!updateResult.IsSuccess)
                {
                    AppDialogWindow.Show(this, "Edycja pozycji", updateResult.Message, AppDialogKind.Warning);
                    return;
                }

                await ReloadPositionsAsync();
            }
            catch (Exception ex)
            {
                AppDialogWindow.Show(this, "Edycja pozycji", ex.Message, AppDialogKind.Error);
            }
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

            try
            {
                DialogResult = dialogResult;
            }
            catch (InvalidOperationException)
            {
            }

            Close();
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
            ConfigureDocumentActionButton(canEdit);
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
                MagazynZrodlowyKod = _documentType == "PM" ? null : NormalizeValue(SourceWarehouseComboBox.SelectedValue as string),
                SektorZrodlowyKod = _documentType == "PM" ? null : GetSelectedSectorValue(SourceSectorComboBox, _pendingSourceSectorDetach),
                MagazynDocelowyKod = _documentType == "WM" ? null : NormalizeValue(DestinationWarehouseComboBox.SelectedValue as string),
                SektorDocelowyKod = _documentType == "WM" ? null : GetSelectedSectorValue(DestinationSectorComboBox, _pendingDestinationSectorDetach),
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
                string currentStateSignature = CreateStateSignature(request);
                if (!force && string.Equals(currentStateSignature, _lastPersistedStateSignature, StringComparison.Ordinal))
                {
                    return true;
                }

                DocumentProcedureResult updateResult = await _documentCatalogService.UpdateDocumentAsync(request);
                if (!updateResult.IsSuccess)
                {
                    if (showErrors)
                    {
                        AppDialogWindow.Show(this, "Edycja dokumentu", updateResult.Message, AppDialogKind.Warning);
                    }

                    return false;
                }

                ApplyPersistedSectorState(request);
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

        private string CreateStateSignature()
        {
            return CreateStateSignature(BuildUpdateRequest());
        }

        private static string CreateStateSignature(DocumentUpdateRequest request)
        {
            return string.Join("|",
                request.DataDokumentu?.ToString("O") ?? string.Empty,
                request.MagazynZrodlowyKod ?? string.Empty,
                request.SektorZrodlowyKod ?? string.Empty,
                request.MagazynDocelowyKod ?? string.Empty,
                request.SektorDocelowyKod ?? string.Empty,
                request.OpisDokumentu ?? string.Empty);
        }

        private void ApplyPersistedSectorState(DocumentUpdateRequest request)
        {
            if (request.SektorZrodlowyKod == DetachSectorKey)
            {
                _persistedSourceSectorValue = null;
                _pendingSourceSectorDetach = false;
            }
            else if (!string.IsNullOrWhiteSpace(request.SektorZrodlowyKod))
            {
                _persistedSourceSectorValue = request.SektorZrodlowyKod;
                _pendingSourceSectorDetach = false;
            }

            if (request.SektorDocelowyKod == DetachSectorKey)
            {
                _persistedDestinationSectorValue = null;
                _pendingDestinationSectorDetach = false;
            }
            else if (!string.IsNullOrWhiteSpace(request.SektorDocelowyKod))
            {
                _persistedDestinationSectorValue = request.SektorDocelowyKod;
                _pendingDestinationSectorDetach = false;
            }

            UpdateSectorClearButtons();
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

        private void ConfigureDocumentActionButton(bool canEdit)
        {
            string? action = GetDocumentAction();
            if (!canEdit || string.IsNullOrWhiteSpace(action))
            {
                DocumentActionButton.Visibility = Visibility.Collapsed;
                return;
            }

            DocumentActionButton.Content = action == "Usun" ? "Usun dokument" : "Anuluj dokument";
            DocumentActionButton.Visibility = Visibility.Visible;
            DocumentActionButton.IsEnabled = true;
        }

        private string? GetDocumentAction()
        {
            return _documentStatus switch
            {
                "Szkic" => "Usun",
                "Zatwierdzony" => "Anuluj",
                _ => null
            };
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