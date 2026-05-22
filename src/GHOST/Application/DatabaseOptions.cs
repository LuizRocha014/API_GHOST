namespace Application;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Quando true, ao iniciar a API o `MigrationRunner` aplica todas as
    /// migrations SQL pendentes (controladas pela tabela `HistoryMigration`)
    /// e também executa o `EF Core MigrateAsync` para preservar o histórico
    /// existente baseado em DbContext.
    /// </summary>
    public bool RunMigrations { get; set; }
}
