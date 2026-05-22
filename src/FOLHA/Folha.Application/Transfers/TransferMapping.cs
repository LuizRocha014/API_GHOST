using Folha.Domain.Entities;

namespace Folha.Application.Transfers;

internal static class TransferMapping
{
    public static TransferDto ToDto(this Transfer t) => new(
        t.Id, t.UserId, t.FromAccountId, t.ToAccountId, t.Amount, t.Fee,
        t.OccurredAt, t.Description, t.Notes, t.CreatedAt, t.UpdatedAt);
}
