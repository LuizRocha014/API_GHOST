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
        Connection = new SqlConnection(DatabaseConnection.Resolve(configuration));
        Connection.Open();
    }

    public void Dispose() => Connection.Dispose();

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
