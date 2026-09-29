using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Users.DTOs;

namespace Wedding.Application.Features.Users.Queries.GetUsers;

public record GetUsersQuery : IRequest<List<UserManagementDto>>;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, List<UserManagementDto>>
{
    private readonly IApplicationDbContext _context;

    public GetUsersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserManagementDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _context.Users
            .Include(u => u.Vendor)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(cancellationToken);

        return users.Select(u => new UserManagementDto(
            u.Id,
            u.FullName,
            u.Email,
            u.PhoneNumber,
            u.Role.ToString(),
            u.AvatarUrl,
            u.IsActive,
            u.CreatedAt,
            u.LastLoginAt,
            u.Vendor?.Id,
            u.Vendor?.BrandName
        )).ToList();
    }
}
