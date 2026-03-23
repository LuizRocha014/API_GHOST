using Domain.Entities;

namespace Application.Abstractions;

public interface IJwtTokenService
{
    string CreateToken(User user);
}
