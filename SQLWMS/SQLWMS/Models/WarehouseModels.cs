using System.Collections.ObjectModel;

namespace SQLWMS.Models
{
    internal sealed class WarehouseMasterItem : BindableBase
    {
        private bool _isExpanded;
        private bool _detailsLoaded;
        private string _detailStatus = "Rozwin wiersz, aby zaladowac sektory.";

        public int Id { get; init; }
        public string Kod { get; init; } = string.Empty;
        public string Nazwa { get; init; } = string.Empty;
        public string Opis { get; init; } = string.Empty;
        public string Adres { get; init; } = string.Empty;
        public int LiczbaSektorow { get; init; }
        public ObservableCollection<SectorItem> Sectors { get; } = [];

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

    internal sealed class SectorItem
    {
        public int Id { get; init; }
        public string Kod { get; init; } = string.Empty;
        public string Nazwa { get; init; } = string.Empty;
        public string Opis { get; init; } = string.Empty;
    }
}
