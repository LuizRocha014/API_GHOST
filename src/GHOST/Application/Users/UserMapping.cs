using Domain.Entities;

namespace Application.Users;

internal static class UserMapping
{
    public static UserDto ToDto(this User user) =>
        new(user.Id, user.Name, user.Username, user.Email, user.Active, user.CreatedAt, user.UpdatedAt);
}
