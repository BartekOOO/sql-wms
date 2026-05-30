using System.Windows;

namespace SQLWMS
{
    public partial class MainWindow : Window
    {
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
            NavigateTo(NavigationSection.Home);
        }

        private void DocumentsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(NavigationSection.Documents);
        }

        private void WarehousesButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(NavigationSection.Warehouses);
        }

        private void ProductsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(NavigationSection.Products);
        }

        private void TraceabilityButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(NavigationSection.Traceability);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(NavigationSection.Home);
        }

        private void NavigateTo(NavigationSection section)
        {
            bool isHome = section == NavigationSection.Home;

            HomeView.Visibility = isHome ? Visibility.Visible : Visibility.Collapsed;
            SectionView.Visibility = isHome ? Visibility.Collapsed : Visibility.Visible;
            BackButton.Visibility = isHome ? Visibility.Collapsed : Visibility.Visible;

            FooterNavigationTextBlock.Text = isHome
                ? "Widok: Panel startowy"
                : $"Widok: {GetSectionTitle(section)}";

            if (isHome)
            {
                return;
            }

            SectionBadgeTextBlock.Text = "Obszar operacyjny";
            SectionTitleTextBlock.Text = GetSectionTitle(section);
            SectionCardTitleTextBlock.Text = GetSectionTitle(section);
            SectionDescriptionTextBlock.Text = GetSectionDescription(section);
            SectionBackendHintTextBlock.Text = GetBackendHint(section);
            SectionFooterTextBlock.Text =
                $"Sekcja {GetSectionTitle(section).ToLower()} prowadzi do dalszych ekranow roboczych i zachowuje prosta nawigacje powrotu do panelu glownego.";
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
                    "Obszar do przegladu magazynow, sektorow i organizacji przestrzeni pracy.",
                NavigationSection.Products =>
                    "Obszar do zarzadzania kartotekami towarow i najwazniejszymi informacjami o asortymencie.",
                NavigationSection.Traceability =>
                    "Obszar do sledzenia pochodzenia, historii ruchu i powiazan pomiedzy operacjami.",
                _ => ""
            };
        }

        private static string GetBackendHint(NavigationSection section)
        {
            return section switch
            {
                NavigationSection.Documents => "Przestrzen na statusy dokumentow, terminy i priorytety obslugi.",
                NavigationSection.Warehouses => "Przestrzen na przeglad oblozenia, sektorow i organizacji przeplywu.",
                NavigationSection.Products => "Przestrzen na katalog indeksow, cechy i szybki przeglad asortymentu.",
                NavigationSection.Traceability => "Przestrzen na historie zdarzen, identyfikacje partii i powiazania dokumentow.",
                _ => ""
            };
        }
    }
}