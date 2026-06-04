using System.Collections.ObjectModel;
using System.Globalization;
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
                        QuantityTextBox.Text = _existingPosition.IloscJednostkowa.ToString("0.###", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        ProductComboBox.SelectedIndex = 0;
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
                UnitComboBox.ItemsSource = null;
                return;
            }

            try
            {
                ValidationTextBlock.Text = string.Empty;
                AddButton.IsEnabled = false;
                List<UnitLookupItem> units = await _documentCatalogService.LoadUnitLookupAsync(product.Code);
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

                    AddButton.IsEnabled = true;
                }
                else
                {
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
            AllocationSplitFeatureTextBox.IsEnabled = isEditMode && hasSelection;
            SplitAllocationButton.IsEnabled = isEditMode && hasSelection;
            DeleteAllocationButton.IsEnabled = isEditMode && hasSelection;
        }

        private void AllocationsDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            AllocationValidationTextBlock.Text = string.Empty;

            if (AllocationsDataGrid.SelectedItem is not PositionAllocationItem)
            {
                AllocationSplitQuantityTextBox.Text = string.Empty;
                AllocationSplitFeatureTextBox.Text = string.Empty;
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

                AllocationSplitQuantityTextBox.Text = string.Empty;
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