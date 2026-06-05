using Microsoft.Data.SqlClient;
using System.Data;
using SQLWMS.Models;

namespace SQLWMS.Services
{
    internal sealed class DocumentCatalogService : SqlServiceBase
    {
        private const string DestinationLocationType = "Docelowy";
        private const string SourceLocationType = "\u0179r\u00f3d\u0142owy";

        public async Task<DocumentPageResult> LoadDocumentsAsync(
            int pageNumber,
            int pageSize,
            string? documentNumberFilter,
            string? documentTypeFilter,
            string? documentStatusFilter,
            string? warehouseFilter,
            string? sectorFilter,
            string? productFilter)
        {
            bool useProcedure = string.IsNullOrWhiteSpace(documentStatusFilter)
                && string.IsNullOrWhiteSpace(warehouseFilter)
                && string.IsNullOrWhiteSpace(sectorFilter)
                && string.IsNullOrWhiteSpace(productFilter);

            return useProcedure
                ? await LoadDocumentsViaProcedureAsync(pageNumber, pageSize, documentNumberFilter, documentTypeFilter)
                : await LoadDocumentsViaViewAsync(
                    pageNumber,
                    pageSize,
                    documentNumberFilter,
                    documentTypeFilter,
                    documentStatusFilter,
                    warehouseFilter,
                    sectorFilter,
                    productFilter);
        }

        private async Task<DocumentPageResult> LoadDocumentsViaProcedureAsync(int pageNumber, int pageSize, string? documentNumberFilter, string? documentTypeFilter)
        {
            List<DocumentRow> rows = await LoadListAsync(
                "SBD.ListaDokumentow",
                MapRow,
                command =>
                {
                    command.Parameters.Add(new SqlParameter("@Strona", pageNumber));
                    command.Parameters.Add(new SqlParameter("@WielkoscStrony", pageSize));
                    command.Parameters.Add(new SqlParameter("@KolumnySortowania", "NumerSortowania ASC"));
                    command.Parameters.Add(CreateNullableParameter("@NumerDokumentu", documentNumberFilter, 50));
                    command.Parameters.Add(CreateNullableParameter("@TypDokumentu", documentTypeFilter, 10));
                },
                CommandType.StoredProcedure);

            return ToPageResult(rows);
        }

        private async Task<DocumentPageResult> LoadDocumentsViaViewAsync(
            int pageNumber,
            int pageSize,
            string? documentNumberFilter,
            string? documentTypeFilter,
            string? documentStatusFilter,
            string? warehouseFilter,
            string? sectorFilter,
            string? productFilter)
        {
            const string sql = @"
SELECT
            d.Id
        , d.NumerDokumentu
    , d.StatusDokumentu
    , d.DataRealizacji
    , d.MagazynZrodlowyKod
    , d.SektorZrodlowyKod
    , d.MagazynDocelowyKod
    , d.SektorDocelowyKod
    , d.TypDokumentu
    , d.OtworzonyPrzez
    , COUNT(1) OVER() AS LiczbaWszystkichRekordow
FROM SBD.DokumentyView d
WHERE (@NumerDokumentu = N'' OR d.NumerDokumentu LIKE N'%' + @NumerDokumentu + N'%')
  AND (@TypDokumentu = N'' OR d.TypDokumentu = @TypDokumentu)
  AND (@StatusDokumentu = N'' OR d.StatusDokumentu = @StatusDokumentu)
    AND (@MagazynKod = N'' OR d.MagazynZrodlowyKod = @MagazynKod OR d.MagazynDocelowyKod = @MagazynKod)
    AND (@SektorKod = N'' OR d.SektorZrodlowyKod = @SektorKod OR d.SektorDocelowyKod = @SektorKod)
    AND (@TowarKod = N'' OR EXISTS (
                SELECT 1
                FROM SBD.Pozycje p
                WHERE p.DokumentId = d.Id
                    AND p.TowarKod = @TowarKod))
ORDER BY d.NumerSortowania ASC
OFFSET @Offset ROWS
FETCH NEXT @WielkoscStrony ROWS ONLY;";

            List<DocumentRow> rows = await LoadListAsync(
                sql,
                MapRow,
                command =>
                {
                    command.Parameters.Add(new SqlParameter("@NumerDokumentu", documentNumberFilter ?? string.Empty));
                    command.Parameters.Add(new SqlParameter("@TypDokumentu", documentTypeFilter ?? string.Empty));
                    command.Parameters.Add(new SqlParameter("@StatusDokumentu", documentStatusFilter ?? string.Empty));
                    command.Parameters.Add(new SqlParameter("@MagazynKod", warehouseFilter ?? string.Empty));
                    command.Parameters.Add(new SqlParameter("@SektorKod", sectorFilter ?? string.Empty));
                    command.Parameters.Add(new SqlParameter("@TowarKod", productFilter ?? string.Empty));
                    command.Parameters.Add(new SqlParameter("@Offset", (pageNumber - 1) * pageSize));
                    command.Parameters.Add(new SqlParameter("@WielkoscStrony", pageSize));
                });

            return ToPageResult(rows);
        }

