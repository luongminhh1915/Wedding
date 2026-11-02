using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Contracts.Queries.GetCustomerContracts;
using Wedding.Application.Features.Leads.Queries.GetCustomerLeads;
using Wedding.Application.Features.Quotes;
using Wedding.Domain.Exceptions;
using Wedding.Domain.Enums;

namespace Wedding.Application.Features.CustomerOrders;

public record GetCustomerOrderPageQuery(string Kind, int Page = 1, int PageSize = 6, string Filter = "all", Guid? FocusId = null) : IRequest<CustomerOrderPageDto>;
public record CustomerOrderPageDto(object Items, List<OrderJourneyItemDto> Metadata, int Page, int PageSize, int TotalCount, string Filter, bool FocusFound);
internal sealed class OrderPageIndex { public Guid Id { get; init; } public Guid VendorId { get; init; } public Guid? ComboId { get; init; } public Guid LeadId { get; init; } public DateTime CreatedAt { get; init; } public bool Closed { get; init; } public bool Pending { get; init; } public bool Confirmed { get; init; } }

/// <summary>Page supplier/combo GROUPS rather than individual rows so one package is never split
/// across pages. Filtering occurs before grouping; full DTOs and images are loaded only for the page.</summary>
public class GetCustomerOrderPageQueryHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<GetCustomerOrderPageQuery, CustomerOrderPageDto>
{
    public async Task<CustomerOrderPageDto> Handle(GetCustomerOrderPageQuery r, CancellationToken ct)
    {
        var uid = user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var size = Math.Clamp(r.PageSize, 1, 10);
        IQueryable<OrderPageIndex> index = r.Kind switch {
            "lead" => db.Leads.Where(l => l.CustomerId == uid).Select(l => new OrderPageIndex { Id = l.Id, VendorId = l.VendorId, ComboId = l.ComboOrderId, LeadId = l.Id, CreatedAt = l.CreatedAt, Closed = l.Status == LeadStatus.Cancelled || l.Status == LeadStatus.Expired, Pending = l.Status == LeadStatus.New }),
            "quote" => db.Quotes.Where(q => q.CustomerId == uid).Select(q => new OrderPageIndex { Id = q.Id, VendorId = q.VendorId, ComboId = q.Lead.ComboOrderId, LeadId = q.LeadId, CreatedAt = q.CreatedAt, Closed = q.Status == QuoteStatus.Rejected || q.Status == QuoteStatus.Expired || q.Status == QuoteStatus.Superseded, Pending = q.Status == QuoteStatus.Sent }),
            "contract" => db.BookingContracts.Where(c => c.CustomerId == uid).Select(c => new OrderPageIndex { Id = c.Id, VendorId = c.VendorId, ComboId = c.Lead.ComboOrderId, LeadId = c.LeadId, CreatedAt = c.CreatedAt, Closed = c.Status == ContractStatus.Completed || c.Status == ContractStatus.Cancelled, Pending = c.Status == ContractStatus.Draft || c.Status == ContractStatus.PendingVerification, Confirmed = c.Status == ContractStatus.Confirmed || c.Status == ContractStatus.Disputed }),
            _ => throw new DomainException("Loại hồ sơ không hợp lệ.")
        };
        var states = db.CustomerOrderStates.Where(s => s.CustomerId == uid && s.Kind == r.Kind);
        var filter = r.Filter;
        var focus = r.FocusId.HasValue ? await index.FirstOrDefaultAsync(i => i.Id == r.FocusId, ct) : null;
        if (focus != null) filter = await states.AnyAsync(s => s.EntityId == focus.Id && s.ArchivedAt != null, ct) ? "archived" : "all";
        if (filter == "archived") index = index.Where(i => states.Any(s => s.EntityId == i.Id && s.ArchivedAt != null));
        else {
            index = index.Where(i => !states.Any(s => s.EntityId == i.Id && s.ArchivedAt != null));
            if (filter == "closed") index = index.Where(i => i.Closed);
            else if (filter == "pending") index = index.Where(i => i.Pending);
            else if (filter == "confirmed" && r.Kind == "contract") index = index.Where(i => i.Confirmed);
            else if (filter == "active") index = index.Where(i => !i.Closed);
        }
        var groupQuery = index.GroupBy(i => new { i.VendorId, i.ComboId })
            .Select(g => new { g.Key.VendorId, g.Key.ComboId, Newest = g.Max(i => i.CreatedAt) })
            .OrderByDescending(g => g.Newest).ThenBy(g => g.VendorId).ThenBy(g => g.ComboId);
        var total = await groupQuery.CountAsync(ct);
        var page = Math.Clamp(r.Page, 1, Math.Max(1, (total + size - 1) / size));
        if (focus != null) {
            var keys = await groupQuery.ToListAsync(ct);
            var position = keys.FindIndex(k => k.VendorId == focus.VendorId && k.ComboId == focus.ComboId);
            if (position >= 0) page = position / size + 1;
        }
        var groups = await groupQuery.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var vendors = groups.Select(g => g.VendorId).ToList();
        var possible = await index.Where(i => vendors.Contains(i.VendorId)).ToListAsync(ct);
        var chosen = possible.Where(i => groups.Any(g => g.VendorId == i.VendorId && g.ComboId == i.ComboId)).ToList();
        var ids = chosen.Select(i => i.Id).ToList();
        object items = r.Kind switch {
            "lead" => await new GetCustomerLeadsQueryHandler(db, user).Handle(new(1, Math.Max(1, ids.Count), ids), ct),
            "quote" => await new GetQuotesQueryHandler(db, user).Handle(new(null, ids), ct),
            _ => await new GetCustomerContractsQueryHandler(db, user).Handle(new(1, Math.Max(1, ids.Count), ids), ct)
        };
        var metadata = await CustomerOrderJourney.LoadAsync(db, uid, ct, onlyLeadIds: chosen.Select(i => i.LeadId).Distinct().ToList());
        return new(items, metadata.Where(m => m.Kind == r.Kind && ids.Contains(m.Id)).ToList(), page, size, total, filter, focus != null);
    }
}
