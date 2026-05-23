namespace Folha.Application.Bills;

public interface IBillService
{
    Task<IReadOnlyList<BillDto>> ListAsync(Guid userId, DateTime? modifiedSince = null, CancellationToken cancellationToken = default);
    Task<BillDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<BillDto> CreateAsync(Guid userId, CreateBillRequest request, CancellationToken cancellationToken = default);
    Task<BillDto?> UpdateAsync(Guid id, Guid userId, UpdateBillRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
