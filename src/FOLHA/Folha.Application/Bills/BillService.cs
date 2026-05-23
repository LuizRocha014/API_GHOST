using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Application.Bills;

public sealed class BillService : IBillService
{
    private static readonly HashSet<string> ValidKinds =
        new(StringComparer.OrdinalIgnoreCase) { "payable", "receivable" };

    private static readonly HashSet<string> ValidStatus =
        new(StringComparer.OrdinalIgnoreCase) { "pending", "paid", "received", "overdue", "cancelled" };

    private readonly IBillRepository _repository;

    public BillService(IBillRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<BillDto>> ListAsync(Guid userId, DateTime? modifiedSince = null, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllByUserAsync(userId, modifiedSince, cancellationToken).ConfigureAwait(false);
        return items.Select(b => b.ToDto()).ToList();
    }

    public async Task<BillDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var b = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        return b?.ToDto();
    }

    public async Task<BillDto> CreateAsync(Guid userId, CreateBillRequest request, CancellationToken cancellationToken = default)
    {
        var kind = (request.Kind ?? "payable").Trim().ToLowerInvariant();
        if (!ValidKinds.Contains(kind))
            throw new InvalidOperationException("Kind deve ser payable ou receivable.");
        if (request.Amount <= 0)
            throw new InvalidOperationException("Amount deve ser positivo.");
        ValidateInstallment(request.InstallmentCurrent, request.InstallmentTotal);

        var utc = DateTime.UtcNow;
        var entity = new Bill
        {
            Id = request.Id ?? Guid.NewGuid(),
            UserId = userId,
            AccountId = request.AccountId,
            CategoryId = request.CategoryId,
            RecurrenceId = request.RecurrenceId,
            Description = request.Description.Trim(),
            Amount = request.Amount,
            Kind = kind,
            DueDate = request.DueDate.Date,
            Status = "pending",
            PaidAmount = 0,
            InstallmentCurrent = request.InstallmentCurrent,
            InstallmentTotal = request.InstallmentTotal,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }

    public async Task<BillDto?> UpdateAsync(Guid id, Guid userId, UpdateBillRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return null;

        var kind = (request.Kind ?? "payable").Trim().ToLowerInvariant();
        var status = (request.Status ?? "pending").Trim().ToLowerInvariant();
        if (!ValidKinds.Contains(kind))
            throw new InvalidOperationException("Kind deve ser payable ou receivable.");
        if (!ValidStatus.Contains(status))
            throw new InvalidOperationException("Status inválido.");
        ValidateInstallment(request.InstallmentCurrent, request.InstallmentTotal);

        // PaidAmount: aceita o valor enviado, com clamp [0, Amount].
        // Quando ausente, mantém o valor atual.
        var paidAmount = request.PaidAmount ?? entity.PaidAmount;
        if (paidAmount < 0) paidAmount = 0;
        if (paidAmount > request.Amount) paidAmount = request.Amount;

        // Se o status pedido é de quitação, força paid_amount = amount.
        var isSettling = status == "paid" || status == "received";
        if (isSettling) paidAmount = request.Amount;

        entity.AccountId = request.AccountId;
        entity.CategoryId = request.CategoryId;
        entity.RecurrenceId = request.RecurrenceId;
        entity.Description = request.Description.Trim();
        entity.Amount = request.Amount;
        entity.Kind = kind;
        entity.DueDate = request.DueDate.Date;
        entity.Status = status;
        entity.PaidAmount = paidAmount;
        entity.PaidAt = request.PaidAt ?? (isSettling ? DateTime.UtcNow : null);
        entity.PaidTransactionId = request.PaidTransactionId;
        entity.InstallmentCurrent = request.InstallmentCurrent ?? entity.InstallmentCurrent;
        entity.InstallmentTotal = request.InstallmentTotal ?? entity.InstallmentTotal;
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return ok ? entity.ToDto() : null;
    }

    public Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, userId, cancellationToken);

    private static void ValidateInstallment(short? current, short? total)
    {
        if (current.HasValue || total.HasValue)
        {
            if (!current.HasValue || !total.HasValue)
                throw new InvalidOperationException("Informe parcela atual e total juntos.");
            if (total < 1 || current < 1 || current > total)
                throw new InvalidOperationException("Parcela inválida.");
        }
    }
}