        private static SqlParameter CreateNullableParameter(string name, string? value, int size)
        {
            return new SqlParameter(name, SqlDbType.NVarChar, size)
            {
                Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value
            };
        }

        private static SqlParameter CreateRequiredTextParameter(string name, string? value, int size)
        {
            return new SqlParameter(name, SqlDbType.NVarChar, size)
            {
                Value = value ?? (object)DBNull.Value
            };
        }

        private static DocumentRow MapRow(SqlDataReader reader)
        {
            return new DocumentRow
            {
                Item = new DocumentListItem
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    NumerDokumentu = Convert.ToString(reader["NumerDokumentu"]) ?? string.Empty,
                    StatusDokumentu = Convert.ToString(reader["StatusDokumentu"]) ?? string.Empty,
                    DataRealizacji = Convert.ToDateTime(reader["DataRealizacji"]),
                    MagazynZrodlowyKod = Convert.ToString(reader["MagazynZrodlowyKod"]) ?? string.Empty,
                    SektorZrodlowyKod = Convert.ToString(reader["SektorZrodlowyKod"]) ?? string.Empty,
                    MagazynDocelowyKod = Convert.ToString(reader["MagazynDocelowyKod"]) ?? string.Empty,
                    SektorDocelowyKod = Convert.ToString(reader["SektorDocelowyKod"]) ?? string.Empty,
                    TypDokumentu = Convert.ToString(reader["TypDokumentu"]) ?? string.Empty,
                    OtworzonyPrzez = Convert.ToString(reader["OtworzonyPrzez"]) ?? string.Empty
                },
                TotalCount = Convert.ToInt32(reader["LiczbaWszystkichRekordow"])
            };
        }

        public async Task<DocumentProcedureResult> OpenDocumentAsync(int id, string operatorCode)
        {
            return await ExecuteDocumentProcedureAsync(
                "SBD.OtworzDokument",
                command =>
                {
                    command.Parameters.Add(new SqlParameter("@Id", id));
                    command.Parameters.Add(new SqlParameter("@Operator", operatorCode));
                });
        }

        public async Task<DocumentProcedureResult> CloseDocumentAsync(int id, string operatorCode, string action = "Brak")
        {
            return await ExecuteDocumentProcedureAsync(
                "SBD.ZamknijDokument",
                command =>
                {
                    command.Parameters.Add(new SqlParameter("@Id", id));
                    command.Parameters.Add(new SqlParameter("@Akcja", action));
                    command.Parameters.Add(new SqlParameter("@Operator", operatorCode));
                });
        }

