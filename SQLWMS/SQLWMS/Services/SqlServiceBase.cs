using Microsoft.Data.SqlClient;
using System.Data;

namespace SQLWMS.Services
{
    internal abstract class SqlServiceBase
    {
        protected async Task<List<T>> LoadListAsync<T>(string sql, Func<SqlDataReader, T> map, Action<SqlCommand>? configure = null, CommandType commandType = CommandType.Text)
        {
            using SqlConnection connection = new(DatabaseConnectionSettings.ConnectionString);
            using SqlCommand command = new(sql, connection);
            command.CommandType = commandType;
            configure?.Invoke(command);

            

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();

            List<T> items = [];
            while (await reader.ReadAsync())
            {
                items.Add(map(reader));
            }

            return items;
        }
    }
}
