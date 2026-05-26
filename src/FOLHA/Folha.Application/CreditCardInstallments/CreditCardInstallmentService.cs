using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Application.CreditCardInstallments;

public sealed class CreditCardInstallmentService : ICreditCardInstallmentService
{
    private readonly ICreditCardInstallmentRepository _repository;
    private readonly ICreditCardRepository _cardRepository;

    public CreditCardInstallmentService(
        ICreditCardInstallmentRepository repository,
        ICreditCardRepository cardRepository)
    {
        _repository = repository;
        _cardRepository = cardRepository;
    }

    public async Task<IReadOnlyList<CreditCardInstallmentDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return items.Select(i => i.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<CreditCardInstallmentDto>> ListByCardAsync(Guid creditCardId, Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllByCardAsync(creditCardId, userId, cancellationToken).ConfigureAwait(false);
        return items.Select(i => i.ToDto()).ToList();
    }

    public async Task<CreditCardInstallmentDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        return item?.ToDto();
    }

    public async Task<CreditCardInstallmentDto> CreateAsync(Guid userId, CreateCreditCardInstallmentRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.InstallmentTotal, request.InstallmentsPaid, request.InstallmentAmount, request.Description);

        // Garante que o cartão existe e pertence ao usuário (evita FK órfã / vazamento entre usuários).
        var card = await _cardRepository.GetByIdAsync(request.CreditCardId, userId, cancellationToken).ConfigureAwait(false);
        if (card is null)
            throw new InvalidOperationException("Cartão não encontrado para este usuário.");

        var utc = DateTime.UtcNow;
        var entity = new CreditCardInstallment
        {
            Id = request.Id ?? Guid.NewGuid(),
            UserId = userId,
            CreditCardId = request.CreditCardId,
            Description = request.Description.Trim(),
            InstallmentTotal = request.InstallmentTotal,
            InstallmentsPaid = request.InstallmentsPaid,
            InstallmentAmount = request.InstallmentAmount,
            StartDate = request.StartDate.Date,
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }

    public async Task<CreditCardInstallmentDto?> UpdateAsync(Guid id, Guid userId, UpdateCreditCardInstallmentRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return null;

        Validate(request.InstallmentTotal, request.InstallmentsPaid, request.InstallmentAmount, request.Description);

        entity.Description = request.Description.Trim();
        entity.InstallmentTotal = request.InstallmentTotal;
        entity.InstallmentsPaid = request.InstallmentsPaid;
        entity.InstallmentAmount = request.InstallmentAmount;
        entity.StartDate = request.StartDate.Date;
        entity.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return ok ? entity.ToDto() : null;
    }

    public Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, userId, cancellationToken);

    private static void Validate(short total, short paid, decimal amount, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidOperationException("Descrição do parcelamento é obrigatória.");
        if (total < 1)
            throw new InvalidOperationException("Total de parcelas deve ser ao menos 1.");
        if (paid < 0 || paid > total)
            throw new InvalidOperationException("Parcelas pagas deve estar entre 0 e o total.");
        if (amount <= 0)
            throw new InvalidOperationException("Valor da parcela deve ser positivo.");
    }
}