        public async Task<DocumentCreateResult> CreateDocumentAsync(DocumentCreateRequest request)
        {
            try
            {
                List<DocumentCreateRow> rows = await LoadListAsync(
                    "SBD.ZalozDokument",
                    reader => new DocumentCreateRow
                    {
                        Message = Convert.ToString(reader["Odpowiedz"]) ?? string.Empty,
                        DocumentId = TryGetInt32(reader, "DokumentId"),
                        DocumentNumber = TryGetString(reader, "DokumentNumer")
                    },
                    command =>
                    {
                        command.Parameters.Add(new SqlParameter("@TypDokumentu", request.TypDokumentu));
                        command.Parameters.Add(CreateNullableDateTimeParameter("@DataWystawienia", request.DataWystawienia));
                        command.Parameters.Add(CreateNullableParameter("@Seria", request.Seria, 20));
                        command.Parameters.Add(new SqlParameter("@Operator", request.Operator));
                    },
                    CommandType.StoredProcedure);

                DocumentCreateRow? row = rows.FirstOrDefault();
                if (row is null)
                {
                    return new DocumentCreateResult
                    {
                        Message = "Brak odpowiedzi z procedury zakladania dokumentu.",
                        Succeeded = false
                    };
                }

                return new DocumentCreateResult
                {
                    Message = row.Message,
                    Succeeded = true,
                    DocumentId = row.DocumentId,
                    DocumentNumber = row.DocumentNumber
                };
            }
            catch (SqlException ex)
            {
                return new DocumentCreateResult
                {
                    Message = ExtractSqlMessage(ex),
                    Succeeded = false
                };
            }
        }

        public async Task<DocumentProcedureResult> UpdateDocumentAsync(DocumentUpdateRequest request)
        {
            return await ExecuteDocumentProcedureAsync(
                "SBD.EdytujDokument",
                command =>
                {
                    command.Parameters.Add(new SqlParameter("@Id", request.Id));
                    command.Parameters.Add(CreateNullableDateTimeParameter("@DataDokumentu", request.DataDokumentu));
                    command.Parameters.Add(CreateNullableParameter("@Opis", request.OpisDokumentu, 500));
                    command.Parameters.Add(new SqlParameter("@Operator", request.Operator));
                });
        }

        public async Task<DocumentProcedureResult> ChangeDocumentWarehouseAsync(int documentId, string warehouseCode, bool isSource, string operatorCode)
        {
            return await ExecuteDocumentProcedureAsync(
                "SBD.ZmienMagazyn",
                command =>
                {
                    command.Parameters.Add(new SqlParameter("@Id", documentId));
                    command.Parameters.Add(CreateRequiredTextParameter("@Magazyn", warehouseCode, 50));
                    command.Parameters.Add(CreateRequiredTextParameter("@Typ", isSource ? SourceLocationType : DestinationLocationType, 50));
                    command.Parameters.Add(CreateRequiredTextParameter("@Operator", operatorCode, 100));
                });
        }

        public async Task<DocumentProcedureResult> ChangeDocumentSectorAsync(int documentId, string sectorCode, bool isSource, string operatorCode)
        {
            return await ExecuteDocumentProcedureAsync(
                "SBD.ZmienSektor",
                command =>
                {
                    command.Parameters.Add(new SqlParameter("@Id", documentId));
                    command.Parameters.Add(CreateRequiredTextParameter("@Sektor", sectorCode, 50));
                    command.Parameters.Add(CreateRequiredTextParameter("@Typ", isSource ? SourceLocationType : DestinationLocationType, 50));
                    command.Parameters.Add(CreateRequiredTextParameter("@Operator", operatorCode, 100));
                });
        }

        public async Task<DocumentProcedureResult> AddDocumentPositionAsync(DocumentPositionCreateRequest request)
        {
            return await ExecuteDocumentProcedureAsync(
                "SBD.DodajPozycje",
                command =>
                {
                    command.Parameters.Add(new SqlParameter("@TowarKod", request.TowarKod));
                    command.Parameters.Add(new SqlParameter("@DokumentId", request.DocumentId));
                    command.Parameters.Add(CreateNullableDecimalParameter("@Ilosc", request.Ilosc, 16, 6));
                    command.Parameters.Add(CreateNullableParameter("@Jednostka", request.JednostkaKod, 20));
                    command.Parameters.Add(CreateNullableParameter("@Cecha", request.Cecha, 200));
                    command.Parameters.Add(new SqlParameter("@Operator", request.Operator));
                });
        }

