namespace Application.Users;

public sealed record UserDto(
    Guid Id,
    string Name,
    string Username,
    string Email,
    bool Active,
    DateTime CreatedAt,
    DateTime UpdatedAt);
