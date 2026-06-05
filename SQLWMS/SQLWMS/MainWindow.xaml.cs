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

        private readonly DocumentCatalogService _documentCatalogService = new();
        private readonly ProductCatalogService _productCatalogService = new();
        private readonly WarehouseCatalogService _warehouseCatalogService = new();
        private readonly UserSessionService _userSessionService = new();
        private readonly ObservableCollection<DocumentListItem> _documents = [];
        private readonly ObservableCollection<ProductMasterItem> _products = [];
        private readonly ObservableCollection<WarehouseMasterItem> _warehouses = [];
        private readonly ICollectionView _documentsView;
        private readonly ICollectionView _productsView;
        private readonly ICollectionView _warehousesView;
        private bool _isOpeningDocument;
        private int _documentsCurrentPage = 1;
        private int _documentsTotalCount;
        private int _filterRequestVersion;
        private bool _suspendFilterReload;
        private string _documentWarehouseFilter = string.Empty;
        private string _documentSectorFilter = string.Empty;
        private string _documentProductFilter = string.Empty;
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
            WarehousesDataGrid.ItemsSource = _warehousesView;

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
                _documentProductFilter)
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
                    _documentProductFilter);

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

        private void ResetDocumentAdvancedFilters()
        {
            _documentWarehouseFilter = string.Empty;
            _documentSectorFilter = string.Empty;
            _documentProductFilter = string.Empty;
        }

        private bool HasDocumentAdvancedFilters()
        {
            return !string.IsNullOrWhiteSpace(_documentWarehouseFilter)
                || !string.IsNullOrWhiteSpace(_documentSectorFilter)
                || !string.IsNullOrWhiteSpace(_documentProductFilter);
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
                : "Dodatkowe filtry po magazynie, sektorze i towarze";
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
            DocumentsPaginationPanel.Visibility = Visibility.Collapsed;
            SectionToolbarPanel.Visibility = Visibility.Visible;
            SectionPlaceholderBorder.Visibility = Visibility.Collapsed;
            DocumentsDataGrid.Visibility = Visibility.Collapsed;
            ProductsDataGrid.Visibility = Visibility.Visible;
            WarehousesDataGrid.Visibility = Visibility.Collapsed;
            SectionAddressFilterContainer.Visibility = Visibility.Collapsed;
            SectionAddressSpacerColumn.Width = new GridLength(0);
            SectionAddressFilterColumn.Width = new GridLength(0);
            SectionStatusTextBlock.Text = "Lista towarow z wariantami widocznymi od razu.";
        }

        private void ShowWarehousesLayout()
        {
            DocumentsToolbarPanel.Visibility = Visibility.Collapsed;
            DocumentsPaginationPanel.Visibility = Visibility.Collapsed;
            SectionToolbarPanel.Visibility = Visibility.Visible;
            SectionPlaceholderBorder.Visibility = Visibility.Collapsed;
            DocumentsDataGrid.Visibility = Visibility.Collapsed;
            ProductsDataGrid.Visibility = Visibility.Collapsed;
            WarehousesDataGrid.Visibility = Visibility.Visible;
            SectionAddressFilterContainer.Visibility = Visibility.Visible;
            SectionAddressSpacerColumn.Width = new GridLength(10);
            SectionAddressFilterColumn.Width = new GridLength(200);
            SectionStatusTextBlock.Text = "Lista magazynow. Rozwin wiersz, aby doladowac sektory.";
        }

        private void ShowDocumentsLayout()
        {
            DocumentsToolbarPanel.Visibility = Visibility.Visible;
            SectionToolbarPanel.Visibility = Visibility.Collapsed;
            SectionPlaceholderBorder.Visibility = Visibility.Collapsed;
            DocumentsDataGrid.Visibility = Visibility.Visible;
            ProductsDataGrid.Visibility = Visibility.Collapsed;
            WarehousesDataGrid.Visibility = Visibility.Collapsed;
            DocumentsPaginationPanel.Visibility = Visibility.Visible;
            SectionStatusTextBlock.Text = string.Empty;
        }

        private void ShowPlaceholderLayout(NavigationSection section)
        {
            DocumentsToolbarPanel.Visibility = Visibility.Collapsed;
            SectionToolbarPanel.Visibility = Visibility.Collapsed;
            DocumentsPaginationPanel.Visibility = Visibility.Collapsed;
            DocumentsDataGrid.Visibility = Visibility.Collapsed;
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
                    "Lista dokumentow magazynowych z filtrowaniem po numerze, typie, statusie oraz dodatkowymi filtrami magazynu, sektora i towaru.",
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
                NavigationSection.Documents => "Uzyj filtrow nad tabela i przycisku Filtry, a do przechodzenia pomiedzy stronami skorzystaj z pagera pod lista.",
                NavigationSection.Warehouses => "Kliknij naglowek kolumny, aby sortowac. Uzyj plusa w pierwszej kolumnie, aby doladowac sektory tylko dla wybranego magazynu.",
                NavigationSection.Products => "Kliknij naglowek kolumny, aby sortowac. Uzyj plusa w pierwszej kolumnie, aby doladowac warianty tylko dla wybranego towaru.",
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
    }
}