        public async Task<DocumentProcedureResult> UpdateDocumentPositionAsync(DocumentPositionUpdateRequest request)
        {
            return await ExecuteDocumentProcedureAsync(
                "SBD.EdytujPozycje",
                command =>
                {
                    command.Parameters.Add(new SqlParameter("@Id", request.Id));
                    command.Parameters.Add(CreateNullableDecimalParameter("@Ilosc", request.Ilosc, 18, 6));
                    command.Parameters.Add(new SqlParameter("@Operator", request.Operator));
                });
        }

        public async Task<DocumentProcedureResult> DeleteDocumentPositionAsync(int id, string operatorCode)
        {
            return await ExecuteDocumentProcedureAsync(
                "SBD.UsunPozycje",
                command =>
                {
                    command.Parameters.Add(new SqlParameter("@Id", id));
                    command.Parameters.Add(new SqlParameter("@Operator", operatorCode));
                });
        }

        public Task<List<PositionAllocationItem>> LoadPositionAllocationsAsync(int positionId)
        {
            const string sql = @"
SELECT
      a.Id AS AlokacjaId
    , a.Cecha AS AlokacjaCecha
    , a.Kierunek AS AlokacjaKierunek
    , a.Ilosc AS Ilosc
    , CASE
          WHEN p.JednostkaPrzelicznik IS NULL OR p.JednostkaPrzelicznik = 0 THEN a.Ilosc
          ELSE a.Ilosc / p.JednostkaPrzelicznik
      END AS IloscJednostkowa
        , p.JednostkaKod AS Jednostka
FROM SBD.Alokacje a
JOIN SBD.Pozycje p ON p.Id = a.PozycjaId
WHERE a.PozycjaId = @PozycjaId
ORDER BY a.Id;";

            return LoadListAsync(
                sql,
                reader => new PositionAllocationItem
                {
                    AllocationId = Convert.ToInt32(reader["AlokacjaId"]),
                    Feature = Convert.ToString(reader["AlokacjaCecha"]) ?? string.Empty,
                    Direction = Convert.ToString(reader["AlokacjaKierunek"]) ?? string.Empty,
                    Quantity = Convert.ToDecimal(reader["Ilosc"]),
                    UnitQuantity = Convert.ToDecimal(reader["IloscJednostkowa"]),
                    UnitCode = Convert.ToString(reader["Jednostka"]) ?? string.Empty
                },
                command => command.Parameters.Add(new SqlParameter("@PozycjaId", positionId)));
        }

        public async Task<DocumentProcedureResult> SplitAllocationAsync(AllocationSplitRequest request)
        {
            return await ExecuteDocumentProcedureAsync(
                "SBD.RozbijAlokacje",
                command =>
                {
                    command.Parameters.Add(new SqlParameter("@Id", request.AllocationId));
                    command.Parameters.Add(CreateNullableDecimalParameter("@Ilosc", request.Quantity, 18, 6));
                    command.Parameters.Add(CreateNullableParameter("@Cecha", request.Feature, 200));
                    command.Parameters.Add(new SqlParameter("@Operator", request.Operator));
                });
        }

        public async Task<DocumentProcedureResult> DeleteAllocationAsync(int allocationId, string operatorCode)
        {
            return await ExecuteDocumentProcedureAsync(
                "SBD.UsunAlokacje",
                command =>
                {
                    command.Parameters.Add(new SqlParameter("@Id", allocationId));
                    command.Parameters.Add(new SqlParameter("@Operator", operatorCode));
                });
        }

        public Task<List<WarehouseLookupItem>> LoadWarehouseLookupAsync()
        {
            const string sql = @"
SELECT
      Kod
    , Nazwa
FROM SBD.Magazyny
ORDER BY Kod;";

            return LoadListAsync(
                sql,
                reader => new WarehouseLookupItem
                {
                    Code = Convert.ToString(reader["Kod"]) ?? string.Empty,
                    Name = Convert.ToString(reader["Nazwa"]) ?? string.Empty
                });
        }

