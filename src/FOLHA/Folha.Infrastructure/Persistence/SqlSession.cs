using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Folha.Infrastructure.Persistence;

public sealed class SqlSession : IDisposable, IAsyncDisposable
{
    public SqlConnection Connection { get; }

    public SqlSession(IConfiguration configuration)
    {
        var cs = configuration.GetConnectionString("FolhaConnection")
                 ?? configuration.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException(
                     "Defina ConnectionStrings:FolhaConnection apontando para o FolhaDB.");
        Connection = new SqlConnection(cs);
        Connection.Open();
    }

    public void Dispose() => Connection.Dispose();

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
