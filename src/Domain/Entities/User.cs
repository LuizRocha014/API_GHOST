namespace Domain.Entities;

public sealed class User : Core
{
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Perfil: <see cref="UserRoles.Owner"/> (tudo) ou <see cref="UserRoles.Staff"/> (o dia a dia da agenda).</summary>
    public string Role { get; set; } = UserRoles.Owner;

    /// <summary>Senha provisória (criada pelo dono ou a do seed): só entra depois de trocar.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Senhas erradas seguidas; zera ao entrar.</summary>
    public int FailedLogins { get; set; }

    /// <summary>Bloqueado por tentativas até este instante (UTC).</summary>
    public DateTime? LockedUntil { get; set; }

    public DateTime? LastLoginAt { get; set; }
}

public static class UserRoles
{
    /// <summary>Dono: ajustes, equipe, filiais, relatórios e exclusões.</summary>
    public const string Owner = "owner";

    /// <summary>Atendente: agenda, clientes, WhatsApp e lembretes.</summary>
    public const string Staff = "staff";

    public static bool IsValid(string? role) => role is Owner or Staff;
}
