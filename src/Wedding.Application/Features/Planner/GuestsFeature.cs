using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Planner;

public record GuestInput(Guid? Id, string Name, string? Phone = null, string Group = "Khác", string Side = "Shared", int PartySize = 1,
    string Status = "Pending", string? Dietary = null, string? Notes = null, Guid? TableId = null);
public record GuestDto(Guid Id, string Name, string? Phone, string Group, string Side, int PartySize, string Status, string? Dietary, string? Notes, Guid? TableId);
public record TableDto(Guid Id, string Name, int Capacity, int Assigned);
public record GuestsOverviewDto(string MyRole, List<GuestDto> Guests, List<TableDto> Tables);
internal static class GuestMap
{
    public static GuestDto Dto(WeddingGuest g) => new(g.Id, g.Name, g.Phone, g.Group, g.Side, g.PartySize, g.Status, g.Dietary, g.Notes, g.TableId);
    public static async Task<Guid> WeddingId(IApplicationDbContext db, ICurrentUserService user, CancellationToken ct, bool write)
        => (await WeddingAccess.ResolveAsync(db, user, ct, write)).ProfileId ?? throw new DomainException("Hãy hoàn thành hồ sơ cưới trước.");
}
public record GetWeddingGuestsQuery : IRequest<GuestsOverviewDto>;
public class GetWeddingGuestsHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<GetWeddingGuestsQuery, GuestsOverviewDto>
{
    public async Task<GuestsOverviewDto> Handle(GetWeddingGuestsQuery r, CancellationToken ct)
    {
        var scope = await WeddingAccess.ResolveAsync(db, user, ct);
        var guests = await db.WeddingGuests.AsNoTracking().Where(g => g.WeddingProfileId == scope.ProfileId).OrderBy(g => g.Group).ThenBy(g => g.Name).ToListAsync(ct);
        var tables = await db.SeatTables.AsNoTracking().Where(t => t.WeddingProfileId == scope.ProfileId).OrderBy(t => t.Name).ToListAsync(ct);
        return new(scope.Role, guests.Select(GuestMap.Dto).ToList(), tables.Select(t => new TableDto(t.Id, t.Name, t.Capacity, guests.Where(g => g.TableId == t.Id).Sum(g => g.PartySize))).ToList());
    }
}
public record SaveWeddingGuestsCommand(List<GuestInput> Guests) : IRequest<List<GuestDto>>;
public class SaveWeddingGuestsHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<SaveWeddingGuestsCommand, List<GuestDto>>
{
    public Task<List<GuestDto>> Handle(SaveWeddingGuestsCommand r, CancellationToken ct) => db.ExecuteSerializableAsync(inner => Save(r, inner), ct);
    private async Task<List<GuestDto>> Save(SaveWeddingGuestsCommand r, CancellationToken ct)
    {
        var wedding = await GuestMap.WeddingId(db, user, ct, true);
        if (r.Guests.Count is < 1 or > 1000) throw new DomainException("Mỗi lần nhập từ 1 đến 1000 khách.");
        var existing = await db.WeddingGuests.Where(x => x.WeddingProfileId == wedding).ToListAsync(ct);
        var tables = await db.SeatTables.Where(x => x.WeddingProfileId == wedding).ToListAsync(ct);
        var result = new List<WeddingGuest>();
        foreach (var input in r.Guests)
        {
            var guest = input.Id.HasValue ? existing.FirstOrDefault(x => x.Id == input.Id) ?? throw new DomainException("Không tìm thấy khách mời.") : WeddingGuest.Create(wedding);
            guest.Update(input.Name, input.Phone, input.Group, input.Side, input.PartySize, input.Status, input.Dietary, input.Notes);
            if (input.TableId != null && !tables.Any(t => t.Id == input.TableId)) throw new DomainException("Bàn không thuộc hồ sơ cưới.");
            guest.AssignTable(input.Status == "NotAttending" ? null : input.TableId);
            if (!input.Id.HasValue) { db.WeddingGuests.Add(guest); existing.Add(guest); }
            result.Add(guest);
        }
        foreach (var t in tables)
            if (existing.Where(g => g.TableId == t.Id).Sum(g => g.PartySize) > t.Capacity) throw new DomainException($"Bàn {t.Name} không đủ ghế.");
        await db.SaveChangesAsync(ct); return result.Select(GuestMap.Dto).ToList();
    }
}
public record DeleteWeddingGuestCommand(Guid Id) : IRequest<Unit>;
public class DeleteWeddingGuestHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<DeleteWeddingGuestCommand, Unit>
{
    public async Task<Unit> Handle(DeleteWeddingGuestCommand r, CancellationToken ct)
    {
        var wedding = await GuestMap.WeddingId(db, user, ct, true);
        var g = await db.WeddingGuests.FirstOrDefaultAsync(x => x.Id == r.Id && x.WeddingProfileId == wedding, ct) ?? throw new DomainException("Không tìm thấy khách.");
        db.WeddingGuests.Remove(g); await db.SaveChangesAsync(ct); return Unit.Value;
    }
}
public record SaveSeatTableCommand(Guid? Id, string Name, int Capacity) : IRequest<TableDto>;
public class SaveSeatTableHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<SaveSeatTableCommand, TableDto>
{
    public Task<TableDto> Handle(SaveSeatTableCommand r, CancellationToken ct) => db.ExecuteSerializableAsync(inner => Save(r, inner), ct);
    private async Task<TableDto> Save(SaveSeatTableCommand r, CancellationToken ct)
    {
        var wedding = await GuestMap.WeddingId(db, user, ct, true);
        var t = r.Id.HasValue ? await db.SeatTables.FirstOrDefaultAsync(x => x.Id == r.Id && x.WeddingProfileId == wedding, ct) ?? throw new DomainException("Không tìm thấy bàn.") : SeatTable.Create(wedding, r.Name, r.Capacity);
        var guests = await db.WeddingGuests.Where(g => g.TableId == t.Id && g.WeddingProfileId == wedding).ToListAsync(ct);
        if (guests.Sum(g => g.PartySize) > r.Capacity) throw new DomainException("Sức chứa nhỏ hơn số khách đã xếp.");
        t.Update(r.Name, r.Capacity); if (!r.Id.HasValue) db.SeatTables.Add(t);
        await db.SaveChangesAsync(ct); return new(t.Id, t.Name, t.Capacity, guests.Sum(g => g.PartySize));
    }
}
public record DeleteSeatTableCommand(Guid Id) : IRequest<Unit>;
public class DeleteSeatTableHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<DeleteSeatTableCommand, Unit>
{
    public async Task<Unit> Handle(DeleteSeatTableCommand r, CancellationToken ct)
    {
        var wedding = await GuestMap.WeddingId(db, user, ct, true);
        var table = await db.SeatTables.FirstOrDefaultAsync(x => x.Id == r.Id && x.WeddingProfileId == wedding, ct) ?? throw new DomainException("Không tìm thấy bàn.");
        foreach (var g in await db.WeddingGuests.Where(g => g.TableId == table.Id && g.WeddingProfileId == wedding).ToListAsync(ct)) g.AssignTable(null);
        db.SeatTables.Remove(table); await db.SaveChangesAsync(ct); return Unit.Value;
    }
}
public record AutoAssignTablesCommand : IRequest<int>;
public class AutoAssignTablesHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<AutoAssignTablesCommand, int>
{
    public Task<int> Handle(AutoAssignTablesCommand r, CancellationToken ct) => db.ExecuteSerializableAsync(inner => Assign(r, inner), ct);
    private async Task<int> Assign(AutoAssignTablesCommand r, CancellationToken ct)
    {
        var wedding = await GuestMap.WeddingId(db, user, ct, true);
        var tables = await db.SeatTables.Where(t => t.WeddingProfileId == wedding).OrderBy(t => t.Name).ToListAsync(ct);
        var guests = await db.WeddingGuests.Where(g => g.WeddingProfileId == wedding).ToListAsync(ct);
        // Preserve occupied seats. Split only unseated parties so every free seat can be used without exceeding capacity.
        var remaining = tables.ToDictionary(t => t.Id, t => Math.Max(0, t.Capacity - guests.Where(g => g.TableId == t.Id).Sum(g => g.PartySize)));
        var placed = 0;
        foreach (var g in guests.Where(g => g.TableId == null && g.Status == "Attending").OrderBy(g => g.Side).ThenBy(g => g.Group).ThenByDescending(g => g.PartySize).ThenBy(g => g.Id).ToList())
        {
            foreach (var table in tables.Where(t => remaining[t.Id] > 0))
            {
                var people = Math.Min(remaining[table.Id], g.PartySize);
                var seatingGroup = people == g.PartySize ? g : g.SplitParty(people);
                seatingGroup.AssignTable(table.Id);
                if (seatingGroup != g) db.WeddingGuests.Add(seatingGroup);
                remaining[table.Id] -= people; placed += people;
                if (seatingGroup == g) break;
            }
        }
        await db.SaveChangesAsync(ct); return placed;
    }
}
