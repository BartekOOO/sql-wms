using Microsoft.Data.SqlClient;
using SQLWMS.Models;

namespace SQLWMS.Services
{
    internal sealed class WarehouseCatalogService : SqlServiceBase
    {
        public Task<List<WarehouseMasterItem>> LoadWarehousesAsync(string? codeFilter, string? nameFilter, string? addressFilter)
        {
            const string sql = @"
SELECT
      Id
    , Nazwa
    , Kod
    , Opis
    , Adres
    , LiczbaSektorow
FROM SBD.MagazynyView
WHERE (@CodeFilter = '' OR Kod LIKE '%' + @CodeFilter + '%')
    AND (@NameFilter = '' OR Nazwa LIKE '%' + @NameFilter + '%')
    AND (@AddressFilter = '' OR Adres LIKE '%' + @AddressFilter + '%')
ORDER BY Kod, Nazwa;";

            return LoadListAsync(
                sql,
                reader => new WarehouseMasterItem
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Nazwa = Convert.ToString(reader["Nazwa"]) ?? string.Empty,
                    Kod = Convert.ToString(reader["Kod"]) ?? string.Empty,
                    Opis = Convert.ToString(reader["Opis"]) ?? string.Empty,
                    Adres = Convert.ToString(reader["Adres"]) ?? string.Empty,
                    LiczbaSektorow = Convert.ToInt32(reader["LiczbaSektorow"])
                },
                command =>
                {
                    command.Parameters.Add(new SqlParameter("@CodeFilter", codeFilter ?? string.Empty));
                    command.Parameters.Add(new SqlParameter("@NameFilter", nameFilter ?? string.Empty));
                    command.Parameters.Add(new SqlParameter("@AddressFilter", addressFilter ?? string.Empty));
                });
        }

        public Task<List<SectorItem>> LoadSectorsAsync(int warehouseId)
        {
            const string sql = @"
SELECT
      Id
    , Kod
    , Nazwa
    , Opis
FROM SBD.Sektory
WHERE MagazynId = @WarehouseId
ORDER BY Kod, Nazwa;";

            return LoadListAsync(
                sql,
                reader => new SectorItem
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Kod = Convert.ToString(reader["Kod"]) ?? string.Empty,
                    Nazwa = Convert.ToString(reader["Nazwa"]) ?? string.Empty,
                    Opis = Convert.ToString(reader["Opis"]) ?? string.Empty
                },
                command => command.Parameters.Add(new SqlParameter("@WarehouseId", warehouseId)));
        }
    }
}
