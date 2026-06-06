using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SQLWMS.Models;
using SQLWMS.Services;

namespace SQLWMS
{
    public partial class MainWindow : Window
    {
        private const int DocumentsPageSize = 7;
        private const string TraceabilityBlankFeatureKey = "__BLANK__";

        private readonly DocumentCatalogService _documentCatalogService = new();
        private readonly ProductCatalogService _productCatalogService = new();
        private readonly TraceabilityService _traceabilityService = new();
        private readonly WarehouseCatalogService _warehouseCatalogService = new();
        private readonly UserSessionService _userSessionService = new();
        private readonly ObservableCollection<DocumentListItem> _documents = [];
        private readonly ObservableCollection<ProductMasterItem> _products = [];
        private readonly ObservableCollection<TraceabilityFilterOption> _traceabilityFeatureOptions = [];
        private readonly ObservableCollection<TraceabilityFilterOption> _traceabilityProductOptions = [];
        private readonly ObservableCollection<TraceabilityReportItem> _traceabilityItems = [];
        private readonly ObservableCollection<WarehouseMasterItem> _warehouses = [];
        private List<TraceabilityReportItem> _traceabilityScopeItems = [];
        private readonly ICollectionView _documentsView;
        private readonly ICollectionView _productsView;
        private readonly ICollectionView _warehousesView;
        private bool _isOpeningDocument;
        private bool _isExecutingDocumentAction;
        private bool _isTraceabilityFilterLoading;
        private int _documentsCurrentPage = 1;
        private int _documentsTotalCount;
        private int _filterRequestVersion;
        private bool _suspendFilterReload;
        private string _documentWarehouseFilter = string.Empty;
        private string _documentSectorFilter = string.Empty;
        private string _documentProductFilter = string.Empty;
        private string _documentSeriesFilter = string.Empty;
        private string _traceabilityLoadedDocumentNumber = string.Empty;
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

            _documentsView = CollectionViewSource.GetDefaultView(_documents);
            _productsView = CollectionViewSource.GetDefaultView(_products);
            _warehousesView = CollectionViewSource.GetDefaultView(_warehouses);
            DocumentsDataGrid.ItemsSource = _documentsView;
            ProductsDataGrid.ItemsSource = _productsView;
            TraceabilityFeatureComboBox.ItemsSource = _traceabilityFeatureOptions;
            TraceabilityProductComboBox.ItemsSource = _traceabilityProductOptions;
            TraceabilityResultsDataGrid.ItemsSource = _traceabilityItems;
            WarehousesDataGrid.ItemsSource = _warehousesView;

            ResetTraceabilityState(clearDocument: true);
            UpdateCurrentUserPresentation();
            UpdateDocumentAdvancedFilterButtons();
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

