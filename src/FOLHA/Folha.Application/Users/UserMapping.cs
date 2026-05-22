using Folha.Domain.Entities;

namespace Folha.Application.Users;

internal static class UserMapping
{
    public static UserDto ToDto(this User u) => new(
        u.Id, u.Email, u.FullName, u.DisplayName, u.AvatarUrl,
        u.Timezone, u.Locale, u.CurrencyCode, u.IsActive, u.EmailVerified,
        u.LastLoginAt, u.CreatedAt, u.UpdatedAt);
}
