using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using SQLWMS.Models;
using SQLWMS.Services;

namespace SQLWMS
{
    public partial class AddDocumentPositionWindow : Window
    {
        private readonly DocumentCatalogService _documentCatalogService;
        private readonly int? _documentId;
        private readonly DocumentPositionItem? _existingPosition;
        private readonly string _operatorCode;
        private readonly ObservableCollection<PositionAllocationItem> _allocations = [];
        private List<UnitLookupItem> _availableUnits = [];
        private bool _isSyncingPositionQuantity;
        private bool _isSyncingAllocationQuantity;

        internal AddDocumentPositionWindow(DocumentCatalogService documentCatalogService, int documentId, string operatorCode)
        {
            InitializeComponent();
            _documentCatalogService = documentCatalogService;
            _documentId = documentId;
            _operatorCode = operatorCode;
            AllocationsDataGrid.ItemsSource = _allocations;
            UpdateAllocationSectionState();

            Loaded += AddDocumentPositionWindow_Loaded;
        }

        internal AddDocumentPositionWindow(DocumentCatalogService documentCatalogService, string operatorCode, DocumentPositionItem existingPosition)
            : this(documentCatalogService, documentId: 0, operatorCode)
        {
            _existingPosition = existingPosition;

            DialogTitleTextBlock.Text = "Pozycja dokumentu";
            AddButton.Content = "Zapisz pozycje";
            FeaturePanel.Visibility = Visibility.Collapsed;
            ProductComboBox.IsEnabled = false;
        }

        public bool IsEditMode => _existingPosition is not null;

        public string SelectedProductCode { get; private set; } = string.Empty;

        public string SelectedUnitCode { get; private set; } = string.Empty;

        public decimal Quantity { get; private set; }

        public string? Feature => string.IsNullOrWhiteSpace(FeatureTextBox.Text) ? null : FeatureTextBox.Text.Trim();

        private async void AddDocumentPositionWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                List<ProductLookupItem> products = await _documentCatalogService.LoadProductLookupAsync();
                ProductComboBox.ItemsSource = products;

                if (products.Count > 0)
                {
                    if (_existingPosition is not null)
                    {
                        ProductComboBox.SelectedValue = _existingPosition.TowarKod;
                        SetPositionQuantitiesSilently(
                            _existingPosition.IloscJednostkowa.ToString("0.###", CultureInfo.InvariantCulture),
                            _existingPosition.Ilosc.ToString("0.###", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        ProductComboBox.SelectedIndex = 0;
                        SetPositionQuantitiesSilently(QuantityTextBox.Text, QuantityTextBox.Text);
                    }
                }
                else
                {
                    ValidationTextBlock.Text = "Brak towarow do wyboru.";
                    AddButton.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                ValidationTextBlock.Text = $"Nie udalo sie pobrac towarow. {ex.Message}";
                AddButton.IsEnabled = false;
            }

            if (IsEditMode)
            {
                await LoadAllocationsAsync();
            }
            else
            {
                UpdateAllocationSectionState();
            }
        }

        private async void ProductComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ProductComboBox.SelectedItem is not ProductLookupItem product)
            {
                _availableUnits = [];
                UnitComboBox.ItemsSource = null;
                RefreshPositionQuantityLabels();
                return;
            }

            try
            {
                ValidationTextBlock.Text = string.Empty;
                AddButton.IsEnabled = false;
                List<UnitLookupItem> units = await _documentCatalogService.LoadUnitLookupAsync(product.Code);
                _availableUnits = units;
                UnitComboBox.ItemsSource = units;

                if (units.Count > 0)
                {
                    if (_existingPosition is not null)
                    {
                        UnitComboBox.SelectedValue = _existingPosition.Jednostka;
                        if (UnitComboBox.SelectedIndex < 0)
                        {
                            UnitComboBox.SelectedIndex = 0;
                        }

                        UnitComboBox.IsEnabled = false;
                    }
                    else
                    {
                        UnitComboBox.SelectedIndex = 0;
                    }

                    RefreshPositionQuantityLabels();
                    SyncBaseQuantityFromUnitQuantity();
                    AddButton.IsEnabled = true;
                }
                else
                {
                    _availableUnits = [];
                    RefreshPositionQuantityLabels();
                    ValidationTextBlock.Text = "Wybrany towar nie ma jednostek do wyboru.";
                }
            }
            catch (Exception ex)
            {
                ValidationTextBlock.Text = $"Nie udalo sie pobrac jednostek. {ex.Message}";
            }
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            ValidationTextBlock.Text = string.Empty;

