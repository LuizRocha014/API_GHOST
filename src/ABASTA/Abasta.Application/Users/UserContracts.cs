namespace Abasta.Application.Users;

public sealed record UserDto(
    Guid Id,
    Guid CompanyId,
    string Email,
    string FullName,
    string DisplayName,
    string Role,
    string? JobTitle,
    string? Phone,
    string? AvatarUrl,
    bool IsActive,
    bool EmailVerified,
    DateTime CreatedAt);

/// <summary>Gestor cria um colaborador (ou outro gestor) dentro da empresa.</summary>
public sealed record CreateUserRequest(
    string FullName,
    string Email,
    string Password,
    string Role = "collaborator",
    string? JobTitle = null,
    string? Phone = null);

/// <summary>Atualização do próprio perfil.</summary>
public sealed record UpdateUserRequest(
    string FullName,
    string DisplayName,
    string? JobTitle,
    string? Phone,
    string? AvatarUrl);