        public Task<List<SectorLookupItem>> LoadSectorLookupAsync(string? warehouseCode)
        {
            const string sql = @"
SELECT
      MagazynKod
    , SektorKod
    , SektorNazwa
FROM SBD.SektoryView
WHERE (@MagazynKod = N'' OR MagazynKod = @MagazynKod)
ORDER BY SektorKod;";

            return LoadListAsync(
                sql,
                reader => new SectorLookupItem
                {
                    WarehouseCode = Convert.ToString(reader["MagazynKod"]) ?? string.Empty,
                    Code = Convert.ToString(reader["SektorKod"]) ?? string.Empty,
                    Name = Convert.ToString(reader["SektorNazwa"]) ?? string.Empty
                },
                command => command.Parameters.Add(new SqlParameter("@MagazynKod", warehouseCode ?? string.Empty)));
        }

        public Task<List<ProductLookupItem>> LoadProductLookupAsync()
        {
            const string sql = @"
SELECT
      Kod
    , Nazwa
FROM SBD.Towary
ORDER BY Kod;";

            return LoadListAsync(
                sql,
                reader => new ProductLookupItem
                {
                    Code = Convert.ToString(reader["Kod"]) ?? string.Empty,
                    Name = Convert.ToString(reader["Nazwa"]) ?? string.Empty
                });
        }

        public Task<List<UnitLookupItem>> LoadUnitLookupAsync(string productCode)
        {
            const string sql = @"
SELECT
      t.Kod AS TowarKod
    , j.Kod
    , j.Nazwa
    , j.Przelicznik
FROM SBD.Jednostki j
JOIN SBD.Towary t ON t.Id = j.TowarId
WHERE t.Kod = @TowarKod
ORDER BY CASE WHEN j.Przelicznik = 1 THEN 0 ELSE 1 END, j.Kod;";

            return LoadListAsync(
                sql,
                reader => new UnitLookupItem
                {
                    ProductCode = Convert.ToString(reader["TowarKod"]) ?? string.Empty,
                    Code = Convert.ToString(reader["Kod"]) ?? string.Empty,
                    Name = Convert.ToString(reader["Nazwa"]) ?? string.Empty,
                    ConversionFactor = Convert.ToDecimal(reader["Przelicznik"])
                },
                command => command.Parameters.Add(new SqlParameter("@TowarKod", productCode)));
        }

        public async Task<DocumentDetailsItem?> LoadDocumentDetailsAsync(int id)
        {
            const string sql = @"
SELECT
      Id
    , NumerDokumentu
    , TypDokumentu
    , StatusDokumentu
    , DataRealizacji
    , OtworzonyPrzez
    , DataModyfikacji
    , SeriaDokumentu
    , OpisDokumentu
    , MagazynZrodlowyKod
    , SektorZrodlowyKod
    , MagazynDocelowyKod
    , SektorDocelowyKod
FROM SBD.DokumentyView
WHERE Id = @Id;";

            List<DocumentDetailsItem> items = await LoadListAsync(
                sql,
                reader => new DocumentDetailsItem
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    NumerDokumentu = Convert.ToString(reader["NumerDokumentu"]) ?? string.Empty,
                    TypDokumentu = Convert.ToString(reader["TypDokumentu"]) ?? string.Empty,
                    StatusDokumentu = Convert.ToString(reader["StatusDokumentu"]) ?? string.Empty,
                    DataRealizacji = Convert.ToDateTime(reader["DataRealizacji"]),
                    OtworzonyPrzez = Convert.ToString(reader["OtworzonyPrzez"]) ?? string.Empty,
                    DataModyfikacji = Convert.ToString(reader["DataModyfikacji"]) ?? string.Empty,
                    SeriaDokumentu = Convert.ToString(reader["SeriaDokumentu"]) ?? string.Empty,
                    OpisDokumentu = Convert.ToString(reader["OpisDokumentu"]) ?? string.Empty,
                    MagazynZrodlowyKod = Convert.ToString(reader["MagazynZrodlowyKod"]) ?? string.Empty,
                    SektorZrodlowyKod = Convert.ToString(reader["SektorZrodlowyKod"]) ?? string.Empty,
                    MagazynDocelowyKod = Convert.ToString(reader["MagazynDocelowyKod"]) ?? string.Empty,
                    SektorDocelowyKod = Convert.ToString(reader["SektorDocelowyKod"]) ?? string.Empty
                },
                command => command.Parameters.Add(new SqlParameter("@Id", id)));

