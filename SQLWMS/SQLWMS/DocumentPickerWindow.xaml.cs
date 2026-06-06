using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SQLWMS.Models;
using SQLWMS.Services;

namespace SQLWMS
{
    public partial class DocumentPickerWindow : Window
    {
        private const int PageSize = 12;

        private readonly DocumentCatalogService _documentCatalogService;
        private readonly ObservableCollection<DocumentListItem> _documents = [];
        private int _currentPage = 1;
        private int _totalCount;

        internal DocumentPickerWindow(DocumentCatalogService documentCatalogService)
        {
            InitializeComponent();

            _documentCatalogService = documentCatalogService;
            DocumentsDataGrid.ItemsSource = _documents;

            Loaded += DocumentPickerWindow_Loaded;
        }

        internal string SelectedDocumentNumber { get; private set; } = string.Empty;

        private async void DocumentPickerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDocumentsAsync();
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = 1;
            await LoadDocumentsAsync();
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            DocumentNumberTextBox.Clear();
            DocumentTypeComboBox.SelectedIndex = 0;
            DocumentStatusComboBox.SelectedIndex = 0;
            _currentPage = 1;
            await LoadDocumentsAsync();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ChooseButton_Click(object sender, RoutedEventArgs e)
        {
            ChooseSelectedDocument();
        }

        private async void PreviousPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage <= 1)
            {
                return;
            }

            _currentPage--;
            await LoadDocumentsAsync();
        }

        private async void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage >= GetTotalPages())
            {
                return;
            }

            _currentPage++;
            await LoadDocumentsAsync();
        }

        private void DocumentsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DocumentsDataGrid.SelectedItem is DocumentListItem)
            {
                ChooseSelectedDocument();
            }
        }

        private void DocumentsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ChooseButton.IsEnabled = DocumentsDataGrid.SelectedItem is DocumentListItem;
        }

        private async void DocumentNumberTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;
            _currentPage = 1;
            await LoadDocumentsAsync();
        }

        private async Task LoadDocumentsAsync()
        {
            try
            {
                SetBusy(true);
                StatusTextBlock.Text = "Ladowanie dokumentow...";

                DocumentPageResult page = await _documentCatalogService.LoadDocumentsAsync(
                    _currentPage,
                    PageSize,
                    DocumentNumberTextBox.Text.Trim(),
                    GetSelectedFilterValue(DocumentTypeComboBox),
                    GetSelectedFilterValue(DocumentStatusComboBox),
                    null,
                    null,
                    null,
                    null);

                _documents.Clear();
                foreach (DocumentListItem item in page.Items)
                {
                    _documents.Add(item);
                }

                _totalCount = page.TotalCount;
                UpdatePagination();
                StatusTextBlock.Text = _documents.Count == 0
                    ? "Brak dokumentow dla aktualnych filtrow."
                    : "Wybierz dokument i zatwierdz przyciskiem lub dwuklikiem.";
            }
            catch (Exception ex)
            {
                _documents.Clear();
                _totalCount = 0;
                UpdatePagination();
                StatusTextBlock.Text = $"Nie udalo sie pobrac dokumentow. {ex.Message}";
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void UpdatePagination()
        {
            int totalPages = GetTotalPages();
            PaginationTextBlock.Text = $"Strona {_currentPage} z {totalPages}";
            ResultsTextBlock.Text = _totalCount == 1 ? "1 rekord" : $"{_totalCount} rekordow";
            PreviousPageButton.IsEnabled = _currentPage > 1;
            NextPageButton.IsEnabled = _currentPage < totalPages;
            ChooseButton.IsEnabled = DocumentsDataGrid.SelectedItem is DocumentListItem;
        }

        private void ChooseSelectedDocument()
        {
            if (DocumentsDataGrid.SelectedItem is not DocumentListItem item)
            {
                return;
            }

            SelectedDocumentNumber = item.NumerDokumentu;
            DialogResult = true;
        }

        private int GetTotalPages()
        {
            return Math.Max(1, (int)Math.Ceiling(_totalCount / (double)PageSize));
        }

        private void SetBusy(bool isBusy)
        {
            DocumentNumberTextBox.IsEnabled = !isBusy;
            DocumentTypeComboBox.IsEnabled = !isBusy;
            DocumentStatusComboBox.IsEnabled = !isBusy;
            SearchButton.IsEnabled = !isBusy;
            ChooseButton.IsEnabled = !isBusy && DocumentsDataGrid.SelectedItem is DocumentListItem;
            PreviousPageButton.IsEnabled = !isBusy && _currentPage > 1;
            NextPageButton.IsEnabled = !isBusy && _currentPage < GetTotalPages();
        }

        private static string GetSelectedFilterValue(System.Windows.Controls.ComboBox comboBox)
        {
            return comboBox.SelectedItem is ComboBoxItem item
                ? Convert.ToString(item.Tag) ?? string.Empty
                : string.Empty;
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