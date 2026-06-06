using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SQLWMS.Models;
using SQLWMS.Services;

namespace SQLWMS
{
    public partial class DocumentAdvancedFiltersWindow : Window
    {
        private readonly DocumentCatalogService _documentCatalogService;
        private readonly string _initialWarehouseCode;
        private readonly string _initialSectorCode;
        private readonly string _initialProductCode;
        private readonly string _initialSeries;
        private bool _isLoading;

        internal DocumentAdvancedFiltersWindow(
            DocumentCatalogService documentCatalogService,
            string warehouseCode,
            string sectorCode,
            string productCode,
            string series)
        {
            InitializeComponent();

            _documentCatalogService = documentCatalogService;
            _initialWarehouseCode = warehouseCode ?? string.Empty;
            _initialSectorCode = sectorCode ?? string.Empty;
            _initialProductCode = productCode ?? string.Empty;
            _initialSeries = series ?? string.Empty;

            Loaded += DocumentAdvancedFiltersWindow_Loaded;
        }

        internal string SelectedWarehouseCode { get; private set; } = string.Empty;

        internal string SelectedSectorCode { get; private set; } = string.Empty;

        internal string SelectedProductCode { get; private set; } = string.Empty;

        internal string SelectedSeries { get; private set; } = string.Empty;

        private async void DocumentAdvancedFiltersWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            ApplyButton.IsEnabled = false;

            try
            {
                await LoadWarehouseOptionsAsync(_initialWarehouseCode);
                await LoadSectorOptionsAsync(_initialWarehouseCode, _initialSectorCode);
                await LoadProductOptionsAsync(_initialProductCode);
                SeriesFilterTextBox.Text = _initialSeries;
            }
            catch (Exception ex)
            {
                ValidationTextBlock.Text = $"Nie udalo sie pobrac filtrow. {ex.Message}";
            }
            finally
            {
                _isLoading = false;
                ApplyButton.IsEnabled = true;
            }
        }

        private async Task LoadWarehouseOptionsAsync(string selectedWarehouseCode)
        {
            List<WarehouseLookupItem> warehouses = await _documentCatalogService.LoadWarehouseLookupAsync();
            List<FilterOptionItem> options = [new FilterOptionItem(string.Empty, "Wszystkie magazyny")];
            options.AddRange(warehouses.Select(item => new FilterOptionItem(item.Code, item.DisplayName)));

            WarehouseFilterComboBox.ItemsSource = options;
            WarehouseFilterComboBox.SelectedValue = selectedWarehouseCode;
            if (WarehouseFilterComboBox.SelectedIndex < 0)
            {
                WarehouseFilterComboBox.SelectedIndex = 0;
            }
        }

        private async Task LoadSectorOptionsAsync(string? warehouseCode, string selectedSectorCode)
        {
            List<SectorLookupItem> sectors = await _documentCatalogService.LoadSectorLookupAsync(string.IsNullOrWhiteSpace(warehouseCode) ? null : warehouseCode);
            List<FilterOptionItem> options = [new FilterOptionItem(string.Empty, "Wszystkie sektory")];
            options.AddRange(sectors.Select(item => new FilterOptionItem(item.Code, $"{item.WarehouseCode} / {item.DisplayName}")));

            SectorFilterComboBox.ItemsSource = options;
            SectorFilterComboBox.SelectedValue = selectedSectorCode;
            if (SectorFilterComboBox.SelectedIndex < 0)
            {
                SectorFilterComboBox.SelectedIndex = 0;
            }
        }

        private async Task LoadProductOptionsAsync(string selectedProductCode)
        {
            List<ProductLookupItem> products = await _documentCatalogService.LoadProductLookupAsync();
            List<FilterOptionItem> options = [new FilterOptionItem(string.Empty, "Wszystkie towary")];
            options.AddRange(products.Select(item => new FilterOptionItem(item.Code, item.DisplayName)));

            ProductFilterComboBox.ItemsSource = options;
            ProductFilterComboBox.SelectedValue = selectedProductCode;
            if (ProductFilterComboBox.SelectedIndex < 0)
            {
                ProductFilterComboBox.SelectedIndex = 0;
            }
        }

        private async void WarehouseFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            string currentSectorCode = GetSelectedValue(SectorFilterComboBox);
            await LoadSectorOptionsAsync(GetSelectedValue(WarehouseFilterComboBox), currentSectorCode);
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedWarehouseCode = GetSelectedValue(WarehouseFilterComboBox);
            SelectedSectorCode = GetSelectedValue(SectorFilterComboBox);
            SelectedProductCode = GetSelectedValue(ProductFilterComboBox);
            SelectedSeries = SeriesFilterTextBox.Text.Trim();
            DialogResult = true;
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            WarehouseFilterComboBox.SelectedIndex = 0;
            await LoadSectorOptionsAsync(null, string.Empty);
            ProductFilterComboBox.SelectedIndex = 0;
            SeriesFilterTextBox.Clear();
            _isLoading = false;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private static string GetSelectedValue(System.Windows.Controls.ComboBox comboBox)
        {
            return comboBox.SelectedItem is FilterOptionItem item
                ? item.Value
                : string.Empty;
        }

        private void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private sealed class FilterOptionItem(string value, string displayName)
        {
            public string Value { get; } = value;

            public string DisplayName { get; } = displayName;
        }
    }
}