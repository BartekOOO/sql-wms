using System.Windows;

namespace SQLWMS
{
    public partial class CreateDocumentWindow : Window
    {
        public CreateDocumentWindow(string documentType)
        {
            InitializeComponent();

            DocumentType = documentType.Trim().ToUpperInvariant();
            DocumentTypeTextBlock.Text = "Ustaw date wystawienia i opcjonalna serie dla nowego dokumentu.";
            DocumentTypeValueTextBlock.Text = DocumentType;
            DocumentDateEdit.EditValue = DateTime.Today;

            Loaded += (_, _) => SeriesTextBox.Focus();
        }

        public string DocumentType { get; }

        public DateTime? DocumentDate => DocumentDateEdit.EditValue is DateTime value ? value.Date : null;

        public string DocumentSeries => SeriesTextBox.Text.Trim();

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!DocumentDate.HasValue)
            {
                ValidationTextBlock.Text = "Wybierz date wystawienia dokumentu.";
                return;
            }

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}