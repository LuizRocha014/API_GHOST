using Folha.Domain.Entities;

namespace Folha.Application.CreditCards;

internal static class CreditCardMapping
{
    public static CreditCardDto ToDto(this CreditCard c) => new(
        c.Id, c.UserId, c.AccountId, c.Name, c.Brand, c.LastFour, c.CreditLimit,
        c.ClosingDay, c.DueDay, c.IsArchived, c.CreatedAt, c.UpdatedAt);
}
