using Microsoft.Data.SqlClient;
using static Dapper.SqlMapper;

namespace NkplmErp.Shared.Services
{
    public class DisposableGridReader : IDisposable
    {
        public SqlConnection Connection { get; }
        public GridReader Reader { get; }

        public DisposableGridReader(SqlConnection connection, GridReader reader)
        {
            Connection = connection;
            Reader = reader;
        }

        public void Dispose()
        {
            Reader?.Dispose();
            Connection?.Dispose();
        }
    }
}
