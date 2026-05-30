using System.Collections.ObjectModel;

namespace SQLWMS.Models
{
    internal sealed class ProductMasterItem : BindableBase
    {
        private bool _isExpanded;
        private bool _detailsLoaded;
        private string _detailStatus = "Rozwin wiersz, aby zaladowac warianty.";

        public int Id { get; init; }
        public string Kod { get; init; } = string.Empty;
        public string Nazwa { get; init; } = string.Empty;
        public bool HasVariants { get; init; }
        public int LiczbaWariantow { get; init; }
        public decimal SumaIlosci { get; init; }
        public ObservableCollection<ProductVariantItem> Variants { get; } = [];

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public bool DetailsLoaded
        {
            get => _detailsLoaded;
            set => SetProperty(ref _detailsLoaded, value);
        }

        public string DetailStatus
        {
            get => _detailStatus;
            set => SetProperty(ref _detailStatus, value);
        }
    }

    internal sealed class ProductVariantItem
    {
        public string Cecha { get; init; } = string.Empty;
        public decimal Ilosc { get; init; }
    }
}
