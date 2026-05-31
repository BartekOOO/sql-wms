using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SQLWMS.Models;

namespace SQLWMS
{
    partial class DocumentWindow : Window
    {
        private readonly bool _isReadOnlyMode;
        private readonly Func<Task<DocumentProcedureResult>>? _saveAction;
        private bool _allowClose;

        internal DocumentWindow(DocumentDetailsItem details, IReadOnlyList<DocumentPositionItem> positions, bool isReadOnlyMode, string readOnlyMessage, Func<Task<DocumentProcedureResult>>? saveAction)
        {
            InitializeComponent();

            _isReadOnlyMode = isReadOnlyMode;
            _saveAction = saveAction;

            DocumentTitleTextBlock.Text = details.NumerDokumentu;
            DocumentSubtitleTextBlock.Text = isReadOnlyMode
                ? "Podglad dokumentu w trybie tylko do odczytu."
                : "Podglad dokumentu otwartego do pracy.";

            NumerDokumentuTextBlock.Text = details.NumerDokumentu;
            TypDokumentuTextBlock.Text = details.TypDokumentu;
            StatusDokumentuTextBlock.Text = details.StatusDokumentu;
            DataRealizacjiTextBlock.Text = details.DataRealizacji.ToString("dd.MM.yyyy HH:mm");
            MagazynZrodlowyTextBlock.Text = details.MagazynZrodlowyKod;
            SektorZrodlowyTextBlock.Text = details.SektorZrodlowyKod;
            MagazynDocelowyTextBlock.Text = details.MagazynDocelowyKod;
            SektorDocelowyTextBlock.Text = details.SektorDocelowyKod;
            SeriaDokumentuTextBlock.Text = string.IsNullOrWhiteSpace(details.SeriaDokumentu) ? "Brak" : details.SeriaDokumentu;
            OtworzonyPrzezTextBlock.Text = string.IsNullOrWhiteSpace(details.OtworzonyPrzez) ? "Brak" : details.OtworzonyPrzez;
            OpisDokumentuTextBlock.Text = string.IsNullOrWhiteSpace(details.OpisDokumentu) ? "Brak opisu." : details.OpisDokumentu;
            DocumentPositionsDataGrid.ItemsSource = positions;

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
                _allowClose = true;
                DialogResult = false;
                Close();
                return;
            }

            if (_saveAction is null)
            {
                _allowClose = true;
                DialogResult = true;
                Close();
                return;
            }

            SaveButton.IsEnabled = false;

            try
            {
                DocumentProcedureResult result = await _saveAction();
                if (!result.IsSuccess)
                {
                    AppDialogWindow.Show(this, "Zamykanie dokumentu", result.Message, AppDialogKind.Warning);
                    SaveButton.IsEnabled = true;
                    return;
                }

                _allowClose = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                AppDialogWindow.Show(this, "Zamykanie dokumentu", ex.Message, AppDialogKind.Error);
                SaveButton.IsEnabled = true;
            }
        }

        private void DocumentWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
            }
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