namespace Application.Agenda;

/// <summary>O que a API confere a cada chamada da agenda: usuário ativo, perfil, senha provisória e acesso à filial.</summary>
public sealed record AgendaUserAccess(bool Active, string Role, bool MustChangePassword, bool BranchAllowed);

/// <summary>Pessoa da equipe. <c>BranchIds</c> vazio = todas as filiais.</summary>
public sealed record TeamMemberDto(
    string Id,
    string Name,
    string Username,
    string Role,
    bool Active,
    bool MustChangePassword,
    bool Locked,
    string? LastLoginAt,
    IReadOnlyList<string> BranchIds);

/// <summary>Cadastro ou alteração de uma pessoa da equipe.</summary>
public sealed class TeamMemberRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Login (só na criação; depois não muda).</summary>
    public string? Username { get; set; }

    public string Role { get; set; } = Domain.Entities.UserRoles.Staff;
    public bool Active { get; set; } = true;

    /// <summary>Filiais que a pessoa abre; vazio = todas.</summary>
    public List<string> BranchIds { get; set; } = [];

    /// <summary>Senha provisória: obrigatória na criação; na alteração, redefine a senha (a pessoa troca ao entrar).</summary>
    public string? TemporaryPassword { get; set; }
}

/// <summary>Filial da empresa (para o dono renomear e cadastrar).</summary>
public sealed record CompanyBranchDto(string Id, string Name, string CompanyId, string CompanyName);

public sealed class NameRequest
{
    public string Name { get; set; } = string.Empty;
}

public sealed record TeamMemberRow(
    Guid Id, string Name, string Username, string Role, bool Active, bool MustChangePassword, DateTime? LockedUntil, DateTime? LastLoginAt);
