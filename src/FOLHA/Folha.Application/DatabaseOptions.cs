namespace Folha.Application;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Quando true, ao iniciar a API o `MigrationRunner` aplica todas as
    /// migrations SQL pendentes (controladas pela tabela `HistoryMigration`).
    /// </summary>
    public bool RunMigrations { get; set; }
}
