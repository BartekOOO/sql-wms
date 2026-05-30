using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using SQLWMS.Models;
using SQLWMS.Services;

namespace SQLWMS
{
    public partial class MainWindow : Window
    {
        private readonly ProductCatalogService _productCatalogService = new();
        private readonly WarehouseCatalogService _warehouseCatalogService = new();
        private readonly ObservableCollection<ProductMasterItem> _products = [];
        private readonly ObservableCollection<WarehouseMasterItem> _warehouses = [];
        private readonly ICollectionView _productsView;
        private readonly ICollectionView _warehousesView;
        private int _filterRequestVersion;
        private bool _suspendFilterReload;
        private NavigationSection _currentSection = NavigationSection.Home;

        private enum NavigationSection
        {
            Home,
            Documents,
            Warehouses,
            Products,
            Traceability
        }

        public MainWindow()
        {
            InitializeComponent();

            _productsView = CollectionViewSource.GetDefaultView(_products);
            _warehousesView = CollectionViewSource.GetDefaultView(_warehouses);
            ProductsDataGrid.ItemsSource = _productsView;
            WarehousesDataGrid.ItemsSource = _warehousesView;

            ShowHome();
        }

        private async void DocumentsButton_Click(object sender, RoutedEventArgs e)
        {
            await NavigateToAsync(NavigationSection.Documents);
        }

        private async void WarehousesButton_Click(object sender, RoutedEventArgs e)
        {
            await NavigateToAsync(NavigationSection.Warehouses);
        }

        private async void ProductsButton_Click(object sender, RoutedEventArgs e)
        {
            await NavigateToAsync(NavigationSection.Products);
        }

        private async void TraceabilityButton_Click(object sender, RoutedEventArgs e)
        {
            await NavigateToAsync(NavigationSection.Traceability);
        }

