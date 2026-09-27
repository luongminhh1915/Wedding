using Wedding.Domain.Entities;

namespace Wedding.Application.Common.Interfaces;

public interface IJwtProvider
{
    string GenerateToken(User user);
}
