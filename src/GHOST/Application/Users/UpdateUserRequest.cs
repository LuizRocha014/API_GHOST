namespace Application.Users;

public sealed class UpdateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public string? Password { get; set; }
}
