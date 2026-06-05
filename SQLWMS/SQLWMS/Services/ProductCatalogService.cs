using Microsoft.Data.SqlClient;
using SQLWMS.Models;

namespace SQLWMS.Services
{
    internal sealed class ProductCatalogService : SqlServiceBase
    {
        public async Task<List<ProductMasterItem>> LoadProductsAsync(string? codeFilter, string? nameFilter)
        {
            const string sql = @"
SELECT
      Id
    , Kod
    , Nazwa
    , Cecha
    , Ilosc
FROM SBD.TowaryView
WHERE (@CodeFilter = '' OR Kod LIKE '%' + @CodeFilter + '%')
    AND (@NameFilter = '' OR Nazwa LIKE '%' + @NameFilter + '%')
ORDER BY Kod, Nazwa, Cecha;";

            List<ProductViewRow> rows = await LoadListAsync(
                sql,
                reader => new ProductViewRow
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Kod = Convert.ToString(reader["Kod"]) ?? string.Empty,
                    Nazwa = Convert.ToString(reader["Nazwa"]) ?? string.Empty,
                    Cecha = Convert.ToString(reader["Cecha"]) ?? string.Empty,
                    Ilosc = Convert.ToDecimal(reader["Ilosc"])
                },
                command =>
                {
                    command.Parameters.Add(new SqlParameter("@CodeFilter", codeFilter ?? string.Empty));
                    command.Parameters.Add(new SqlParameter("@NameFilter", nameFilter ?? string.Empty));
                });

            List<ProductMasterItem> products = [];
            ProductAggregate? current = null;

            foreach (ProductViewRow row in rows)
            {
                if (current is null || current.Id != row.Id)
                {
                    if (current is not null)
                    {
                        products.Add(current.ToProduct());
                    }

                    current = new ProductAggregate(row.Id, row.Kod, row.Nazwa);
                }

                current.AddVariant(row.Cecha, row.Ilosc);
            }

            if (current is not null)
            {
                products.Add(current.ToProduct());
            }

            return products;
        }

        public Task<List<ProductVariantItem>> LoadVariantsAsync(int productId)
        {
            const string sql = @"
SELECT
      Cecha
    , Ilosc
FROM SBD.TowaryView
WHERE Id = @ProductId
ORDER BY Cecha;";

            return LoadListAsync(
                sql,
                reader => new ProductVariantItem
                {
                    Cecha = Convert.ToString(reader["Cecha"]) ?? string.Empty,
                    Ilosc = Convert.ToDecimal(reader["Ilosc"])
                },
                command => command.Parameters.Add(new SqlParameter("@ProductId", productId)));
        }

        private sealed class ProductViewRow
        {
            public int Id { get; init; }
            public string Kod { get; init; } = string.Empty;
            public string Nazwa { get; init; } = string.Empty;
            public string Cecha { get; init; } = string.Empty;
            public decimal Ilosc { get; init; }
        }

        private sealed class ProductAggregate(int id, string kod, string nazwa)
        {
            private readonly List<ProductVariantItem> _variants = [];
            private decimal _totalQuantity;

            public int Id { get; } = id;
            public string Kod { get; } = kod;
            public string Nazwa { get; } = nazwa;

            public void AddVariant(string cecha, decimal ilosc)
            {
                _totalQuantity += ilosc;

                if (string.IsNullOrWhiteSpace(cecha))
                {
                    return;
                }

                _variants.Add(new ProductVariantItem
                {
                    Cecha = cecha,
                    Ilosc = ilosc
                });
            }

            public ProductMasterItem ToProduct()
            {
                ProductMasterItem product = new()
                {
                    Id = Id,
                    Kod = Kod,
                    Nazwa = Nazwa,
                    HasVariants = _variants.Count > 0,
                    LiczbaWariantow = _variants.Count,
                    SumaIlosci = _totalQuantity
                };

                foreach (ProductVariantItem variant in _variants)
                {
                    product.Variants.Add(variant);
                }

                product.DetailsLoaded = true;
                product.IsExpanded = false;
                product.DetailStatus = _variants.Count switch
                {
                    0 => "Towar bez wariantow.",
                    1 => "1 wariant",
                    _ => $"Warianty: {_variants.Count}"
                };

                return product;
            }
        }
    }
}
