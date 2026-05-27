using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Abasta.Infrastructure.Persistence;

/// <summary>
/// Conexão SQL Server com tempo de vida por requisição (scoped). Compartilhada
/// por todos os repositórios Dapper.
/// </summary>
public sealed class SqlSession : IDisposable, IAsyncDisposable
{
    public SqlConnection Connection { get; }

    public SqlSession(IConfiguration configuration)
    {
        var cs = configuration.GetConnectionString("AbastaConnection")
                 ?? configuration.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException(
                     "Defina ConnectionStrings:AbastaConnection apontando para o AbastaDB.");
        Connection = new SqlConnection(cs);
        Connection.Open();
    }

    public void Dispose() => Connection.Dispose();

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
