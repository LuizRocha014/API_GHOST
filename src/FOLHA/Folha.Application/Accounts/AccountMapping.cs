using Folha.Domain.Entities;

namespace Folha.Application.Accounts;

internal static class AccountMapping
{
    public static AccountDto ToDto(this Account a) => new(
        a.Id, a.UserId, a.Name, a.Kind, a.Institution, a.Icon, a.ColorHex,
        a.InitialBalance, a.CurrencyCode, a.IsArchived, a.IncludeInTotal,
        a.SortOrder, a.CreatedAt, a.UpdatedAt);
}
