using System.Globalization;
using System.Windows;
using SQLWMS.Models;
using SQLWMS.Services;

namespace SQLWMS
{
    public partial class AddDocumentPositionWindow : Window
    {
        private readonly DocumentCatalogService _documentCatalogService;
        private readonly DocumentPositionItem? _existingPosition;

        internal AddDocumentPositionWindow(DocumentCatalogService documentCatalogService)
        {
            InitializeComponent();
            _documentCatalogService = documentCatalogService;

            Loaded += AddDocumentPositionWindow_Loaded;
        }

        internal AddDocumentPositionWindow(DocumentCatalogService documentCatalogService, DocumentPositionItem existingPosition)
            : this(documentCatalogService)
        {
            _existingPosition = existingPosition;

            DialogTitleTextBlock.Text = "Pozycja dokumentu";
            DialogSubtitleTextBlock.Text = "Mozesz zmienic towar i ilosc pozycji. Jednostka jest tylko informacyjna.";
            AddButton.Content = "Zapisz pozycje";
            FeaturePanel.Visibility = Visibility.Collapsed;
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

        private void AddButton_Click(object sender, RoutedEventArgs e)
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
            DialogResult = true;
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
    }
}