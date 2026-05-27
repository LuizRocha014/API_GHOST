namespace Abasta.Application;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Quando true, ao iniciar a API o <c>MigrationRunner</c> aplica todas as
    /// migrations SQL pendentes (controladas pela tabela ABASTA_HistoryMigration).
    /// </summary>
    public bool RunMigrations { get; set; }
}
