namespace SQLWMS.Models
{
    internal sealed class DocumentListItem
    {
        public int Id { get; init; }
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

    internal sealed class DocumentDetailsItem
    {
        public int Id { get; init; }
        public string NumerDokumentu { get; init; } = string.Empty;
        public string TypDokumentu { get; init; } = string.Empty;
        public string StatusDokumentu { get; init; } = string.Empty;
        public DateTime DataRealizacji { get; init; }
        public string OtworzonyPrzez { get; init; } = string.Empty;
        public string DataModyfikacji { get; init; } = string.Empty;
        public string SeriaDokumentu { get; init; } = string.Empty;
        public string OpisDokumentu { get; init; } = string.Empty;
        public string MagazynZrodlowyKod { get; init; } = string.Empty;
        public string SektorZrodlowyKod { get; init; } = string.Empty;
        public string MagazynDocelowyKod { get; init; } = string.Empty;
        public string SektorDocelowyKod { get; init; } = string.Empty;
    }

    internal sealed class DocumentPositionItem
    {
        public int Id { get; init; }
        public string TowarKod { get; init; } = string.Empty;
        public string TowarNazwa { get; init; } = string.Empty;
        public decimal IloscJednostkowa { get; init; }
        public string Jednostka { get; init; } = string.Empty;
        public decimal Ilosc { get; init; }
    }

    internal sealed class DocumentCreateRequest
    {
        public string TypDokumentu { get; init; } = string.Empty;
        public DateTime? DataWystawienia { get; init; }
        public string Seria { get; init; } = string.Empty;
        public string Operator { get; init; } = string.Empty;
    }

    internal sealed class DocumentCreateResult
    {
        public string Message { get; init; } = string.Empty;
        public int? ErrorCode { get; init; }
        public int? DocumentId { get; init; }
        public string DocumentNumber { get; init; } = string.Empty;
        public bool IsSuccess => ErrorCode is null;
    }

    internal sealed class DocumentUpdateRequest
    {
        public int Id { get; init; }
        public DateTime? DataDokumentu { get; init; }
        public string? MagazynZrodlowyKod { get; init; }
        public string? SektorZrodlowyKod { get; init; }
        public string? MagazynDocelowyKod { get; init; }
        public string? SektorDocelowyKod { get; init; }
        public string? OpisDokumentu { get; init; }
        public string Operator { get; init; } = string.Empty;
    }

    internal sealed class DocumentPositionCreateRequest
    {
        public int DocumentId { get; init; }
        public string TowarKod { get; init; } = string.Empty;
        public decimal? Ilosc { get; init; }
        public string? JednostkaKod { get; init; }
        public string? Cecha { get; init; }
        public string Operator { get; init; } = string.Empty;
    }

    internal sealed class DocumentPositionUpdateRequest
    {
        public int Id { get; init; }
        public string? TowarKod { get; init; }
        public decimal? Ilosc { get; init; }
        public string Operator { get; init; } = string.Empty;
    }

    internal sealed class WarehouseLookupItem
    {
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Code : $"{Code} - {Name}";
    }

    internal sealed class SectorLookupItem
    {
        public string WarehouseCode { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Code : $"{Code} - {Name}";
    }

    internal sealed class ProductLookupItem
    {
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Code : $"{Code} - {Name}";
    }

    internal sealed class UnitLookupItem
    {
        public string ProductCode { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public decimal ConversionFactor { get; init; }
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Code : $"{Code} - {Name}";
    }

    internal sealed class DocumentProcedureResult
    {
        public string Message { get; init; } = string.Empty;
        public int? ErrorCode { get; init; }
        public bool IsSuccess => ErrorCode is null;
        public bool IsLockedByOtherUser => ErrorCode.HasValue && Message.Contains("zablokowany przez", StringComparison.OrdinalIgnoreCase);
    }
}