        private void DocumentsDataGridRow_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row)
            {
                row.IsSelected = true;
                row.Focus();
            }
        }

        private void DocumentsDataGrid_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject) is null)
            {
                DocumentsDataGrid.SelectedItem = null;
            }
        }

        private async void DocumentsDataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row)
            {
                row.IsSelected = true;
                row.Focus();
            }

            e.Handled = true;
            await OpenSelectedDocumentFromGridAsync();
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

        private void NestedDetailsDataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not DependencyObject source)
            {
                return;
            }

            DataGridRow? ownerRow = FindVisualParent<DataGridRow>(source);
            if (ownerRow is null)
            {
                return;
            }

            System.Windows.Controls.DataGrid? parentGrid = FindVisualParent<System.Windows.Controls.DataGrid>(ownerRow);
            if (parentGrid is null)
            {
                return;
            }

            ScrollViewer? scrollViewer = FindVisualParent<ScrollViewer>(parentGrid);
            if (scrollViewer is null)
            {
                return;
            }

            e.Handled = true;

            if (e.Delta > 0)
            {
                scrollViewer.LineUp();
                return;
            }

            if (e.Delta < 0)
            {
                scrollViewer.LineDown();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            ShowHome();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await PromptUserLoginAsync(forcePrompt: true);
        }

        private async void OpenSelectedDocumentMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await OpenSelectedDocumentFromGridAsync();
        }

        private async void CreateDocumentMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem { Tag: string documentType })
            {
                return;
            }

            if (!await PromptUserLoginAsync(forcePrompt: false))
            {
                return;
            }

            CreateDocumentWindow createDocumentWindow = new(documentType)
            {
                Owner = this
            };

            bool? dialogResult = createDocumentWindow.ShowDialog();
            if (dialogResult != true)
            {
                return;
            }

            try
            {
                DocumentCreateResult createResult = await _documentCatalogService.CreateDocumentAsync(new DocumentCreateRequest
                {
                    TypDokumentu = createDocumentWindow.DocumentType,
                    DataWystawienia = createDocumentWindow.DocumentDate,
                    Seria = createDocumentWindow.DocumentSeries,
                    Operator = _userSessionService.CurrentUser
                });

                if (!createResult.IsSuccess)
                {
                    AppDialogWindow.Show(this, "Zakladanie dokumentu", createResult.Message, AppDialogKind.Warning);
                    await ReloadCurrentSectionAsync();
                    return;
                }

                if (!createResult.DocumentId.HasValue)
                {
                    AppDialogWindow.Show(this, "Zakladanie dokumentu", "Procedura nie zwrocila identyfikatora nowego dokumentu.", AppDialogKind.Warning);
                    await ReloadCurrentSectionAsync();
                    return;
                }

                DocumentProcedureResult openResult = await _documentCatalogService.OpenDocumentAsync(createResult.DocumentId.Value, _userSessionService.CurrentUser);
                if (!openResult.IsSuccess)
                {
                    AppDialogWindow.Show(this, "Zakladanie dokumentu", openResult.Message, AppDialogKind.Warning);
                    await ReloadCurrentSectionAsync();
                    return;
                }

                await ShowDocumentWindowAsync(createResult.DocumentId.Value, isReadOnly: false, releaseLockOnFailure: true);
            }
            catch (Exception ex)
            {
                AppDialogWindow.Show(this, "Zakladanie dokumentu", ex.Message, AppDialogKind.Error);
                await ReloadCurrentSectionAsync();
            }
        }

        private async void ExecuteSelectedDocumentActionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem { Tag: string action })
            {
                return;
            }

            await ExecuteSelectedDocumentActionFromGridAsync(action);
        }

        private async Task<bool> PromptUserLoginAsync(bool forcePrompt)
        {
            if (!forcePrompt && _userSessionService.HasUser)
            {
                return true;
            }

            LoginWindow loginWindow = new(_userSessionService.CurrentUser)
            {
                Owner = this
            };

            bool? result = loginWindow.ShowDialog();
            if (result != true)
            {
                return false;
            }

            _userSessionService.SaveCurrentUser(loginWindow.UserName);
            UpdateCurrentUserPresentation();

            if (_currentSection == NavigationSection.Documents)
            {
                await ReloadCurrentSectionAsync();
            }

            return true;
        }

        private async void SectionFilterTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_suspendFilterReload)
            {
                return;
            }

            if (_currentSection == NavigationSection.Documents)
            {
                _documentsCurrentPage = 1;
            }

            await ReloadCurrentSectionAsync();
        }

        private async void DocumentFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suspendFilterReload)
            {
                return;
            }

            _documentsCurrentPage = 1;
            await ReloadCurrentSectionAsync();
        }

        private async void DocumentAdvancedFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            DocumentAdvancedFiltersWindow filtersWindow = new(
                _documentCatalogService,
                _documentWarehouseFilter,
                _documentSectorFilter,
                _documentProductFilter,
                _documentSeriesFilter)
            {
                Owner = this
            };

            bool? result = filtersWindow.ShowDialog();
            if (result != true)
            {
                return;
            }

            _documentWarehouseFilter = filtersWindow.SelectedWarehouseCode;
            _documentSectorFilter = filtersWindow.SelectedSectorCode;
            _documentProductFilter = filtersWindow.SelectedProductCode;
            _documentSeriesFilter = filtersWindow.SelectedSeries;
            _documentsCurrentPage = 1;
            UpdateDocumentAdvancedFilterButtons();
            await ReloadCurrentSectionAsync();
        }

        private async void DocumentAdvancedFiltersResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (!HasDocumentAdvancedFilters())
            {
                return;
            }

            ResetDocumentAdvancedFilters();
            _documentsCurrentPage = 1;
            UpdateDocumentAdvancedFilterButtons();
            await ReloadCurrentSectionAsync();
        }

        private async void DocumentsPreviousPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_documentsCurrentPage <= 1)
            {
                return;
            }

            _documentsCurrentPage--;
            await ReloadCurrentSectionAsync();
        }

        private async void DocumentsNextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_documentsCurrentPage >= GetDocumentsTotalPages())
            {
                return;
            }

            _documentsCurrentPage++;
            await ReloadCurrentSectionAsync();
        }

        private void ShowHome()
        {
            _currentSection = NavigationSection.Home;

            HomeView.Visibility = Visibility.Visible;
            SectionView.Visibility = Visibility.Collapsed;
            HomeOverviewPanel.Visibility = Visibility.Visible;
            BackButton.Visibility = Visibility.Collapsed;
            LoginButton.Visibility = Visibility.Visible;
        }

        private async Task NavigateToAsync(NavigationSection section)
        {
            _currentSection = section;

            HomeView.Visibility = Visibility.Collapsed;
            SectionView.Visibility = Visibility.Visible;
            HomeOverviewPanel.Visibility = Visibility.Collapsed;
            BackButton.Visibility = Visibility.Visible;
            LoginButton.Visibility = Visibility.Collapsed;
            SectionTitleTextBlock.Text = GetSectionTitle(section);
            SectionDescriptionTextBlock.Text = GetSectionDescription(section);
            SectionFooterTextBlock.Text = GetSectionFooter(section);
            _suspendFilterReload = true;
            SectionCodeFilterTextBox.Text = string.Empty;
            SectionNameFilterTextBox.Text = string.Empty;
            SectionAddressFilterTextBox.Text = string.Empty;
            DocumentNumberFilterTextBox.Text = string.Empty;
            DocumentTypeFilterComboBox.SelectedIndex = 0;
            DocumentStatusFilterComboBox.SelectedIndex = 0;
            ResetDocumentAdvancedFilters();
            ResetTraceabilityState(clearDocument: true);
            _documentsCurrentPage = 1;
            _suspendFilterReload = false;
            UpdateDocumentAdvancedFilterButtons();

            SectionPlaceholderBorder.Visibility = Visibility.Collapsed;

            if (section == NavigationSection.Documents)
            {
                DocumentsDataGrid.Items.Refresh();
                ShowDocumentsLayout();
                await ReloadCurrentSectionAsync();
                return;
            }

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

            if (section == NavigationSection.Traceability)
            {
                TraceabilityResultsDataGrid.Items.Refresh();
                ShowTraceabilityLayout();
                return;
            }

            ShowPlaceholderLayout(section);
        }

        private Task ReloadCurrentSectionAsync()
        {
            if (_currentSection == NavigationSection.Documents)
            {
                return LoadDocumentsAsync(++_filterRequestVersion);
            }

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

        private async Task LoadDocumentsAsync(int requestVersion)
        {
            string documentNumberFilter = DocumentNumberFilterTextBox.Text.Trim();
            string documentTypeFilter = GetSelectedFilterValue(DocumentTypeFilterComboBox);
            string documentStatusFilter = GetSelectedFilterValue(DocumentStatusFilterComboBox);

            try
            {
                SectionStatusTextBlock.Text = "Ladowanie listy dokumentow...";

                DocumentPageResult page = await _documentCatalogService.LoadDocumentsAsync(
                    _documentsCurrentPage,
                    DocumentsPageSize,
                    documentNumberFilter,
                    documentTypeFilter,
                    documentStatusFilter,
                    _documentWarehouseFilter,
                    _documentSectorFilter,
                    _documentProductFilter,
                    _documentSeriesFilter);

                if (requestVersion != _filterRequestVersion || _currentSection != NavigationSection.Documents)
                {
                    return;
                }

                _documents.Clear();
                foreach (DocumentListItem item in page.Items)
                {
                    item.IsOpenedByCurrentUser = item.IsOpened
                        && string.Equals(item.OtworzonyPrzez, _userSessionService.CurrentUser, StringComparison.OrdinalIgnoreCase);
                    _documents.Add(item);
                }

                _documentsTotalCount = page.TotalCount;
                _documentsView.Refresh();
                UpdateDocumentsPagination();

                if (_documents.Count == 0)
                {
                    SectionStatusTextBlock.Text = "Brak dokumentow dla aktualnych filtrow.";
                }
                else
                {
                    SectionStatusTextBlock.Text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                if (requestVersion != _filterRequestVersion || _currentSection != NavigationSection.Documents)
                {
                    return;
                }

                SectionPlaceholderBorder.Visibility = Visibility.Visible;
                DocumentsDataGrid.Visibility = Visibility.Collapsed;
                DocumentsPaginationPanel.Visibility = Visibility.Collapsed;
                SectionPlaceholderTextBlock.Text = $"Nie udalo sie pobrac dokumentow. {ex.Message}";
                SectionStatusTextBlock.Text = "Blad odczytu danych.";
            }
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

        private async void TraceabilitySelectDocumentButton_Click(object sender, RoutedEventArgs e)
        {
            DocumentPickerWindow pickerWindow = new(_documentCatalogService)
            {
                Owner = this
            };

            bool? result = pickerWindow.ShowDialog();
            if (result != true)
            {
                return;
            }

            await LoadTraceabilityReportAsync(pickerWindow.SelectedDocumentNumber);
        }

        private void TraceabilityClearButton_Click(object sender, RoutedEventArgs e)
        {
            ResetTraceabilityState(clearDocument: true);
        }

        private void TraceabilityProductComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isTraceabilityFilterLoading)
            {
                return;
            }

            string selectedFeature = GetSelectedTraceabilityOptionValue(TraceabilityFeatureComboBox);
            PopulateTraceabilityFeatureOptions(_traceabilityScopeItems, GetSelectedTraceabilityOptionValue(TraceabilityProductComboBox), selectedFeature);
            ApplyTraceabilityFilters();
        }

        private void TraceabilityFeatureComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isTraceabilityFilterLoading)
            {
                return;
            }

            ApplyTraceabilityFilters();
        }

        private void TraceabilityResultsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TraceabilityResultsDataGrid.SelectedItem is not TraceabilityReportItem item)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(item.Path))
            {
                return;
            }

            AppDialogWindow.Show(this, "Pelna sciezka traceability", item.Path, AppDialogKind.Information);
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

        private static string GetSelectedFilterValue(System.Windows.Controls.ComboBox comboBox)
        {
            return comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item
                ? Convert.ToString(item.Tag) ?? string.Empty
                : string.Empty;
        }

        private async Task LoadTraceabilityReportAsync(string documentNumber)
        {
            if (string.IsNullOrWhiteSpace(documentNumber))
            {
                SectionStatusTextBlock.Text = "Wybierz dokument startowy, aby pobrac raport traceability.";
                return;
            }

            try
            {
                SetTraceabilityBusy(true);
                SectionStatusTextBlock.Text = "Budowanie raportu traceability...";
                TraceabilitySelectedDocumentTextBlock.Text = documentNumber;
                TraceabilitySelectedDocumentTextBlock.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryBrush");

                string selectedProduct = GetSelectedTraceabilityOptionValue(TraceabilityProductComboBox);
                string selectedFeature = GetSelectedTraceabilityOptionValue(TraceabilityFeatureComboBox);
                List<TraceabilityReportItem> scopeItems = await _traceabilityService.LoadReportAsync(documentNumber);

                _traceabilityLoadedDocumentNumber = documentNumber;
                _traceabilityScopeItems = scopeItems;
                PopulateTraceabilityProductOptions(scopeItems, selectedProduct);
                PopulateTraceabilityFeatureOptions(scopeItems, GetSelectedTraceabilityOptionValue(TraceabilityProductComboBox), selectedFeature);
                ApplyTraceabilityFilters();

                SectionStatusTextBlock.Text = _traceabilityItems.Count == 0
                    ? $"Raport nie zwrocil etapow dla dokumentu {documentNumber}."
                    : "Raport zaladowany. Zmiana towaru i cechy zawęza wynik od razu.";
            }
            catch (Exception ex)
            {
                _traceabilityLoadedDocumentNumber = string.Empty;
                _traceabilityScopeItems = [];
                _traceabilityItems.Clear();
                ResetTraceabilityFilterOptions();
                UpdateTraceabilitySummary(string.Empty, _traceabilityItems);
                TraceabilitySelectedDocumentTextBlock.Text = documentNumber;
                TraceabilitySelectedDocumentTextBlock.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryBrush");
                SectionStatusTextBlock.Text = $"Nie udalo sie pobrac raportu traceability. {ex.Message}";
            }
            finally
            {
                SetTraceabilityBusy(false);
            }
        }

        private void ApplyTraceabilityFilters()
        {
            IEnumerable<TraceabilityReportItem> items = _traceabilityScopeItems;
            string selectedProduct = GetSelectedTraceabilityOptionValue(TraceabilityProductComboBox);
            string selectedFeature = GetSelectedTraceabilityOptionValue(TraceabilityFeatureComboBox);

            if (!string.IsNullOrWhiteSpace(selectedProduct))
            {
                items = items.Where(item => string.Equals(item.ProductCode, selectedProduct, StringComparison.OrdinalIgnoreCase));
            }

            if (selectedFeature == TraceabilityBlankFeatureKey)
            {
                items = items.Where(item => string.IsNullOrWhiteSpace(item.Feature));
            }
            else if (!string.IsNullOrWhiteSpace(selectedFeature))
            {
                items = items.Where(item => string.Equals(item.Feature, selectedFeature, StringComparison.OrdinalIgnoreCase));
            }

            List<TraceabilityReportItem> filteredItems = items.ToList();

            _traceabilityItems.Clear();
            foreach (TraceabilityReportItem item in filteredItems)
            {
                _traceabilityItems.Add(item);
            }

            if (_traceabilityItems.Count > 0)
            {
                TraceabilityResultsDataGrid.SelectedIndex = 0;
            }

            UpdateTraceabilitySummary(_traceabilityLoadedDocumentNumber, filteredItems);

            if (!string.IsNullOrWhiteSpace(_traceabilityLoadedDocumentNumber))
            {
                SectionStatusTextBlock.Text = filteredItems.Count == 0
                    ? "Brak etapow dla aktualnie wybranego towaru i cechy."
                    : $"Raport pokazuje {filteredItems.Count} etap(y) dla aktywnego zakresu.";
            }
        }

        private void PopulateTraceabilityProductOptions(IEnumerable<TraceabilityReportItem> items, string selectedProduct)
        {
            bool previousLoadingState = _isTraceabilityFilterLoading;
            _isTraceabilityFilterLoading = true;

            _traceabilityProductOptions.Clear();
            _traceabilityProductOptions.Add(new TraceabilityFilterOption(string.Empty, "Wszystkie towary"));

            foreach (TraceabilityReportItem item in items
                         .Where(item => !string.IsNullOrWhiteSpace(item.ProductCode))
                         .GroupBy(item => item.ProductCode, StringComparer.OrdinalIgnoreCase)
                         .Select(group => group.First())
                         .OrderBy(item => item.ProductCode))
            {
                _traceabilityProductOptions.Add(new TraceabilityFilterOption(item.ProductCode, item.ProductDisplay));
            }

            TraceabilityProductComboBox.SelectedValue = selectedProduct;
            if (TraceabilityProductComboBox.SelectedIndex < 0)
            {
                TraceabilityProductComboBox.SelectedIndex = 0;
            }

            TraceabilityProductComboBox.IsEnabled = _traceabilityProductOptions.Count > 1;
            _isTraceabilityFilterLoading = previousLoadingState;
        }

        private void PopulateTraceabilityFeatureOptions(IEnumerable<TraceabilityReportItem> items, string selectedProduct, string selectedFeature)
        {
            bool previousLoadingState = _isTraceabilityFilterLoading;
            _isTraceabilityFilterLoading = true;

            IEnumerable<TraceabilityReportItem> source = items;
            if (!string.IsNullOrWhiteSpace(selectedProduct))
            {
                source = source.Where(item => string.Equals(item.ProductCode, selectedProduct, StringComparison.OrdinalIgnoreCase));
            }

            _traceabilityFeatureOptions.Clear();
            _traceabilityFeatureOptions.Add(new TraceabilityFilterOption(string.Empty, "Wszystkie cechy"));

            bool hasBlankFeature = false;
            foreach (TraceabilityReportItem item in source.OrderBy(item => item.DisplayFeature))
            {
                if (string.IsNullOrWhiteSpace(item.Feature))
                {
                    hasBlankFeature = true;
                    continue;
                }

                if (_traceabilityFeatureOptions.Any(option => string.Equals(option.Value, item.Feature, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                _traceabilityFeatureOptions.Add(new TraceabilityFilterOption(item.Feature!, item.Feature!));
            }

            if (hasBlankFeature)
            {
                _traceabilityFeatureOptions.Add(new TraceabilityFilterOption(TraceabilityBlankFeatureKey, "Brak cechy"));
            }

            TraceabilityFeatureComboBox.SelectedValue = selectedFeature;
            if (TraceabilityFeatureComboBox.SelectedIndex < 0)
            {
                TraceabilityFeatureComboBox.SelectedIndex = 0;
            }

            TraceabilityFeatureComboBox.IsEnabled = _traceabilityFeatureOptions.Count > 1;
            _isTraceabilityFilterLoading = previousLoadingState;
        }

        private void ResetTraceabilityFilterOptions()
        {
            bool previousLoadingState = _isTraceabilityFilterLoading;
            _isTraceabilityFilterLoading = true;

            _traceabilityProductOptions.Clear();
            _traceabilityProductOptions.Add(new TraceabilityFilterOption(string.Empty, "Wszystkie towary"));
            TraceabilityProductComboBox.SelectedIndex = 0;
            TraceabilityProductComboBox.IsEnabled = false;

            _traceabilityFeatureOptions.Clear();
            _traceabilityFeatureOptions.Add(new TraceabilityFilterOption(string.Empty, "Wszystkie cechy"));
            TraceabilityFeatureComboBox.SelectedIndex = 0;
            TraceabilityFeatureComboBox.IsEnabled = false;

            _isTraceabilityFilterLoading = previousLoadingState;
        }

        private void ResetTraceabilityState(bool clearDocument)
        {
            bool previousLoadingState = _isTraceabilityFilterLoading;
            _isTraceabilityFilterLoading = true;

            if (clearDocument)
            {
                TraceabilitySelectedDocumentTextBlock.Text = "Nie wybrano dokumentu startowego";
                TraceabilitySelectedDocumentTextBlock.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
            }

            _traceabilityLoadedDocumentNumber = string.Empty;
            _traceabilityScopeItems = [];
            _traceabilityItems.Clear();
            ResetTraceabilityFilterOptions();
            UpdateTraceabilitySummary(string.Empty, _traceabilityItems);

            _isTraceabilityFilterLoading = previousLoadingState;

            if (_currentSection == NavigationSection.Traceability || clearDocument)
            {
                SectionStatusTextBlock.Text = "Wybierz dokument startowy, aby zbudowac raport traceability.";
            }
        }

        private void InvalidateTraceabilityResults()
        {
            _traceabilityLoadedDocumentNumber = string.Empty;
            _traceabilityScopeItems = [];
            _traceabilityItems.Clear();
            ResetTraceabilityFilterOptions();
            UpdateTraceabilitySummary(string.Empty, _traceabilityItems);

            if (_currentSection == NavigationSection.Traceability)
            {
                SectionStatusTextBlock.Text = string.IsNullOrWhiteSpace(TraceabilitySelectedDocumentTextBlock.Text)
                    || string.Equals(TraceabilitySelectedDocumentTextBlock.Text, "Nie wybrano dokumentu startowego", StringComparison.OrdinalIgnoreCase)
                    ? "Wybierz dokument startowy, aby zbudowac raport traceability."
                    : "Wybierz dokument ponownie, aby przeliczyc traceability dla innego startu.";
            }
        }

        private void SetTraceabilityBusy(bool isBusy)
        {
            TraceabilitySelectDocumentButton.IsEnabled = !isBusy;
            TraceabilityClearButton.IsEnabled = !isBusy;
            TraceabilityProductComboBox.IsEnabled = !isBusy && _traceabilityProductOptions.Count > 1;
            TraceabilityFeatureComboBox.IsEnabled = !isBusy && _traceabilityFeatureOptions.Count > 1;
        }

        private void UpdateTraceabilitySummary(string documentNumber, IEnumerable<TraceabilityReportItem> items)
        {
            List<TraceabilityReportItem> rows = items.ToList();
            TraceabilityScopeValueTextBlock.Text = string.IsNullOrWhiteSpace(documentNumber) ? "Brak raportu" : documentNumber;
            TraceabilityScopeFiltersTextBlock.Text = string.IsNullOrWhiteSpace(documentNumber)
                ? "Wybierz dokument startowy, aby zobaczyc przeplyw dostaw i powiazan."
                : BuildTraceabilityFilterSummary();
            TraceabilityRowsMetricTextBlock.Text = rows.Count.ToString();
            TraceabilityProductsMetricTextBlock.Text = rows
                .Where(item => !string.IsNullOrWhiteSpace(item.ProductCode))
                .Select(item => item.ProductCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count()
                .ToString();
            TraceabilityDepthMetricTextBlock.Text = rows.Count == 0
                ? "0"
                : rows.Max(item => item.Level).ToString();
        }

        private string BuildTraceabilityFilterSummary()
        {
            List<string> parts = [];
            string selectedProduct = GetSelectedTraceabilityOptionValue(TraceabilityProductComboBox);
            string selectedFeature = GetSelectedTraceabilityOptionValue(TraceabilityFeatureComboBox);

            if (!string.IsNullOrWhiteSpace(selectedProduct))
            {
                parts.Add($"Towar: {selectedProduct}");
            }

            if (selectedFeature == TraceabilityBlankFeatureKey)
            {
                parts.Add("Cecha: Brak");
            }
            else if (!string.IsNullOrWhiteSpace(selectedFeature))
            {
                parts.Add($"Cecha: {selectedFeature}");
            }

            return parts.Count == 0
                ? "Zakres: wszystkie powiazane towary i cechy dla wybranego dokumentu."
                : $"Zakres: {string.Join(" | ", parts)}";
        }

        private static string GetSelectedTraceabilityOptionValue(System.Windows.Controls.ComboBox comboBox)
        {
            return comboBox.SelectedItem is TraceabilityFilterOption item
                ? item.Value
                : string.Empty;
        }

        private void ResetDocumentAdvancedFilters()
        {
            _documentWarehouseFilter = string.Empty;
            _documentSectorFilter = string.Empty;
            _documentProductFilter = string.Empty;
            _documentSeriesFilter = string.Empty;
        }

        private bool HasDocumentAdvancedFilters()
        {
            return !string.IsNullOrWhiteSpace(_documentWarehouseFilter)
                || !string.IsNullOrWhiteSpace(_documentSectorFilter)
                || !string.IsNullOrWhiteSpace(_documentProductFilter)
                || !string.IsNullOrWhiteSpace(_documentSeriesFilter);
        }

        private void UpdateDocumentAdvancedFilterButtons()
        {
            if (DocumentAdvancedFiltersButton is null || DocumentAdvancedFiltersResetButton is null)
            {
                return;
            }

            bool hasFilters = HasDocumentAdvancedFilters();
            DocumentAdvancedFiltersButton.ToolTip = hasFilters
                ? BuildDocumentAdvancedFiltersTooltip()
                : "Dodatkowe filtry po magazynie, sektorze, towarze i serii";
            DocumentAdvancedFiltersResetButton.IsEnabled = hasFilters;
            DocumentAdvancedFiltersResetButton.Opacity = hasFilters ? 1 : 0.5;
        }

        private string BuildDocumentAdvancedFiltersTooltip()
        {
            List<string> parts = [];

            if (!string.IsNullOrWhiteSpace(_documentWarehouseFilter))
            {
                parts.Add($"Magazyn: {_documentWarehouseFilter}");
            }

            if (!string.IsNullOrWhiteSpace(_documentSectorFilter))
            {
                parts.Add($"Sektor: {_documentSectorFilter}");
            }

            if (!string.IsNullOrWhiteSpace(_documentProductFilter))
            {
                parts.Add($"Towar: {_documentProductFilter}");
            }

            if (!string.IsNullOrWhiteSpace(_documentSeriesFilter))
            {
                parts.Add($"Seria: {_documentSeriesFilter}");
            }

            return string.Join(Environment.NewLine, parts);
        }

        private async Task OpenSelectedDocumentFromGridAsync()
        {
            if (_isOpeningDocument)
            {
                return;
            }

            if (DocumentsDataGrid.SelectedItem is not DocumentListItem selectedDocument)
            {
                return;
            }

            if (!await PromptUserLoginAsync(forcePrompt: false))
            {
                return;
            }

            _isOpeningDocument = true;
            try
            {
                await OpenDocumentAsync(selectedDocument.Id);
            }
            finally
            {
                _isOpeningDocument = false;
            }
        }

        private async Task ExecuteSelectedDocumentActionFromGridAsync(string action)
        {
            if (_isExecutingDocumentAction)
            {
                return;
            }

            if (DocumentsDataGrid.SelectedItem is not DocumentListItem selectedDocument)
            {
                return;
            }

            if (!CanExecuteDocumentAction(selectedDocument, action))
            {
                return;
            }

            if (!await PromptUserLoginAsync(forcePrompt: false))
            {
                return;
            }

            if (!ConfirmDocumentAction(action))
            {
                return;
            }

            _isExecutingDocumentAction = true;
            bool lockAcquired = false;

            try
            {
                DocumentProcedureResult openResult = await _documentCatalogService.OpenDocumentAsync(selectedDocument.Id, _userSessionService.CurrentUser);
                if (!openResult.IsSuccess)
                {
                    AppDialogWindow.Show(this, GetDocumentActionTitle(action), openResult.Message, AppDialogKind.Warning);
                    await ReloadCurrentSectionAsync();
                    return;
                }

                lockAcquired = true;

                DocumentProcedureResult actionResult = await _documentCatalogService.CloseDocumentAsync(selectedDocument.Id, _userSessionService.CurrentUser, action);
                if (!actionResult.IsSuccess)
                {
                    await ReleaseDocumentLockAsync(selectedDocument.Id);
                    lockAcquired = false;
                    AppDialogWindow.Show(this, GetDocumentActionTitle(action), actionResult.Message, AppDialogKind.Warning);
                    await ReloadCurrentSectionAsync();
                    return;
                }

                await ReloadCurrentSectionAsync();
            }
            catch (Exception ex)
            {
                if (lockAcquired)
                {
                    await ReleaseDocumentLockAsync(selectedDocument.Id);
                    lockAcquired = false;
                }

                AppDialogWindow.Show(this, GetDocumentActionTitle(action), ex.Message, AppDialogKind.Error);
                await ReloadCurrentSectionAsync();
            }
            finally
            {
                if (lockAcquired)
                {
                    await ReleaseDocumentLockAsync(selectedDocument.Id);
                }

                _isExecutingDocumentAction = false;
            }
        }

        private async Task OpenDocumentAsync(int documentId)
        {
            DocumentProcedureResult openResult = await _documentCatalogService.OpenDocumentAsync(documentId, _userSessionService.CurrentUser);
            bool isReadOnly = false;

            if (!openResult.IsSuccess)
            {
                if (!openResult.IsLockedByOtherUser)
                {
                    AppDialogWindow.Show(this, "Otwieranie dokumentu", openResult.Message, AppDialogKind.Warning);
                    return;
                }

                isReadOnly = true;
            }

            await ShowDocumentWindowAsync(documentId, isReadOnly, releaseLockOnFailure: openResult.IsSuccess);
        }

        private async Task ShowDocumentWindowAsync(int documentId, bool isReadOnly, bool releaseLockOnFailure)
        {
            try
            {
                DocumentDetailsItem? details = await _documentCatalogService.LoadDocumentDetailsAsync(documentId);
                if (details is null)
                {
                    if (releaseLockOnFailure)
                    {
                        await _documentCatalogService.CloseDocumentAsync(documentId, _userSessionService.CurrentUser);
                    }

                    AppDialogWindow.Show(this, "Otwieranie dokumentu", "Nie udalo sie pobrac szczegolow dokumentu.", AppDialogKind.Warning);
                    await ReloadCurrentSectionAsync();
                    return;
                }

                List<DocumentPositionItem> positions = await _documentCatalogService.LoadDocumentPositionsAsync(documentId);
                List<WarehouseLookupItem> warehouses = isReadOnly
                    ? []
                    : await _documentCatalogService.LoadWarehouseLookupAsync();
                List<SectorLookupItem> sectors = isReadOnly
                    ? []
                    : await _documentCatalogService.LoadSectorLookupAsync(null);

                string readOnlyMessage = isReadOnly
                    ? $"Dokument jest zablokowany przez '{details.OtworzonyPrzez}'. Widok tylko do odczytu."
                    : string.Empty;

                DocumentWindow documentWindow = new(
                    _documentCatalogService,
                    _userSessionService.CurrentUser,
                    details,
                    positions,
                    warehouses,
                    sectors,
                    isReadOnly,
                    readOnlyMessage)
                {
                    Owner = this
                };

                documentWindow.ShowDialog();
                await ReloadCurrentSectionAsync();
            }
            catch (Exception ex)
            {
                if (releaseLockOnFailure)
                {
                    await _documentCatalogService.CloseDocumentAsync(documentId, _userSessionService.CurrentUser);
                }

                AppDialogWindow.Show(this, "Otwieranie dokumentu", ex.Message, AppDialogKind.Error);
                await ReloadCurrentSectionAsync();
            }
        }

        private int GetDocumentsTotalPages()
        {
            return Math.Max(1, (int)Math.Ceiling(_documentsTotalCount / (double)DocumentsPageSize));
        }

        private static bool CanExecuteDocumentAction(DocumentListItem document, string action)
        {
            if (document.IsOpened)
            {
                return false;
            }

            string status = document.StatusDokumentu.Trim();
            return action switch
            {
                "Usun" or "Zatwierdz" => string.Equals(status, "Szkic", StringComparison.OrdinalIgnoreCase),
                "Anuluj" => string.Equals(status, "Zatwierdzony", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        private bool ConfirmDocumentAction(string action)
        {
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

            return AppDialogWindow.Confirm(this, GetDocumentActionTitle(action), message, confirmText, "Nie", AppDialogKind.Warning);
        }

        private async Task ReleaseDocumentLockAsync(int documentId)
        {
            try
            {
                await _documentCatalogService.CloseDocumentAsync(documentId, _userSessionService.CurrentUser);
            }
            catch
            {
            }
        }

        private static string GetDocumentActionTitle(string action)
        {
            return action switch
            {
                "Usun" => "Usuwanie dokumentu",
                "Zatwierdz" => "Zatwierdzanie dokumentu",
                "Anuluj" => "Anulowanie dokumentu",
                _ => "Potwierdzenie akcji"
            };
        }

        private void UpdateDocumentsPagination()
        {
            int totalPages = GetDocumentsTotalPages();
            DocumentsPaginationTextBlock.Text = $"Strona {_documentsCurrentPage} z {totalPages}";
            DocumentsResultsTextBlock.Text = _documentsTotalCount == 1
                ? "1 rekord"
                : $"{_documentsTotalCount} rekordow";
            DocumentsPreviousPageButton.IsEnabled = _documentsCurrentPage > 1;
            DocumentsNextPageButton.IsEnabled = _documentsCurrentPage < totalPages;
            DocumentsPaginationPanel.Visibility = Visibility.Visible;
        }

        private void UpdateCurrentUserPresentation()
        {
            if (_userSessionService.HasUser)
            {
                CurrentUserTextBlock.Text = $"Zalogowany: {_userSessionService.CurrentUser}";
                LoginButton.Content = "Zmien login";
                return;
            }

            CurrentUserTextBlock.Text = "Brak aktywnego uzytkownika";
            LoginButton.Content = "Zaloguj";
        }

        private void ShowProductsLayout()
        {
            DocumentsToolbarPanel.Visibility = Visibility.Collapsed;
            TraceabilityToolbarPanel.Visibility = Visibility.Collapsed;
            DocumentsPaginationPanel.Visibility = Visibility.Collapsed;
            SectionToolbarPanel.Visibility = Visibility.Visible;
            SectionPlaceholderBorder.Visibility = Visibility.Collapsed;
            DocumentsDataGrid.Visibility = Visibility.Collapsed;
            ProductsDataGrid.Visibility = Visibility.Visible;
            TraceabilityContentGrid.Visibility = Visibility.Collapsed;
            WarehousesDataGrid.Visibility = Visibility.Collapsed;
            SectionAddressFilterContainer.Visibility = Visibility.Collapsed;
            SectionAddressSpacerColumn.Width = new GridLength(0);
            SectionAddressFilterColumn.Width = new GridLength(0);
            SectionStatusTextBlock.Text = "Lista towarow z wariantami widocznymi od razu.";
        }

        private void ShowWarehousesLayout()
        {
            DocumentsToolbarPanel.Visibility = Visibility.Collapsed;
            TraceabilityToolbarPanel.Visibility = Visibility.Collapsed;
            DocumentsPaginationPanel.Visibility = Visibility.Collapsed;
            SectionToolbarPanel.Visibility = Visibility.Visible;
            SectionPlaceholderBorder.Visibility = Visibility.Collapsed;
            DocumentsDataGrid.Visibility = Visibility.Collapsed;
            ProductsDataGrid.Visibility = Visibility.Collapsed;
            TraceabilityContentGrid.Visibility = Visibility.Collapsed;
            WarehousesDataGrid.Visibility = Visibility.Visible;
            SectionAddressFilterContainer.Visibility = Visibility.Visible;
            SectionAddressSpacerColumn.Width = new GridLength(10);
            SectionAddressFilterColumn.Width = new GridLength(200);
            SectionStatusTextBlock.Text = "Lista magazynow. Rozwin wiersz, aby doladowac sektory.";
        }

        private void ShowDocumentsLayout()
        {
            DocumentsToolbarPanel.Visibility = Visibility.Visible;
            TraceabilityToolbarPanel.Visibility = Visibility.Collapsed;
            SectionToolbarPanel.Visibility = Visibility.Collapsed;
            SectionPlaceholderBorder.Visibility = Visibility.Collapsed;
            DocumentsDataGrid.Visibility = Visibility.Visible;
            ProductsDataGrid.Visibility = Visibility.Collapsed;
            TraceabilityContentGrid.Visibility = Visibility.Collapsed;
            WarehousesDataGrid.Visibility = Visibility.Collapsed;
            DocumentsPaginationPanel.Visibility = Visibility.Visible;
            SectionStatusTextBlock.Text = string.Empty;
        }

        private void ShowTraceabilityLayout()
        {
            DocumentsToolbarPanel.Visibility = Visibility.Collapsed;
            TraceabilityToolbarPanel.Visibility = Visibility.Visible;
            SectionToolbarPanel.Visibility = Visibility.Collapsed;
            DocumentsPaginationPanel.Visibility = Visibility.Collapsed;
            SectionPlaceholderBorder.Visibility = Visibility.Collapsed;
            DocumentsDataGrid.Visibility = Visibility.Collapsed;
            ProductsDataGrid.Visibility = Visibility.Collapsed;
            TraceabilityContentGrid.Visibility = Visibility.Visible;
            WarehousesDataGrid.Visibility = Visibility.Collapsed;
            SectionStatusTextBlock.Text = string.IsNullOrWhiteSpace(_traceabilityLoadedDocumentNumber)
                ? "Wybierz dokument startowy, aby zbudowac raport traceability."
                : SectionStatusTextBlock.Text;
        }

        private void ShowPlaceholderLayout(NavigationSection section)
        {
            DocumentsToolbarPanel.Visibility = Visibility.Collapsed;
            TraceabilityToolbarPanel.Visibility = Visibility.Collapsed;
            SectionToolbarPanel.Visibility = Visibility.Collapsed;
            DocumentsPaginationPanel.Visibility = Visibility.Collapsed;
            DocumentsDataGrid.Visibility = Visibility.Collapsed;
            ProductsDataGrid.Visibility = Visibility.Collapsed;
            TraceabilityContentGrid.Visibility = Visibility.Collapsed;
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
                    "Lista dokumentow magazynowych z filtrowaniem po numerze, typie, statusie oraz dodatkowymi filtrami magazynu, sektora i towaru.",
                NavigationSection.Warehouses =>
                    "Lista magazynow z danymi z widoku SBD.MagazynyView. Szczegoly sektorow doladowuja sie dopiero po rozwinieciu wiersza.",
                NavigationSection.Products =>
                    "Lista towarow zgrupowana po indeksie. Warianty z cecha sa widoczne po rozwinieciu wybranego wiersza.",
                NavigationSection.Traceability =>
                    "Raport przeplywu dostaw i alokacji. Zacznij od dokumentu startowego, a potem zawez wynik po towarze i cesze.",
                _ => ""
            };
        }

        private static string GetSectionFooter(NavigationSection section)
        {
            return section switch
            {
                NavigationSection.Documents => "Uzyj filtrow nad tabela i przycisku Filtry, a do przechodzenia pomiedzy stronami skorzystaj z pagera pod lista.",
                NavigationSection.Warehouses => "Kliknij naglowek kolumny, aby sortowac. Uzyj plusa w pierwszej kolumnie, aby doladowac sektory tylko dla wybranego magazynu.",
                NavigationSection.Products => "Kliknij naglowek kolumny, aby sortowac. Uzyj plusa w pierwszej kolumnie, aby doladowac warianty tylko dla wybranego towaru.",
                NavigationSection.Traceability => "Najpierw wybierz dokument startowy. Potem wybieraj towar i ceche, aby interaktywnie zwezac trase powiazan.",
                _ => "Widok zachowuje prosta nawigacje powrotu do panelu glownego."
            };
        }

        private static string GetPlaceholderText(NavigationSection section)
        {
            return section switch
            {
                NavigationSection.Traceability => "Traceability zostawilem jeszcze bez podpiecia. Na tym etapie aktywne sa widoki Towary i Magazyny.",
                _ => "Ten widok nie ma jeszcze podlaczonej tabeli."
            };
        }

        private sealed class TraceabilityFilterOption(string value, string displayName)
        {
            public string Value { get; } = value;

            public string DisplayName { get; } = displayName;

            public override string ToString()
            {
                return DisplayName;
            }
        }
    }
}