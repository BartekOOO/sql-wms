namespace SQLWMS.Models
{
    internal sealed class TraceabilityReportItem
    {
        public int Level { get; init; }
        public string Path { get; init; } = string.Empty;
        public string StartDocumentNumber { get; init; } = string.Empty;
        public string ThroughDocumentNumber { get; init; } = string.Empty;
        public string ThroughDocumentType { get; init; } = string.Empty;
        public int? DeliveryId { get; init; }
        public string ProductCode { get; init; } = string.Empty;
        public string ProductName { get; init; } = string.Empty;
        public string? Feature { get; init; }
        public string WarehouseCode { get; init; } = string.Empty;
        public string WarehouseName { get; init; } = string.Empty;
        public string SectorCode { get; init; } = string.Empty;
        public string SectorName { get; init; } = string.Empty;
        public decimal CurrentQuantity { get; init; }
        public decimal? IssuedQuantity { get; init; }
        public int? CreatingPositionId { get; init; }
        public int? CreatingAllocationId { get; init; }
        public int? SourceAllocationId { get; init; }
        public int? IssuedAllocationId { get; init; }

        public string DisplayFeature => string.IsNullOrWhiteSpace(Feature) ? "Brak" : Feature;

        public string ProductDisplay => string.IsNullOrWhiteSpace(ProductName)
            ? ProductCode
            : $"{ProductCode} - {ProductName}";

        public string StepDocumentDisplay => string.IsNullOrWhiteSpace(ThroughDocumentNumber)
            ? StartDocumentNumber
            : ThroughDocumentNumber;

        public string StepDocumentTypeDisplay => string.IsNullOrWhiteSpace(ThroughDocumentType)
            ? "START"
            : ThroughDocumentType;

        public string MovementLabel => string.IsNullOrWhiteSpace(ThroughDocumentNumber)
            ? "Start"
            : DeliveryId.HasValue
                ? "Przeplyw"
                : "Wydanie";

        public string LocationDisplay
        {
            get
            {
                bool hasWarehouse = !string.IsNullOrWhiteSpace(WarehouseCode);
                bool hasSector = !string.IsNullOrWhiteSpace(SectorCode);

                if (!hasWarehouse && !hasSector)
                {
                    return "Brak lokalizacji";
                }

                if (hasWarehouse && hasSector)
                {
                    return $"{WarehouseCode} / {SectorCode}";
                }

                return hasWarehouse ? WarehouseCode : SectorCode;
            }
        }

        public string PathTreeDisplay
        {
            get
            {
                string[] segments = Path
                    .Split("->", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                if (segments.Length == 0)
                {
                    return string.Empty;
                }

                List<string> lines = [];
                for (int index = 0; index < segments.Length; index++)
                {
                    string indent = new(' ', index * 2);
                    string prefix = index == 0 ? string.Empty : "|- ";
                    lines.Add($"{indent}{prefix}{segments[index]}");
                }

                return string.Join(Environment.NewLine, lines);
            }
        }
    }
}