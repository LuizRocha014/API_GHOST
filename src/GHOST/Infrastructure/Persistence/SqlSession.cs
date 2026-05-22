using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence;

/// <summary>
/// Uma conexão SQL aberta por escopo (request), compartilhada pelos repositórios Dapper.
/// </summary>
public sealed class SqlSession : IDisposable, IAsyncDisposable
{
    public SqlConnection Connection { get; }

    public SqlSession(IConfiguration configuration)
    {
        var cs = configuration.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException(
                     "Defina ConnectionStrings:DefaultConnection (ex.: SQL Server LocalDB ou instância nomeada).");
        Connection = new SqlConnection(cs);
        Connection.Open();
    }

    public void Dispose() => Connection.Dispose();

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
