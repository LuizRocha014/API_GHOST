using Application.Abstractions;
using Domain.Entities;

namespace Application.Accesses;

public sealed class AccessService : IAccessService
{
    private readonly IAccessRepository _accesses;

    public AccessService(IAccessRepository accesses)
    {
        _accesses = accesses;
    }

    public async Task<IReadOnlyList<AccessDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var list = await _accesses.ListActiveAsync(cancellationToken).ConfigureAwait(false);
        return list.Select(x => x.ToDto()).ToList();
    }

    public async Task<AccessDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var e = await _accesses.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return e?.ToDto();
    }

    public async Task<AccessDto> CreateAsync(CreateAccessRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrEmpty(name))
            throw new InvalidOperationException("Nome do acesso é obrigatório.");

        string? code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
        if (code is not null &&
            await _accesses.CodeExistsAsync(code.ToLowerInvariant(), null, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Já existe um acesso ativo com este código.");

        var utc = DateTime.UtcNow;
        var entity = new Access
        {
            Id = Guid.NewGuid(),
            Name = name,
            Code = code,
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true
        };

        var created = await _accesses.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }
}
