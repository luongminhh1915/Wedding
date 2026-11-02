using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Users.Commands.UpdateUserStatus;

public record UpdateUserStatusCommand(Guid UserId, bool IsActive) : IRequest;

public class UpdateUserStatusCommandHandler : IRequestHandler<UpdateUserStatusCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _current;

    public UpdateUserStatusCommandHandler(IApplicationDbContext context, ICurrentUserService current)
    {
        _context = context;
        _current = current;
    }

    public async Task Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId == null || _current.Role is not ("SuperAdmin" or "Moderator"))
            throw new DomainException("Bạn không có quyền quản lý tài khoản.");
        var actor = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == _current.UserId && u.IsActive, cancellationToken);
        if (actor == null || actor.Role.ToString() != _current.Role)
            throw new DomainException("Tài khoản không còn quyền quản lý người dùng.");
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Người dùng không tồn tại.");
        }

        if (user.Role == Wedding.Domain.Enums.UserRole.SuperAdmin && !request.IsActive)
        {
            throw new DomainException("Không thể khóa tài khoản Quản trị viên (SuperAdmin).");
        }

        if (_current.Role == "Moderator" && user.Role is not (Wedding.Domain.Enums.UserRole.Customer or Wedding.Domain.Enums.UserRole.VendorOwner))
            throw new DomainException("Kiểm duyệt viên chỉ được quản lý tài khoản khách hàng và nhà cung cấp.");

        user.SetStatus(request.IsActive);
        _context.AuditLogs.Add(Wedding.Domain.Entities.AuditLog.Create(_current.UserId, _current.Role, "User.Status", "User", user.Id.ToString(),
            System.Text.Json.JsonSerializer.Serialize(new { request.IsActive })));
        await _context.SaveChangesAsync(cancellationToken);
    }
}