            return items.FirstOrDefault();
        }

        public Task<List<DocumentPositionItem>> LoadDocumentPositionsAsync(int id)
        {
            const string sql = @"
SELECT
      Id
    , TowarKod
    , TowarNazwa
    , IloscJednostkowa
    , Jednostka
    , Ilosc
FROM SBD.PozycjeView
WHERE IdDokumentu = @Id
ORDER BY Id;";

            return LoadListAsync(
                sql,
                reader => new DocumentPositionItem
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    TowarKod = Convert.ToString(reader["TowarKod"]) ?? string.Empty,
                    TowarNazwa = Convert.ToString(reader["TowarNazwa"]) ?? string.Empty,
                    IloscJednostkowa = Convert.ToDecimal(reader["IloscJednostkowa"]),
                    Jednostka = Convert.ToString(reader["Jednostka"]) ?? string.Empty,
                    Ilosc = Convert.ToDecimal(reader["Ilosc"])
                },
                command => command.Parameters.Add(new SqlParameter("@Id", id)));
        }

        private async Task<DocumentProcedureResult> ExecuteDocumentProcedureAsync(string procedureName, Action<SqlCommand> configure)
        {
            try
            {
                List<ProcedureResultRow> rows = await LoadListAsync(
                    procedureName,
                    reader => new ProcedureResultRow
                    {
                        Message = Convert.ToString(reader["Odpowiedz"]) ?? string.Empty
                    },
                    configure,
                    CommandType.StoredProcedure);

                ProcedureResultRow? row = rows.FirstOrDefault();
                if (row is null)
                {
                    return new DocumentProcedureResult
                    {
                        Message = "Brak odpowiedzi z procedury.",
                        Succeeded = false
                    };
                }

                return new DocumentProcedureResult
                {
                    Message = row.Message,
                    Succeeded = true
                };
            }
            catch (SqlException ex)
            {
                return new DocumentProcedureResult
                {
                    Message = ExtractSqlMessage(ex),
                    Succeeded = false
                };
            }
        }

        private static string ExtractSqlMessage(SqlException ex)
        {
            return string.IsNullOrWhiteSpace(ex.Message)
                ? "Wystapil blad podczas wykonywania procedury SQL."
                : ex.Message;
        }

        private static SqlParameter CreateNullableDateTimeParameter(string name, DateTime? value)
        {
            return new SqlParameter(name, SqlDbType.DateTime)
            {
                Value = value.HasValue ? value.Value.Date : DBNull.Value
            };
        }

        private static SqlParameter CreateNullableDecimalParameter(string name, decimal? value, byte precision, byte scale)
        {
            return new SqlParameter(name, SqlDbType.Decimal)
            {
                Precision = precision,
                Scale = scale,
                Value = value.HasValue ? value.Value : DBNull.Value
            };
        }

        private static DocumentPageResult ToPageResult(List<DocumentRow> rows)
        {
            return new DocumentPageResult
            {
                Items = rows.Select(row => row.Item).ToList(),
                TotalCount = rows.Count == 0 ? 0 : rows[0].TotalCount
            };
        }

        private sealed class DocumentRow
        {
            public required DocumentListItem Item { get; init; }
            public int TotalCount { get; init; }
        }

        private sealed class ProcedureResultRow
        {
            public string Message { get; init; } = string.Empty;
        }

        private sealed class DocumentCreateRow
        {
            public string Message { get; init; } = string.Empty;
            public int? DocumentId { get; init; }
            public string DocumentNumber { get; init; } = string.Empty;
        }

        private static int? TryGetInt32(SqlDataReader reader, string columnName)
        {
            try
            {
                int ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }

        private static string TryGetString(SqlDataReader reader, string columnName)
        {
            try
            {
                int ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
            }
            catch (IndexOutOfRangeException)
            {
                return string.Empty;
            }
        }
    }
}