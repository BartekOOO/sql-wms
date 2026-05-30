namespace SQLWMS.Models
{
    internal sealed class DocumentListItem
    {
        public string NumerDokumentu { get; init; } = string.Empty;
        public string StatusDokumentu { get; init; } = string.Empty;
        public DateTime DataRealizacji { get; init; }
        public string MagazynZrodlowyKod { get; init; } = string.Empty;
        public string SektorZrodlowyKod { get; init; } = string.Empty;
        public string MagazynDocelowyKod { get; init; } = string.Empty;
        public string SektorDocelowyKod { get; init; } = string.Empty;
        public string TypDokumentu { get; init; } = string.Empty;
        public string OtworzonyPrzez { get; init; } = string.Empty;
        public bool IsOpened => !string.IsNullOrWhiteSpace(OtworzonyPrzez);
        public bool IsOpenedByCurrentUser { get; set; }
    }

    internal sealed class DocumentPageResult
    {
        public List<DocumentListItem> Items { get; init; } = [];
        public int TotalCount { get; init; }
    }
}