        private async void ProductExpander_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton && toggleButton.DataContext is ProductMasterItem item)
            {
                item.IsExpanded = true;
                SetRowDetailsVisibility(toggleButton, Visibility.Visible);
                ProductsDataGrid.SelectedItem = item;
                await EnsureProductDetailsLoadedAsync(item);
                SetRowDetailsVisibility(toggleButton, Visibility.Visible);
                ProductsDataGrid.UpdateLayout();
            }
        }

        private async void WarehouseExpander_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton && toggleButton.DataContext is WarehouseMasterItem item)
            {
                item.IsExpanded = true;
                SetRowDetailsVisibility(toggleButton, Visibility.Visible);
                WarehousesDataGrid.SelectedItem = item;
                await EnsureWarehouseDetailsLoadedAsync(item);
                SetRowDetailsVisibility(toggleButton, Visibility.Visible);
                WarehousesDataGrid.UpdateLayout();
            }
        }

        private void MasterExpander_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton && toggleButton.DataContext is ProductMasterItem product)
            {
                product.IsExpanded = false;
                SetRowDetailsVisibility(toggleButton, Visibility.Collapsed);
                if (ReferenceEquals(ProductsDataGrid.SelectedItem, product))
                {
                    ProductsDataGrid.SelectedItem = null;
                }
            }

            if (sender is ToggleButton toggleWarehouseButton && toggleWarehouseButton.DataContext is WarehouseMasterItem warehouse)
            {
                warehouse.IsExpanded = false;
                SetRowDetailsVisibility(toggleWarehouseButton, Visibility.Collapsed);
                if (ReferenceEquals(WarehousesDataGrid.SelectedItem, warehouse))
                {
                    WarehousesDataGrid.SelectedItem = null;
                }
            }
        }

        private static void SetRowDetailsVisibility(DependencyObject source, Visibility visibility)
        {
            if (FindVisualParent<DataGridRow>(source) is DataGridRow row)
            {
                row.DetailsVisibility = visibility;
            }
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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            ShowHome();
        }

        private async void SectionFilterTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_suspendFilterReload)
            {
                return;
            }

            await ReloadCurrentSectionAsync();
        }

        private void ShowHome()
        {
            _currentSection = NavigationSection.Home;

            HomeView.Visibility = Visibility.Visible;
            SectionView.Visibility = Visibility.Collapsed;
            HomeOverviewPanel.Visibility = Visibility.Visible;
            BackButton.Visibility = Visibility.Collapsed;
        }

        private async Task NavigateToAsync(NavigationSection section)
        {
            _currentSection = section;

            HomeView.Visibility = Visibility.Collapsed;
            SectionView.Visibility = Visibility.Visible;
            HomeOverviewPanel.Visibility = Visibility.Collapsed;
            BackButton.Visibility = Visibility.Visible;
            SectionTitleTextBlock.Text = GetSectionTitle(section);
            SectionDescriptionTextBlock.Text = GetSectionDescription(section);
            SectionFooterTextBlock.Text = GetSectionFooter(section);
            _suspendFilterReload = true;
            SectionCodeFilterTextBox.Text = string.Empty;
            SectionNameFilterTextBox.Text = string.Empty;
            SectionAddressFilterTextBox.Text = string.Empty;
            _suspendFilterReload = false;

            if (section == NavigationSection.Products)
            {
                ProductsDataGrid.Items.Refresh();
                ShowProductsLayout();
                await ReloadCurrentSectionAsync();
                return;
            }

            if (section == NavigationSection.Warehouses)
            {
                WarehousesDataGrid.Items.Refresh();
                ShowWarehousesLayout();
                await ReloadCurrentSectionAsync();
                return;
            }

            ShowPlaceholderLayout(section);
        }

        private Task ReloadCurrentSectionAsync()
        {
            if (_currentSection == NavigationSection.Products)
            {
                return LoadProductsAsync(++_filterRequestVersion);
            }

            if (_currentSection == NavigationSection.Warehouses)
            {
                return LoadWarehousesAsync(++_filterRequestVersion);
            }

            return Task.CompletedTask;
        }

        private async Task LoadProductsAsync(int requestVersion)
        {
            string codeFilter = SectionCodeFilterTextBox.Text.Trim();
            string nameFilter = SectionNameFilterTextBox.Text.Trim();

            try
            {
                SectionStatusTextBlock.Text = "Ladowanie listy towarow...";

                List<ProductMasterItem> items = await _productCatalogService.LoadProductsAsync(codeFilter, nameFilter);
                if (requestVersion != _filterRequestVersion || _currentSection != NavigationSection.Products)
                {
                    return;
                }

                _products.Clear();
                foreach (ProductMasterItem item in items)
                {
                    _products.Add(item);
                }

                _productsView.Refresh();
                if (_products.Count == 0)
                {
                    SectionStatusTextBlock.Text = "Brak towarow do wyswietlenia.";
                }
                else
                {
                    SectionStatusTextBlock.Text = "Lista towarow. Rozwin wiersz, aby zobaczyc warianty cechy.";
                }
            }
            catch (Exception ex)
            {
                if (requestVersion != _filterRequestVersion || _currentSection != NavigationSection.Products)
                {
                    return;
                }

                SectionPlaceholderBorder.Visibility = Visibility.Visible;
                ProductsDataGrid.Visibility = Visibility.Collapsed;
                SectionPlaceholderTextBlock.Text = $"Nie udalo sie pobrac danych. {ex.Message}";
                SectionStatusTextBlock.Text = "Blad odczytu danych.";
            }
        }

        private async Task LoadWarehousesAsync(int requestVersion)
        {
            string codeFilter = SectionCodeFilterTextBox.Text.Trim();
            string nameFilter = SectionNameFilterTextBox.Text.Trim();
            string addressFilter = SectionAddressFilterTextBox.Text.Trim();

            try
            {
                SectionStatusTextBlock.Text = "Ladowanie listy magazynow...";

                List<WarehouseMasterItem> items = await _warehouseCatalogService.LoadWarehousesAsync(codeFilter, nameFilter, addressFilter);
                if (requestVersion != _filterRequestVersion || _currentSection != NavigationSection.Warehouses)
                {
                    return;
                }

                _warehouses.Clear();
                foreach (WarehouseMasterItem item in items)
                {
                    _warehouses.Add(item);
                }

                _warehousesView.Refresh();
                if (_warehouses.Count == 0)
                {
                    SectionStatusTextBlock.Text = "Brak magazynow do wyswietlenia.";
                }
                else
                {
                    SectionStatusTextBlock.Text = "Lista magazynow. Rozwin wiersz, aby doladowac sektory.";
                }
            }
            catch (Exception ex)
            {
                if (requestVersion != _filterRequestVersion || _currentSection != NavigationSection.Warehouses)
                {
                    return;
                }

                SectionPlaceholderBorder.Visibility = Visibility.Visible;
                WarehousesDataGrid.Visibility = Visibility.Collapsed;
                SectionPlaceholderTextBlock.Text = $"Nie udalo sie pobrac danych. {ex.Message}";
                SectionStatusTextBlock.Text = "Blad odczytu danych.";
            }
        }

        private async Task EnsureProductDetailsLoadedAsync(ProductMasterItem item)
        {
            if (item.DetailsLoaded)
            {
                return;
            }

            item.DetailStatus = "Ladowanie wariantow...";

            try
            {
                List<ProductVariantItem> items = await _productCatalogService.LoadVariantsAsync(item.Id);
                item.Variants.Clear();
                foreach (ProductVariantItem variant in items)
                {
                    item.Variants.Add(variant);
                }

                item.DetailsLoaded = true;
                item.DetailStatus = item.Variants.Count == 0
                    ? "Brak wariantow dla wybranego towaru."
                    : $"Warianty: {item.Variants.Count}";
            }
            catch (Exception ex)
            {
                item.DetailStatus = $"Blad ladowania wariantow: {ex.Message}";
            }
        }

        private async Task EnsureWarehouseDetailsLoadedAsync(WarehouseMasterItem item)
        {
            if (item.DetailsLoaded)
            {
                return;
            }

            item.DetailStatus = "Ladowanie sektorow...";

            try
            {
                List<SectorItem> items = await _warehouseCatalogService.LoadSectorsAsync(item.Id);
                item.Sectors.Clear();
                foreach (SectorItem sector in items)
                {
                    item.Sectors.Add(sector);
                }

                item.DetailsLoaded = true;
                item.DetailStatus = item.Sectors.Count == 0
                    ? "Brak sektorow dla wybranego magazynu."
                    : $"Sektory: {item.Sectors.Count}";
            }
            catch (Exception ex)
            {
                item.DetailStatus = $"Blad ladowania sektorow: {ex.Message}";
            }
        }

        private void ShowProductsLayout()
        {
            SectionToolbarPanel.Visibility = Visibility.Visible;
            SectionPlaceholderBorder.Visibility = Visibility.Collapsed;
            ProductsDataGrid.Visibility = Visibility.Visible;
            WarehousesDataGrid.Visibility = Visibility.Collapsed;
            SectionAddressFilterContainer.Visibility = Visibility.Collapsed;
            SectionAddressSpacerColumn.Width = new GridLength(0);
            SectionAddressFilterColumn.Width = new GridLength(0);
            SectionStatusTextBlock.Text = "Lista towarow z wariantami widocznymi od razu.";
        }

        private void ShowWarehousesLayout()
        {
            SectionToolbarPanel.Visibility = Visibility.Visible;
            SectionPlaceholderBorder.Visibility = Visibility.Collapsed;
            ProductsDataGrid.Visibility = Visibility.Collapsed;
            WarehousesDataGrid.Visibility = Visibility.Visible;
            SectionAddressFilterContainer.Visibility = Visibility.Visible;
            SectionAddressSpacerColumn.Width = new GridLength(10);
            SectionAddressFilterColumn.Width = new GridLength(200);
            SectionStatusTextBlock.Text = "Lista magazynow. Rozwin wiersz, aby doladowac sektory.";
        }

        private void ShowPlaceholderLayout(NavigationSection section)
        {
            SectionToolbarPanel.Visibility = Visibility.Collapsed;
            ProductsDataGrid.Visibility = Visibility.Collapsed;
            WarehousesDataGrid.Visibility = Visibility.Collapsed;
            SectionPlaceholderBorder.Visibility = Visibility.Visible;
            SectionPlaceholderTextBlock.Text = GetPlaceholderText(section);
            SectionStatusTextBlock.Text = "Widok jeszcze nie jest podlaczony do danych.";
        }

        private static string GetSectionTitle(NavigationSection section)
        {
            return section switch
            {
                NavigationSection.Documents => "Dokumenty",
                NavigationSection.Warehouses => "Magazyny",
                NavigationSection.Products => "Towary",
                NavigationSection.Traceability => "Traceability",
                _ => "Panel startowy"
            };
        }

        private static string GetSectionDescription(NavigationSection section)
        {
            return section switch
            {
                NavigationSection.Documents =>
                    "Obszar do prowadzenia dokumentow magazynowych oraz kontroli najwazniejszych etapow obiegu.",
                NavigationSection.Warehouses =>
                    "Lista magazynow z danymi z widoku SBD.MagazynyView. Szczegoly sektorow doladowuja sie dopiero po rozwinieciu wiersza.",
                NavigationSection.Products =>
                    "Lista towarow zgrupowana po indeksie. Warianty z cecha sa widoczne po rozwinieciu wybranego wiersza.",
                NavigationSection.Traceability =>
                    "Obszar do sledzenia pochodzenia, historii ruchu i powiazan pomiedzy operacjami.",
                _ => ""
            };
        }

        private static string GetSectionFooter(NavigationSection section)
        {
            return section switch
            {
                NavigationSection.Warehouses => "Kliknij naglowek kolumny, aby sortowac. Uzyj plusa w pierwszej kolumnie, aby doladowac sektory tylko dla wybranego magazynu.",
                NavigationSection.Products => "Kliknij naglowek kolumny, aby sortowac. Uzyj plusa w pierwszej kolumnie, aby doladowac warianty tylko dla wybranego towaru.",
                _ => "Widok zachowuje prosta nawigacje powrotu do panelu glownego."
            };
        }

        private static string GetPlaceholderText(NavigationSection section)
        {
            return section switch
            {
                NavigationSection.Documents => "Dokumenty zostawilem jeszcze bez podpiecia. Na tym etapie aktywne sa widoki Towary i Magazyny.",
                NavigationSection.Traceability => "Traceability zostawilem jeszcze bez podpiecia. Na tym etapie aktywne sa widoki Towary i Magazyny.",
                _ => "Ten widok nie ma jeszcze podlaczonej tabeli."
            };
        }
    }
}