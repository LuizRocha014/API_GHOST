using Application.Abstractions;
using Application.Auth;
using Domain.Entities;

namespace Application.Agenda;

/// <summary>
/// Equipe da agenda: o dono cadastra atendentes e outros donos, escolhe as filiais de cada um, redefine senha (provisória,
/// trocada no primeiro acesso) e desativa. Sempre fica pelo menos um dono ativo, e ninguém tira o próprio acesso de dono.
/// </summary>
public sealed class TeamService(ITeamRepository repo, IUserRepository users, IPasswordHasher hasher, IAgendaClock clock)
{
    public const int MaxNameLength = 120;

    public async Task<IReadOnlyList<TeamMemberDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await repo.ListMembersAsync(ct);
        var links = (await repo.ListMemberBranchesAsync(ct)).ToLookup(x => x.UserId, x => x.BranchId);
        var now = DateTime.UtcNow;
        return rows.Select(r => ToDto(r, links[r.Id], now)).ToList();
    }

    public async Task<TeamMemberDto> CreateAsync(TeamMemberRequest req, CancellationToken ct = default)
    {
        var name = Name(req.Name);
        var username = (req.Username ?? string.Empty).Trim().ToLowerInvariant();
        if (username.Length is < 3 or > 40 || !username.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
            throw new AgendaValidationException("Login: de 3 a 40 letras sem acento, números, ponto, hífen ou sublinhado.");
        if (await users.UsernameExistsAsync(username, null, ct))
            throw new AgendaConflictException("Esse login já existe. Escolha outro.");
        var role = Role(req.Role);
        var branches = await BranchIdsAsync(req.BranchIds, ct);
        var password = req.TemporaryPassword?.Trim() ?? string.Empty;
        if (PasswordPolicy.Problem(password, username) is { } problem) throw new AgendaValidationException(problem);

        var email = $"{username}@equipe.local";
        for (var i = 2; await users.EmailExistsAsync(email, null, ct); i++) email = $"{username}{i}@equipe.local";

        var utc = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Username = username,
            Email = email,
            PasswordHash = hasher.Hash(password),
            Role = role,
            MustChangePassword = true,
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = req.Active,
        };
        await repo.SaveMemberAsync(user, branches, isNew: true, ct);
        return await GetAsync(user.Id, ct);
    }

    public async Task<TeamMemberDto> UpdateAsync(Guid actingUserId, Guid id, TeamMemberRequest req, CancellationToken ct = default)
    {
        var user = await repo.GetUserAsync(id, ct) ?? throw new AgendaNotFoundException("Pessoa da equipe não encontrada.");
        var role = Role(req.Role);
        var losesOwner = user.Role == UserRoles.Owner && user.Active && (role != UserRoles.Owner || !req.Active);
        if (losesOwner && id == actingUserId)
            throw new AgendaValidationException("Você não pode tirar o seu próprio acesso de dono. Peça a outro dono.");
        if (losesOwner && await repo.CountActiveOwnersAsync(id, ct) == 0)
            throw new AgendaValidationException("A agenda precisa de pelo menos um dono ativo.");

        user.Name = Name(req.Name);
        user.Role = role;
        user.Active = req.Active;
        if (!string.IsNullOrWhiteSpace(req.TemporaryPassword))
        {
            var password = req.TemporaryPassword.Trim();
            if (PasswordPolicy.Problem(password, user.Username) is { } problem) throw new AgendaValidationException(problem);
            user.PasswordHash = hasher.Hash(password);
            user.MustChangePassword = true;
            user.FailedLogins = 0;
            user.LockedUntil = null;
        }
        user.UpdatedAt = DateTime.UtcNow;
        await repo.SaveMemberAsync(user, await BranchIdsAsync(req.BranchIds, ct), isNew: false, ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Senha provisória sugerida (o dono pode digitar outra).</summary>
    public static string SuggestPassword() => PasswordPolicy.Temporary();

    /* ---------- Filiais ---------- */

    public async Task<IReadOnlyList<CompanyBranchDto>> BranchesAsync(Guid branchId, CancellationToken ct = default) =>
        await repo.ListCompanyBranchesAsync(await CompanyAsync(branchId, ct), ct);

    public async Task<CompanyBranchDto> CreateBranchAsync(Guid branchId, string name, CancellationToken ct = default)
    {
        var company = await CompanyAsync(branchId, ct);
        var id = await repo.CreateBranchAsync(company, Name(name, "Nome da filial"), ct);
        return (await repo.ListCompanyBranchesAsync(company, ct)).First(b => b.Id == id.ToString());
    }

    public async Task<CompanyBranchDto> RenameBranchAsync(Guid branchId, Guid id, string name, CancellationToken ct = default)
    {
        var company = await CompanyAsync(branchId, ct);
        if (!await repo.RenameBranchAsync(company, id, Name(name, "Nome da filial"), ct))
            throw new AgendaNotFoundException("Filial não encontrada.");
        return (await repo.ListCompanyBranchesAsync(company, ct)).First(b => b.Id == id.ToString());
    }

    /// <summary>Desativa outra filial (não a que está aberta; sempre fica pelo menos uma).</summary>
    public async Task DeactivateBranchAsync(Guid branchId, Guid id, CancellationToken ct = default)
    {
        if (id == branchId) throw new AgendaValidationException("Abra outra filial para desativar esta.");
        var company = await CompanyAsync(branchId, ct);
        if (!await repo.DeactivateBranchAsync(company, id, ct)) throw new AgendaNotFoundException("Filial não encontrada.");
    }

    public async Task RenameCompanyAsync(Guid branchId, string name, CancellationToken ct = default) =>
        await repo.RenameCompanyAsync(await CompanyAsync(branchId, ct), Name(name, "Nome da empresa"), ct);

    /* ---------- Auxiliares ---------- */

    private async Task<TeamMemberDto> GetAsync(Guid id, CancellationToken ct) =>
        (await ListAsync(ct)).First(m => m.Id == id.ToString());

    private async Task<Guid> CompanyAsync(Guid branchId, CancellationToken ct) =>
        await repo.CompanyOfBranchAsync(branchId, ct) ?? throw new AgendaNotFoundException("Filial não encontrada.");

    private async Task<IReadOnlyList<Guid>> BranchIdsAsync(IEnumerable<string>? ids, CancellationToken ct)
    {
        var list = new List<Guid>();
        foreach (var raw in (ids ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            if (!Guid.TryParse(raw, out var id) || await repo.CompanyOfBranchAsync(id, ct) is null)
                throw new AgendaValidationException("Filial inválida.");
            list.Add(id);
        }
        return list;
    }

    private static string Role(string? role) =>
        UserRoles.IsValid(role) ? role! : throw new AgendaValidationException("Perfil inválido: use dono ou atendente.");

    private static string Name(string? name, string field = "Nome")
    {
        var n = (name ?? string.Empty).Trim();
        if (n.Length == 0) throw new AgendaValidationException($"Informe o {field.ToLowerInvariant()}.");
        if (n.Length > MaxNameLength) throw new AgendaValidationException($"{field}: no máximo {MaxNameLength} caracteres.");
        return n;
    }

    private TeamMemberDto ToDto(TeamMemberRow r, IEnumerable<Guid> branches, DateTime utcNow) => new(
        r.Id.ToString(),
        r.Name,
        r.Username,
        UserRoles.IsValid(r.Role) ? r.Role : UserRoles.Owner,
        r.Active,
        r.MustChangePassword,
        r.LockedUntil is { } until && until > utcNow,
        r.LastLoginAt is { } at ? AgendaFormat.LocalDateTime(clock.ToLocal(at)) : null,
        branches.Select(b => b.ToString()).ToList());
}
