using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class WeddingGuest : BaseEntity
{
    public Guid WeddingProfileId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string Group { get; private set; } = "Khác";
    public string Side { get; private set; } = "Shared";
    public int PartySize { get; private set; } = 1;
    public string Status { get; private set; } = "Pending";
    public string? Dietary { get; private set; }
    public string? Notes { get; private set; }
    public Guid? TableId { get; private set; }
    public Guid? RsvpId { get; private set; }
    private WeddingGuest() { }
    public static WeddingGuest Create(Guid weddingId) => new() { WeddingProfileId = weddingId };
    public void Update(string name, string? phone, string group, string side, int partySize, string status, string? dietary, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160) throw new DomainException("Tên khách mời từ 1 đến 160 ký tự.");
        if (partySize < 1 || partySize > 20) throw new DomainException("Mỗi nhóm khách từ 1 đến 20 người.");
        if (side is not ("Shared" or "Bride" or "Groom")) throw new DomainException("Phía gia đình không hợp lệ.");
        if (phone?.Length > 30 || group?.Length > 100 || dietary?.Length > 500 || notes?.Length > 2000) throw new DomainException("Thông tin khách mời vượt giới hạn cho phép.");
        if (status is not ("Pending" or "Attending" or "NotAttending" or "Maybe")) throw new DomainException("Trạng thái khách không hợp lệ.");
        Name = name.Trim(); Phone = phone?.Trim(); Group = string.IsNullOrWhiteSpace(group) ? "Khác" : group.Trim(); Side = side; PartySize = partySize;
        Status = status; Dietary = dietary?.Trim(); Notes = notes?.Trim(); SetUpdated();
        if (status == "NotAttending") TableId = null;
    }
    public void AssignTable(Guid? tableId) { TableId = tableId; SetUpdated(); }
    public void LinkRsvp(Guid rsvpId) { RsvpId = rsvpId; SetUpdated(); }
    /// <summary>Move part of an unseated party into a separate seating row, preserving the total headcount and original RSVP link.</summary>
    public WeddingGuest SplitParty(int people)
    {
        if (TableId.HasValue || people < 1 || people >= PartySize) throw new DomainException("Chỉ có thể tách một phần nhóm khách chưa xếp bàn.");
        var split = Create(WeddingProfileId);
        split.Update(Name, Phone, Group, Side, people, Status, Dietary, Notes);
        PartySize -= people; SetUpdated();
        return split;
    }
}

public class SeatTable : BaseEntity
{
    public Guid WeddingProfileId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Capacity { get; private set; } = 10;
    private SeatTable() { }
    public static SeatTable Create(Guid weddingId, string name, int capacity)
    { var table = new SeatTable { WeddingProfileId = weddingId }; table.Update(name, capacity); return table; }
    public void Update(string name, int capacity)
    { if (string.IsNullOrWhiteSpace(name) || capacity < 1 || capacity > 100) throw new DomainException("Tên bàn bắt buộc; số ghế từ 1 đến 100."); Name = name.Trim(); Capacity = capacity; SetUpdated(); }
}
