namespace Abasta.Application.Abstractions;

public sealed record AnalyticsUser(Guid Id, string FullName, string? JobTitle, string Email, string Role);

public sealed record AnalyticsEntry(
    Guid Id,
    Guid UserId,
    string FuelCode,
    string? StationName,
    decimal Liters,
    decimal PricePerLiter,
    decimal TotalAmount,
    int? OdometerKm,
    DateTime FueledAt);

/// Leituras agregadas para os painéis (gestor e colaborador). A composição
/// (totais, séries, breakdowns) é feita no AnalyticsService a partir destes.
public interface IAnalyticsRepository
{
    Task<IReadOnlyList<AnalyticsUser>> GetUsersAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// Abastecimentos não-deletados da empresa; filtra por usuário se informado.
    Task<IReadOnlyList<AnalyticsEntry>> GetEntriesAsync(Guid companyId, Guid? userId, CancellationToken cancellationToken = default);

    /// userId -> placa do veículo primário ativo.
    Task<IReadOnlyDictionary<Guid, string>> GetUserVehiclePlatesAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// Nome da primeira distribuidora ativa da empresa (para o cabeçalho do painel).
    Task<string?> GetAccountNameAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// Orçamento do mês: do usuário se houver, senão o da empresa.
    Task<decimal?> GetBudgetAsync(Guid companyId, Guid? userId, DateTime month, CancellationToken cancellationToken = default);
}
