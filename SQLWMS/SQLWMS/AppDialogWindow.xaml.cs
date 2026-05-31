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

    public partial class AppDialogWindow : Window
    {
        internal AppDialogWindow(string title, string message, AppDialogKind kind)
        {
            InitializeComponent();

            Title = title;
            TitleTextBlock.Text = title;
            MessageTextBlock.Text = message;

            ApplyKind(kind);
        }

        internal static bool? Show(Window? owner, string title, string message, AppDialogKind kind = AppDialogKind.Warning)
        {
            AppDialogWindow dialog = new(title, message, kind);
            if (owner is not null)
            {
                dialog.Owner = owner;
            }

            return dialog.ShowDialog();
        }

        private void ApplyKind(AppDialogKind kind)
        {
            switch (kind)
            {
                case AppDialogKind.Error:
                    AccentStripeBorder.Background = CreateBrush("#B54747");
                    IconBadgeBorder.Background = CreateBrush("#FDECEC");
                    IconBadgeBorder.BorderBrush = CreateBrush("#E2AAAA");
                    IconTextBlock.Text = "!";
                    IconTextBlock.Foreground = CreateBrush("#8F1D1D");
                    HeadlineTextBlock.Text = "Wystapil blad";
                    OkButton.Background = CreateBrush("#B54747");
                    break;

                case AppDialogKind.Information:
                    AccentStripeBorder.Background = CreateBrush("#1D7A72");
                    IconBadgeBorder.Background = CreateBrush("#E8F6F3");
                    IconBadgeBorder.BorderBrush = CreateBrush("#9FD3C6");
                    IconTextBlock.Text = "i";
                    IconTextBlock.Foreground = CreateBrush("#1D675F");
                    HeadlineTextBlock.Text = "Informacja";
                    OkButton.Background = CreateBrush("#1D7A72");
                    break;

                default:
                    AccentStripeBorder.Background = CreateBrush("#BE6943");
                    IconBadgeBorder.Background = CreateBrush("#FFF2E8");
                    IconBadgeBorder.BorderBrush = CreateBrush("#E7B089");
                    IconTextBlock.Text = "!";
                    IconTextBlock.Foreground = CreateBrush("#8A5318");
                    HeadlineTextBlock.Text = "Uwaga";
                    OkButton.Background = CreateBrush("#173B47");
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
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