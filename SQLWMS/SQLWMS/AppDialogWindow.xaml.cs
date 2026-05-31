using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SQLWMS
{
    internal enum AppDialogKind
    {
        Information,
        Warning,
        Error
    }

    internal enum AppDialogButtons
    {
        Ok,
        ConfirmCancel
    }

    public partial class AppDialogWindow : Window
    {
        private readonly AppDialogButtons _buttons;

        internal AppDialogWindow(string title, string message, AppDialogKind kind, AppDialogButtons buttons, string primaryButtonText, string secondaryButtonText)
        {
            InitializeComponent();

            _buttons = buttons;
            Title = title;
            TitleTextBlock.Text = title;
            MessageTextBlock.Text = message;
            OkButton.Content = primaryButtonText;
            SecondaryButton.Content = secondaryButtonText;
            SecondaryButton.Visibility = buttons == AppDialogButtons.ConfirmCancel
                ? Visibility.Visible
                : Visibility.Collapsed;

            ApplyKind(kind);
        }

        internal static bool? Show(Window? owner, string title, string message, AppDialogKind kind = AppDialogKind.Warning)
        {
            AppDialogWindow dialog = new(title, message, kind, AppDialogButtons.Ok, "OK", string.Empty);
            if (owner is not null)
            {
                dialog.Owner = owner;
            }

            return dialog.ShowDialog();
        }

        internal static bool Confirm(Window? owner, string title, string message, string confirmText, string cancelText, AppDialogKind kind = AppDialogKind.Warning)
        {
            AppDialogWindow dialog = new(title, message, kind, AppDialogButtons.ConfirmCancel, confirmText, cancelText);
            if (owner is not null)
            {
                dialog.Owner = owner;
            }

            return dialog.ShowDialog() == true;
        }

        private void ApplyKind(AppDialogKind kind)
        {
            switch (kind)
            {
                case AppDialogKind.Error:
                    IconBadgeBorder.Background = CreateBrush("#FDECEC");
                    IconBadgeBorder.BorderBrush = CreateBrush("#E2AAAA");
                    IconTextBlock.Text = "!";
                    IconTextBlock.Foreground = CreateBrush("#8F1D1D");
                    TitleTextBlock.Foreground = CreateBrush("#8F1D1D");
                    HeadlineTextBlock.Text = "Wystapil blad";
                    OkButton.Background = CreateBrush("#B54747");
                    OkButton.BorderBrush = CreateBrush("#B54747");
                    break;

                case AppDialogKind.Information:
                    IconBadgeBorder.Background = CreateBrush("#E8F6F3");
                    IconBadgeBorder.BorderBrush = CreateBrush("#9FD3C6");
                    IconTextBlock.Text = "i";
                    IconTextBlock.Foreground = CreateBrush("#1D675F");
                    TitleTextBlock.Foreground = CreateBrush("#1D675F");
                    HeadlineTextBlock.Text = "Informacja";
                    OkButton.Background = CreateBrush("#1D7A72");
                    OkButton.BorderBrush = CreateBrush("#1D7A72");
                    break;

                default:
                    IconBadgeBorder.Background = CreateBrush("#FFF2E8");
                    IconBadgeBorder.BorderBrush = CreateBrush("#E7B089");
                    IconTextBlock.Text = "!";
                    IconTextBlock.Foreground = CreateBrush("#8A5318");
                    TitleTextBlock.Foreground = CreateBrush("#173B47");
                    HeadlineTextBlock.Text = "Uwaga";
                    OkButton.Background = CreateBrush("#173B47");
                    OkButton.BorderBrush = CreateBrush("#173B47");
                    break;
            }
        }

        private static SolidColorBrush CreateBrush(string hex)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void SecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = _buttons == AppDialogButtons.Ok;
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}