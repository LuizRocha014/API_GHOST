using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface IJwtTokenService
{
    string CreateToken(User user);
}
