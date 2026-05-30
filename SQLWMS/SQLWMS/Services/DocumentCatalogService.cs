using Microsoft.Data.SqlClient;
using System.Data;
using SQLWMS.Models;

namespace SQLWMS.Services
{
    internal sealed class DocumentCatalogService : SqlServiceBase
    {
        public async Task<DocumentPageResult> LoadDocumentsAsync(int pageNumber, int pageSize, string? documentNumberFilter, string? documentTypeFilter, string? documentStatusFilter)
        {
            return string.IsNullOrWhiteSpace(documentStatusFilter)
                ? await LoadDocumentsViaProcedureAsync(pageNumber, pageSize, documentNumberFilter, documentTypeFilter)
                : await LoadDocumentsViaViewAsync(pageNumber, pageSize, documentNumberFilter, documentTypeFilter, documentStatusFilter);
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

        private async Task<DocumentPageResult> LoadDocumentsViaViewAsync(int pageNumber, int pageSize, string? documentNumberFilter, string? documentTypeFilter, string? documentStatusFilter)
        {
            const string sql = @"
SELECT
      d.NumerDokumentu
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

        private static DocumentRow MapRow(SqlDataReader reader)
        {
            return new DocumentRow
            {
                Item = new DocumentListItem
                {
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
    }
}