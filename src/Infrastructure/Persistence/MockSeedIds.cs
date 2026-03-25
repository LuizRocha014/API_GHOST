namespace Infrastructure.Persistence;

/// <summary>
/// IDs gerados com <see cref="System.Guid.NewGuid"/> por execução, compartilhados pelos repositórios mock em memória.
/// </summary>
internal static class MockSeedIds
{
    public static readonly Guid CompanyId = Guid.NewGuid();
    public static readonly Guid BranchCentroId = Guid.NewGuid();
    public static readonly Guid BranchSulId = Guid.NewGuid();
    public static readonly Guid UserAdminId = Guid.NewGuid();
    public static readonly Guid ProductNotebookId = Guid.NewGuid();
    public static readonly Guid ProductMouseId = Guid.NewGuid();
    public static readonly Guid ProductMonitorId = Guid.NewGuid();
}