            if (ProductComboBox.SelectedItem is not ProductLookupItem product)
            {
                ValidationTextBlock.Text = "Wybierz towar.";
                return;
            }

            if (UnitComboBox.SelectedItem is not UnitLookupItem unit)
            {
                ValidationTextBlock.Text = "Wybierz jednostke.";
                return;
            }

            string quantityText = QuantityTextBox.Text.Trim();
            if (!TryParseQuantity(quantityText, out decimal quantity) || quantity <= 0)
            {
                ValidationTextBlock.Text = "Podaj dodatnia ilosc.";
                return;
            }

            SelectedProductCode = product.Code;
            SelectedUnitCode = unit.Code;
            Quantity = quantity;

            AddButton.IsEnabled = false;

            try
            {
                DocumentProcedureResult result = IsEditMode
                    ? await _documentCatalogService.UpdateDocumentPositionAsync(new DocumentPositionUpdateRequest
                    {
                        Id = _existingPosition!.Id,
                        Ilosc = quantity,
                        Operator = _operatorCode
                    })
                    : await _documentCatalogService.AddDocumentPositionAsync(new DocumentPositionCreateRequest
                    {
                        DocumentId = _documentId ?? 0,
                        TowarKod = product.Code,
                        Ilosc = quantity,
                        JednostkaKod = unit.Code,
                        Cecha = Feature,
                        Operator = _operatorCode
                    });

                if (!result.IsSuccess)
                {
                    ValidationTextBlock.Text = result.Message;
                    AddButton.IsEnabled = true;
                    return;
                }

                DialogResult = true;
            }
            catch (Exception ex)
            {
                ValidationTextBlock.Text = ex.Message;
                AddButton.IsEnabled = true;
            }
        }

        private async Task LoadAllocationsAsync()
        {
            if (!IsEditMode)
            {
                UpdateAllocationSectionState();
                return;
            }

            try
            {
                AllocationValidationTextBlock.Text = string.Empty;
                List<PositionAllocationItem> allocations = await _documentCatalogService.LoadPositionAllocationsAsync(_existingPosition!.Id);

                _allocations.Clear();
                foreach (PositionAllocationItem allocation in allocations)
                {
                    _allocations.Add(allocation);
                }

                if (_allocations.Count > 0)
                {
                    AllocationsDataGrid.SelectedIndex = 0;
                }
                else
                {
                    AllocationsDataGrid.SelectedItem = null;
                }
            }
            catch (Exception ex)
            {
                AllocationValidationTextBlock.Text = $"Nie udalo sie pobrac alokacji. {ex.Message}";
            }

            UpdateAllocationSectionState();
        }

        private void UpdateAllocationSectionState()
        {
            bool hasSelection = AllocationsDataGrid.SelectedItem is PositionAllocationItem;
            bool isEditMode = IsEditMode;
            bool hasAllocations = _allocations.Count > 0;

            AllocationNewPositionTextBlock.Visibility = isEditMode ? Visibility.Collapsed : Visibility.Visible;
            AllocationEmptyStateTextBlock.Visibility = isEditMode && !hasAllocations ? Visibility.Visible : Visibility.Collapsed;
            AllocationsDataGrid.Visibility = isEditMode && hasAllocations ? Visibility.Visible : Visibility.Collapsed;
            AllocationActionPanel.Visibility = isEditMode ? Visibility.Visible : Visibility.Collapsed;

            AllocationSplitQuantityTextBox.IsEnabled = isEditMode && hasSelection;
            AllocationSplitBaseQuantityTextBox.IsEnabled = isEditMode && hasSelection;
            AllocationSplitFeatureTextBox.IsEnabled = isEditMode && hasSelection;
            SplitAllocationButton.IsEnabled = isEditMode && hasSelection;
            DeleteAllocationButton.IsEnabled = isEditMode && hasSelection;

            RefreshAllocationQuantityLabels();
        }

