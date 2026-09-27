using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Exceptions;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.DTOs;

namespace Wedding.Application.Features.Auth.Queries;

public record GetCurrentUserQuery(Guid UserId) : IRequest<UserDto>;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserDto>
{
    private readonly IApplicationDbContext _context;

    public GetCurrentUserQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Include(u => u.Vendor)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", request.UserId);

        return new UserDto(
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.Role.ToString(),
            user.AvatarUrl,
            user.Vendor?.Id,
            user.Vendor?.BrandName
        );
    }
}
