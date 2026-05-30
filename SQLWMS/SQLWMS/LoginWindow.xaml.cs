using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SQLWMS
{
    public partial class LoginWindow : Window, INotifyPropertyChanged
    {
        private string _userName = string.Empty;

        public LoginWindow(string currentUser)
        {
            InitializeComponent();
            DataContext = this;
            UserName = currentUser;
            Loaded += (_, _) =>
            {
                UserNameTextBox.Focus();
                UserNameTextBox.SelectAll();
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string UserName
        {
            get => _userName;
            set
            {
                if (_userName == value)
                {
                    return;
                }

                _userName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UserName)));
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UserName))
            {
                ValidationTextBlock.Text = "Nazwa uzytkownika nie moze byc pusta.";
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