        private void AllocationsDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            AllocationValidationTextBlock.Text = string.Empty;

            if (AllocationsDataGrid.SelectedItem is not PositionAllocationItem)
            {
                SetAllocationQuantitiesSilently(string.Empty, string.Empty);
                AllocationSplitFeatureTextBox.Text = string.Empty;
            }
            else
            {
                SyncAllocationBaseQuantityFromUnitQuantity();
            }

            UpdateAllocationSectionState();
        }

        private async void SplitAllocationButton_Click(object sender, RoutedEventArgs e)
        {
            AllocationValidationTextBlock.Text = string.Empty;

            if (AllocationsDataGrid.SelectedItem is not PositionAllocationItem allocation)
            {
                AllocationValidationTextBlock.Text = "Wybierz alokacje do rozbicia.";
                return;
            }

            if (!TryParseQuantity(AllocationSplitQuantityTextBox.Text.Trim(), out decimal unitQuantity) || unitQuantity <= 0)
            {
                AllocationValidationTextBlock.Text = "Podaj dodatnia ilosc do rozbicia.";
                return;
            }

            SplitAllocationButton.IsEnabled = false;
            DeleteAllocationButton.IsEnabled = false;

            try
            {
                DocumentProcedureResult result = await _documentCatalogService.SplitAllocationAsync(new AllocationSplitRequest
                {
                    AllocationId = allocation.AllocationId,
                    Quantity = unitQuantity,
                    Feature = string.IsNullOrWhiteSpace(AllocationSplitFeatureTextBox.Text) ? null : AllocationSplitFeatureTextBox.Text.Trim(),
                    Operator = _operatorCode
                });

                if (!result.IsSuccess)
                {
                    AllocationValidationTextBlock.Text = result.Message;
                    UpdateAllocationSectionState();
                    return;
                }

                SetAllocationQuantitiesSilently(string.Empty, string.Empty);
                AllocationSplitFeatureTextBox.Text = string.Empty;
                await LoadAllocationsAsync();
            }
            catch (Exception ex)
            {
                AllocationValidationTextBlock.Text = ex.Message;
                UpdateAllocationSectionState();
            }
        }

        private async void DeleteAllocationButton_Click(object sender, RoutedEventArgs e)
        {
            AllocationValidationTextBlock.Text = string.Empty;

            if (AllocationsDataGrid.SelectedItem is not PositionAllocationItem allocation)
            {
                AllocationValidationTextBlock.Text = "Wybierz alokacje do usuniecia.";
                return;
            }

            bool confirmed = AppDialogWindow.Confirm(
                this,
                "Usuwanie alokacji",
                $"Czy na pewno usunac alokacje \"{allocation.DisplayFeature}\"?",
                "Usun",
                "Anuluj");

            if (!confirmed)
            {
                return;
            }

            SplitAllocationButton.IsEnabled = false;
            DeleteAllocationButton.IsEnabled = false;

            try
            {
                DocumentProcedureResult result = await _documentCatalogService.DeleteAllocationAsync(allocation.AllocationId, _operatorCode);
                if (!result.IsSuccess)
                {
                    AllocationValidationTextBlock.Text = result.Message;
                    UpdateAllocationSectionState();
                    return;
                }

                await LoadAllocationsAsync();
            }
            catch (Exception ex)
            {
                AllocationValidationTextBlock.Text = ex.Message;
                UpdateAllocationSectionState();
            }
        }

        private static bool TryParseQuantity(string value, out decimal quantity)
        {
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out quantity)
                || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity);
        }

        private void UnitComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RefreshPositionQuantityLabels();
            SyncBaseQuantityFromUnitQuantity();
        }

        private void QuantityTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            SyncBaseQuantityFromUnitQuantity();
        }

        private void BaseQuantityTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            SyncUnitQuantityFromBaseQuantity();
        }

        private void AllocationSplitQuantityTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            SyncAllocationBaseQuantityFromUnitQuantity();
        }

        private void AllocationSplitBaseQuantityTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            SyncAllocationUnitQuantityFromBaseQuantity();
        }

        private void SyncBaseQuantityFromUnitQuantity()
        {
            if (_isSyncingPositionQuantity || !ArePositionQuantityControlsReady())
            {
                return;
            }

            if (!TryGetSelectedUnitConversionFactor(out decimal factor))
            {
                return;
            }

            if (!TryParseQuantity(QuantityTextBox.Text.Trim(), out decimal unitQuantity))
            {
                SetPositionBaseQuantityText(string.Empty);
                return;
            }

            SetPositionBaseQuantityText(FormatQuantity(unitQuantity * factor));
        }

        private void SyncUnitQuantityFromBaseQuantity()
        {
            if (_isSyncingPositionQuantity || !ArePositionQuantityControlsReady())
            {
                return;
            }

            if (!TryGetSelectedUnitConversionFactor(out decimal factor))
            {
                return;
            }

            if (!TryParseQuantity(BaseQuantityTextBox.Text.Trim(), out decimal baseQuantity))
            {
                SetPositionUnitQuantityText(string.Empty);
                return;
            }

            SetPositionUnitQuantityText(FormatQuantity(baseQuantity / factor));
        }

        private void SetPositionBaseQuantityText(string value)
        {
            if (BaseQuantityTextBox is null)
            {
                return;
            }

            _isSyncingPositionQuantity = true;
            BaseQuantityTextBox.Text = value;
            _isSyncingPositionQuantity = false;
        }

        private void SetPositionQuantitiesSilently(string unitValue, string baseValue)
        {
            if (!ArePositionQuantityControlsReady())
            {
                return;
            }

            _isSyncingPositionQuantity = true;
            QuantityTextBox.Text = unitValue;
            BaseQuantityTextBox.Text = baseValue;
            _isSyncingPositionQuantity = false;
        }

        private void SetPositionUnitQuantityText(string value)
        {
            if (QuantityTextBox is null)
            {
                return;
            }

            _isSyncingPositionQuantity = true;
            QuantityTextBox.Text = value;
            _isSyncingPositionQuantity = false;
        }

        private void SyncAllocationBaseQuantityFromUnitQuantity()
        {
            if (_isSyncingAllocationQuantity || !AreAllocationQuantityControlsReady())
            {
                return;
            }

            decimal factor = GetSelectedAllocationConversionFactor();
            if (!TryParseQuantity(AllocationSplitQuantityTextBox.Text.Trim(), out decimal unitQuantity))
            {
                SetAllocationBaseQuantityText(string.Empty);
                return;
            }

            SetAllocationBaseQuantityText(FormatQuantity(unitQuantity * factor));
        }

        private void SyncAllocationUnitQuantityFromBaseQuantity()
        {
            if (_isSyncingAllocationQuantity || !AreAllocationQuantityControlsReady())
            {
                return;
            }

            decimal factor = GetSelectedAllocationConversionFactor();
            if (!TryParseQuantity(AllocationSplitBaseQuantityTextBox.Text.Trim(), out decimal baseQuantity))
            {
                SetAllocationUnitQuantityText(string.Empty);
                return;
            }

            SetAllocationUnitQuantityText(FormatQuantity(baseQuantity / factor));
        }

        private void SetAllocationBaseQuantityText(string value)
        {
            if (AllocationSplitBaseQuantityTextBox is null)
            {
                return;
            }

            _isSyncingAllocationQuantity = true;
            AllocationSplitBaseQuantityTextBox.Text = value;
            _isSyncingAllocationQuantity = false;
        }

        private void SetAllocationQuantitiesSilently(string unitValue, string baseValue)
        {
            if (!AreAllocationQuantityControlsReady())
            {
                return;
            }

            _isSyncingAllocationQuantity = true;
            AllocationSplitQuantityTextBox.Text = unitValue;
            AllocationSplitBaseQuantityTextBox.Text = baseValue;
            _isSyncingAllocationQuantity = false;
        }

        private void SetAllocationUnitQuantityText(string value)
        {
            if (AllocationSplitQuantityTextBox is null)
            {
                return;
            }

            _isSyncingAllocationQuantity = true;
            AllocationSplitQuantityTextBox.Text = value;
            _isSyncingAllocationQuantity = false;
        }

        private void RefreshPositionQuantityLabels()
        {
            if (QuantityLabelTextBlock is null || BaseQuantityLabelTextBlock is null)
            {
                return;
            }

            string unitLabel = GetSelectedUnitDisplayCode();
            string baseUnitLabel = GetBaseUnitDisplayCode();

            QuantityLabelTextBlock.Text = $"Ilosc w jednostce pozycji ({unitLabel})";
            BaseQuantityLabelTextBlock.Text = $"Ilosc w jednostce podstawowej ({baseUnitLabel})";
        }

        private void RefreshAllocationQuantityLabels()
        {
            if (AllocationSplitQuantityLabelTextBlock is null || AllocationSplitBaseQuantityLabelTextBlock is null)
            {
                return;
            }

            string unitLabel = GetSelectedAllocationUnitDisplayCode();
            string baseUnitLabel = GetBaseUnitDisplayCode();

            AllocationSplitQuantityLabelTextBlock.Text = $"Ilosc do rozbicia ({unitLabel})";
            AllocationSplitBaseQuantityLabelTextBlock.Text = $"Ilosc podstawowa ({baseUnitLabel})";
        }

        private decimal GetSelectedUnitConversionFactor()
        {
            if (UnitComboBox.SelectedItem is UnitLookupItem unit && unit.ConversionFactor > 0)
            {
                return unit.ConversionFactor;
            }

            if (_availableUnits.FirstOrDefault(item => item.ConversionFactor > 0) is UnitLookupItem fallbackUnit)
            {
                return fallbackUnit.ConversionFactor;
            }

            return 1m;
        }

        private bool TryGetSelectedUnitConversionFactor(out decimal factor)
        {
            if (UnitComboBox.SelectedItem is UnitLookupItem unit && unit.ConversionFactor > 0)
            {
                factor = unit.ConversionFactor;
                return true;
            }

            factor = 0m;
            return false;
        }

        private decimal GetSelectedAllocationConversionFactor()
        {
            if (AllocationsDataGrid.SelectedItem is PositionAllocationItem allocation
                && allocation.UnitQuantity > 0
                && allocation.Quantity > 0)
            {
                return allocation.Quantity / allocation.UnitQuantity;
            }

            return GetSelectedUnitConversionFactor();
        }

        private string GetSelectedUnitDisplayCode()
        {
            if (UnitComboBox.SelectedItem is UnitLookupItem unit && !string.IsNullOrWhiteSpace(unit.Code))
            {
                return unit.Code;
            }

            return "JP";
        }

        private string GetSelectedAllocationUnitDisplayCode()
        {
            if (AllocationsDataGrid.SelectedItem is PositionAllocationItem allocation && !string.IsNullOrWhiteSpace(allocation.UnitCode))
            {
                return allocation.UnitCode;
            }

            return GetSelectedUnitDisplayCode();
        }

        private string GetBaseUnitDisplayCode()
        {
            if (_availableUnits.FirstOrDefault(unit => unit.ConversionFactor == 1m) is UnitLookupItem baseUnit
                && !string.IsNullOrWhiteSpace(baseUnit.Code))
            {
                return baseUnit.Code;
            }

            return "podstawowa";
        }

        private static string FormatQuantity(decimal value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private bool ArePositionQuantityControlsReady()
        {
            return QuantityTextBox is not null && BaseQuantityTextBox is not null;
        }

        private bool AreAllocationQuantityControlsReady()
        {
            return AllocationSplitQuantityTextBox is not null && AllocationSplitBaseQuantityTextBox is not null;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}