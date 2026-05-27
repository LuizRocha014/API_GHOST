namespace Abasta.Application.Abstractions;

public interface IVehicleAssignmentRepository
{
    /// <summary>Define o motorista primário de um veículo, encerrando vínculos
    /// ativos anteriores do veículo e do usuário.</summary>
    Task AssignPrimaryAsync(Guid companyId, Guid vehicleId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Veículo (id) que o usuário dirige atualmente, se houver.</summary>
    Task<Guid?> GetActiveVehicleIdAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default);
}
