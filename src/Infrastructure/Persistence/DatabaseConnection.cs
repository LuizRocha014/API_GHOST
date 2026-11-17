using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence;

/// <summary>
/// Escolhe o banco pela configuração <c>Database:Target</c>: o nome de uma entrada de
/// <c>ConnectionStrings</c> (ex.: <c>Express</c> para o SQL Server Express local ou de uma VM,
/// <c>Remote</c> para o sql.bsite.net). Sem <c>Database:Target</c>, usa <c>DefaultConnection</c>.
/// </summary>
public static class DatabaseConnection
{
    public static string Resolve(IConfiguration configuration)
    {
        var target = configuration["Database:Target"];
        if (!string.IsNullOrWhiteSpace(target))
        {
            var cs = configuration.GetConnectionString(target);
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException($"Database:Target = '{target}', mas ConnectionStrings:{target} não está definida.");
            return cs;
        }

        return configuration.GetConnectionString("DefaultConnection")
               ?? throw new InvalidOperationException(
                   "Defina Database:Target (Express ou Remote) ou ConnectionStrings:DefaultConnection.");
    }

    /// <summary>Descrição para log, sem senha.</summary>
    public static string Describe(IConfiguration configuration)
    {
        var target = configuration["Database:Target"] ?? "DefaultConnection";
        try
        {
            var b = new SqlConnectionStringBuilder(Resolve(configuration));
            var auth = b.IntegratedSecurity ? "Windows" : $"login {b.UserID}";
            return $"{target}: servidor {b.DataSource}, banco {b.InitialCatalog} ({auth})";
        }
        catch (Exception ex)
        {
            return $"{target}: {ex.Message}";
        }
    }
}
