using Microsoft.Data.SqlClient;
using System.Data;
using SQLWMS.Models;

namespace SQLWMS.Services
{
    internal sealed class TraceabilityService : SqlServiceBase
    {
        public Task<List<TraceabilityReportItem>> LoadReportAsync(string documentNumber, string? productCode = null, string? feature = null)
        {
            return LoadListAsync(
                "SBD.RaportTraceability",
                reader => new TraceabilityReportItem
                {
                    Level = Convert.ToInt32(reader["Poziom"]),
                    Path = Convert.ToString(reader["Sciezka"]) ?? string.Empty,
                    StartDocumentNumber = Convert.ToString(reader["DokumentStartowyNumer"]) ?? string.Empty,
                    ThroughDocumentNumber = Convert.ToString(reader["PrzezDokumentNumer"]) ?? string.Empty,
                    ThroughDocumentType = Convert.ToString(reader["PrzezTypDokumentu"]) ?? string.Empty,
                    DeliveryId = TryGetInt32(reader, "DostawaId"),
                    ProductCode = Convert.ToString(reader["TowarKod"]) ?? string.Empty,
                    ProductName = Convert.ToString(reader["TowarNazwa"]) ?? string.Empty,
                    Feature = reader["Cecha"] is DBNull ? null : Convert.ToString(reader["Cecha"]),
                    WarehouseCode = Convert.ToString(reader["MagazynKod"]) ?? string.Empty,
                    WarehouseName = Convert.ToString(reader["MagazynNazwa"]) ?? string.Empty,
                    SectorCode = Convert.ToString(reader["SektorKod"]) ?? string.Empty,
                    SectorName = Convert.ToString(reader["SektorNazwa"]) ?? string.Empty,
                    CurrentQuantity = Convert.ToDecimal(reader["AktualnaIloscDostawy"]),
                    IssuedQuantity = TryGetDecimal(reader, "IloscRozchodu"),
                    CreatingPositionId = TryGetInt32(reader, "ZakladajacaPozycjaId"),
                    CreatingAllocationId = TryGetInt32(reader, "ZakladajacaAlokacjaId"),
                    SourceAllocationId = TryGetInt32(reader, "ZrodlowaAlokacjaId"),
                    IssuedAllocationId = TryGetInt32(reader, "RozchodAlokacjaId")
                },
                command =>
                {
                    command.Parameters.Add(new SqlParameter("@DokumentNumer", documentNumber));
                    command.Parameters.Add(CreateNullableParameter("@TowarKod", productCode, 100));
                    command.Parameters.Add(CreateNullableParameter("@Cecha", feature, 200));
                },
                CommandType.StoredProcedure);
        }

        private static SqlParameter CreateNullableParameter(string name, string? value, int size)
        {
            return new SqlParameter(name, SqlDbType.NVarChar, size)
            {
                Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value
            };
        }

        private static int? TryGetInt32(SqlDataReader reader, string columnName)
        {
            return reader[columnName] is DBNull ? null : Convert.ToInt32(reader[columnName]);
        }

        private static decimal? TryGetDecimal(SqlDataReader reader, string columnName)
        {
            return reader[columnName] is DBNull ? null : Convert.ToDecimal(reader[columnName]);
        }
    }
}