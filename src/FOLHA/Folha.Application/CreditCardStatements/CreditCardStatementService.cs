using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Application.CreditCardStatements;

public sealed class CreditCardStatementService : ICreditCardStatementService
{
    private static readonly HashSet<string> ValidStatus =
        new(StringComparer.OrdinalIgnoreCase) { "open", "closed", "paid", "partial", "overdue" };

    private readonly ICreditCardStatementRepository _repository;

    public CreditCardStatementService(ICreditCardStatementRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<CreditCardStatementDto>> ListByCardAsync(Guid creditCardId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllByCardAsync(creditCardId, cancellationToken).ConfigureAwait(false);
        return items.Select(s => s.ToDto()).ToList();
    }

    public async Task<CreditCardStatementDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return s?.ToDto();
    }

    public async Task<CreditCardStatementDto> CreateAsync(CreateCreditCardStatementRequest request, CancellationToken cancellationToken = default)
    {
        var status = (request.Status ?? "open").Trim().ToLowerInvariant();
        if (!ValidStatus.Contains(status))
            throw new InvalidOperationException("Status inválido.");

        var utc = DateTime.UtcNow;
        var entity = new CreditCardStatement
        {
            Id = Guid.NewGuid(),
            CreditCardId = request.CreditCardId,
            ReferenceMonth = request.ReferenceMonth.Date,
            ClosingDate = request.ClosingDate.Date,
            DueDate = request.DueDate.Date,
            TotalAmount = request.TotalAmount,
            PaidAmount = request.PaidAmount,
            Status = status,
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }

    public async Task<CreditCardStatementDto?> UpdateAsync(Guid id, UpdateCreditCardStatementRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return null;

        var status = (request.Status ?? "open").Trim().ToLowerInvariant();
        if (!ValidStatus.Contains(status))
            throw new InvalidOperationException("Status inválido.");

        entity.ClosingDate = request.ClosingDate.Date;
        entity.DueDate = request.DueDate.Date;
        entity.TotalAmount = request.TotalAmount;
        entity.PaidAmount = request.PaidAmount;
        entity.Status = status;
        entity.PaidAt = request.PaidAt;
        entity.PaidFromAccountId = request.PaidFromAccountId;
        entity.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return ok ? entity.ToDto() : null;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, cancellationToken);
}
