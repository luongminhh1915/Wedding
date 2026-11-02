using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Notifications;

public record NotificationDto(Guid Id, string Message, DateTime CreatedAt, DateTime? ReadAt, string? TargetPath);
public record NotificationPageDto(List<NotificationDto> Items, int Page, int PageSize, int TotalCount, int TotalPages, int UnreadCount);
public record GetNotificationsQuery(int Page = 1, int PageSize = 10, bool UnreadOnly = false) : IRequest<NotificationPageDto>;
public class GetNotificationsQueryHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<GetNotificationsQuery, NotificationPageDto>
{
    public async Task<NotificationPageDto> Handle(GetNotificationsQuery r, CancellationToken ct)
    {
        var uid = user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        if (r.Page < 1 || r.PageSize is < 1 or > 20) throw new DomainException("Trang phải từ 1 và số thông báo mỗi trang từ 1 đến 20.");
        var own = db.Notifications.AsNoTracking().Where(n => n.UserId == uid);
        var unreadCount = await own.CountAsync(n => n.ReadAt == null, ct);
        var filtered = r.UnreadOnly ? own.Where(n => n.ReadAt == null) : own;
        var total = await filtered.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)r.PageSize));
        var page = Math.Min(r.Page, totalPages);
        var rows = await filtered.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Skip((page - 1) * r.PageSize).Take(r.PageSize).ToListAsync(ct);
        var links = await NotificationTargets.ResolveAsync(db, uid, user.Role, rows, ct);
        return new(rows.Select(n => new NotificationDto(n.Id, n.Message, n.CreatedAt, n.ReadAt, links.GetValueOrDefault(n.Id))).ToList(),
            page, r.PageSize, total, totalPages, unreadCount);
    }
}

public record ReadNotificationCommand(Guid? Id) : IRequest;
public class ReadNotificationCommandHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<ReadNotificationCommand>
{
    public async Task Handle(ReadNotificationCommand r, CancellationToken ct)
    {
        var uid = user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        if (r.Id.HasValue)
        {
            var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == r.Id && n.UserId == uid, ct)
                ?? throw new NotFoundException("Không tìm thấy thông báo của bạn.");
            notification.Read(); await db.SaveChangesAsync(ct);
        }
        else
        {
            var readAt = DateTime.UtcNow;
            await db.Notifications.Where(n => n.UserId == uid && n.ReadAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, readAt), ct);
        }
    }
}

/// <summary>Links are generated from allowlisted resource kinds after checking current DB ownership and role.
/// Old notifications without context remain readable; never infer a private destination from message text.</summary>
internal static class NotificationTargets
{
    public static async Task<Dictionary<Guid, string>> ResolveAsync(IApplicationDbContext db, Guid uid, string? role,
        List<Notification> rows, CancellationToken ct)
    {
        var result = new Dictionary<Guid, string>();
        foreach (var group in rows.Where(n => n.ContextId.HasValue && n.ContextKind != null).GroupBy(n => n.ContextKind!))
        {
            var ids = group.Select(n => n.ContextId!.Value).Distinct().ToList();
            List<Guid> allowed; string? path;
            switch (group.Key)
            {
                case "contract" when role is "Customer" or "VendorOwner":
                    allowed = await db.BookingContracts.Where(x => ids.Contains(x.Id) &&
                        (role == "Customer" ? x.CustomerId == uid : x.Vendor.UserId == uid)).Select(x => x.Id).ToListAsync(ct);
                    path = role == "Customer" ? "/customer/dashboard?tab=contracts" : "/vendor/dashboard?tab=contracts-couple"; break;
                case "quote" when role is "Customer" or "VendorOwner":
                    allowed = await db.Quotes.Where(x => ids.Contains(x.Id) &&
                        (role == "Customer" ? x.CustomerId == uid : x.Vendor.UserId == uid)).Select(x => x.Id).ToListAsync(ct);
                    path = role == "Customer" ? "/customer/dashboard?tab=contracts&section=quotes" : "/vendor/dashboard?tab=quotes"; break;
                case "lead" when role is "Customer" or "VendorOwner":
                    allowed = await db.Leads.Where(x => ids.Contains(x.Id) &&
                        (role == "Customer" ? x.CustomerId == uid : x.Vendor.UserId == uid)).Select(x => x.Id).ToListAsync(ct);
                    path = role == "Customer" ? "/customer/dashboard?tab=contracts&section=leads" : "/vendor/dashboard?tab=leads"; break;
                case "appointment" when role is "Customer" or "VendorOwner":
                    allowed = await db.Appointments.Where(x => ids.Contains(x.Id) &&
                        (role == "Customer" ? x.CustomerId == uid : x.Vendor.UserId == uid)).Select(x => x.Id).ToListAsync(ct);
                    path = role == "Customer" ? "/customer/dashboard?tab=appointments" : "/vendor/dashboard?tab=appointments"; break;
                case "combo" when role == "VendorOwner":
                    var comboLeads = await db.ComboOrderItems.Where(x => ids.Contains(x.ComboOrderId) && x.Vendor.UserId == uid && x.LeadId != null)
                        .OrderBy(x => x.Id).Select(x => new { x.ComboOrderId, x.LeadId }).ToListAsync(ct);
                    foreach (var notification in group)
                    {
                        var lead = comboLeads.FirstOrDefault(x => x.ComboOrderId == notification.ContextId)?.LeadId;
                        if (lead.HasValue) result[notification.Id] = $"/vendor/dashboard?tab=leads&focus={lead:D}&focusKind=lead";
                    }
                    continue;
                case "combo" when role == "Customer":
                    allowed = await db.ComboOrders.Where(x => ids.Contains(x.Id) && x.CustomerId == uid).Select(x => x.Id).ToListAsync(ct);
                    path = "/customer/dashboard?tab=combos"; break;
                case "task" when role == "Customer":
                    allowed = await db.ChecklistTasks.Where(x => ids.Contains(x.Id) && x.CustomerId == uid).Select(x => x.Id).ToListAsync(ct);
                    path = "/customer/dashboard?tab=checklist"; break;
                case "order-request" when role is "Customer" or "VendorOwner" or "Finance" or "SuperAdmin":
                    allowed = await db.OrderChangeRequests.Where(x => ids.Contains(x.Id) &&
                        (role == "Customer" ? x.CustomerId == uid : role == "VendorOwner" ? db.Vendors.Any(v => v.Id == x.VendorId && v.UserId == uid) : x.RequiresFinance))
                        .Select(x => x.Id).ToListAsync(ct);
                    path = role == "Customer" ? "/customer/dashboard?tab=contracts&section=requests" : role == "VendorOwner"
                        ? "/vendor/dashboard?tab=change-requests" : "/admin/dashboard?tab=change-requests"; break;
                default: continue;
            }
            var owned = allowed.ToHashSet();
            foreach (var notification in group.Where(n => owned.Contains(n.ContextId!.Value)))
                result[notification.Id] = $"{path}&focus={notification.ContextId:D}&focusKind={group.Key}";
        }
        return result;
    }
}
