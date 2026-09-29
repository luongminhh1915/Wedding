using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Users.Commands.UpdateUserStatus;

public record UpdateUserStatusCommand(Guid UserId, bool IsActive) : IRequest;

public class UpdateUserStatusCommandHandler : IRequestHandler<UpdateUserStatusCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateUserStatusCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Người dùng không tồn tại.");
        }

        if (user.Role == Wedding.Domain.Enums.UserRole.SuperAdmin && !request.IsActive)
        {
            throw new DomainException("Không thể khóa tài khoản Quản trị viên (SuperAdmin).");
        }

        user.SetStatus(request.IsActive);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
