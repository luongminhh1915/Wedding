using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Governance;
public record VendorTaxProfileDto(Guid VendorId, string LegalName, string TaxNumber, string BankCode, string BankAccountNumber, string BankAccountName, bool AgreementAccepted, string Status);
public record GetVendorTaxProfileQuery : IRequest<VendorTaxProfileDto?>;
public class GetVendorTaxProfileHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<GetVendorTaxProfileQuery, VendorTaxProfileDto?>
{
    public async Task<VendorTaxProfileDto?> Handle(GetVendorTaxProfileQuery r, CancellationToken ct)
    {
        var profile = await db.VendorTaxProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Vendor.UserId == user.UserId, ct);
        return profile == null ? null : Map(profile);
    }
    internal static VendorTaxProfileDto Map(VendorTaxProfile p) => new(p.VendorId, p.LegalName, p.TaxNumber, p.BankCode, p.BankAccountNumber, p.BankAccountName, p.AgreementAccepted, p.Status.ToString());
}
public record UpdateVendorTaxProfileCommand(string LegalName, string TaxNumber, string BankCode, string BankAccountNumber, string BankAccountName, bool AgreementAccepted) : IRequest<VendorTaxProfileDto>;
public class UpdateVendorTaxProfileHandler(IApplicationDbContext db, ICurrentUserService user, IAuditLogger audit) : IRequestHandler<UpdateVendorTaxProfileCommand, VendorTaxProfileDto>
{
    public async Task<VendorTaxProfileDto> Handle(UpdateVendorTaxProfileCommand r, CancellationToken ct)
    {
        var vendor = await db.Vendors.FirstOrDefaultAsync(v => v.UserId == user.UserId, ct) ?? throw new DomainException("Không có hồ sơ NCC.");
        var profile = await db.VendorTaxProfiles.FirstOrDefaultAsync(p => p.VendorId == vendor.Id, ct);
        if (profile == null) { profile = VendorTaxProfile.Create(vendor.Id); db.VendorTaxProfiles.Add(profile); }
        profile.Update(r.LegalName, r.TaxNumber, r.BankCode, r.BankAccountNumber, r.BankAccountName, r.AgreementAccepted);
        vendor.SetVerified(false);
        await audit.LogAsync("Vendor.TaxProfileUpdated", nameof(Vendor), vendor.Id.ToString());
        await db.SaveChangesAsync(ct); return GetVendorTaxProfileHandler.Map(profile);
    }
}
public record SuspendVendorCommand(Guid VendorId, string Reason) : IRequest;
public class SuspendVendorHandler(IApplicationDbContext db, IAuditLogger audit) : IRequestHandler<SuspendVendorCommand>
{
    public async Task Handle(SuspendVendorCommand r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Reason)) throw new DomainException("Cần ghi lý do tạm dừng.");
        var vendor = await db.Vendors.FindAsync(new object[] { r.VendorId }, ct) ?? throw new NotFoundException("Không tìm thấy NCC.");
        vendor.SetVerified(false);
        var profile = await db.VendorTaxProfiles.FirstOrDefaultAsync(p => p.VendorId == vendor.Id, ct); profile?.Suspend();
        var listings = await db.Listings.Where(l => l.VendorId == vendor.Id && l.Status == ListingStatus.Active).ToListAsync(ct);
        foreach (var listing in listings) listing.SetStatus(ListingStatus.Hidden);
        await audit.LogAsync("Vendor.Suspend", nameof(Vendor), vendor.Id.ToString(), new { r.Reason }); await db.SaveChangesAsync(ct);
    }
}
public record UpdateArticleCommand(Guid Id, string Title, string Slug, string Excerpt, string Content, string? ThumbnailUrl, string Category, bool Publish, List<Guid>? ListingIds) : IRequest<ArticleDto>;
public class UpdateArticleHandler(IApplicationDbContext db, IAuditLogger audit) : IRequestHandler<UpdateArticleCommand, ArticleDto>
{
    public async Task<ArticleDto> Handle(UpdateArticleCommand r, CancellationToken ct)
    {
        var article = await db.Articles.FindAsync(new object[] { r.Id }, ct) ?? throw new NotFoundException("Bài viết không tồn tại.");
        var slug = r.Slug.Trim().ToLowerInvariant();
        if (await db.Articles.AnyAsync(a => a.Id != r.Id && a.Slug == slug, ct)) throw new DomainException("Slug đã tồn tại.");
        var ids = r.ListingIds?.Distinct().Take(10).ToList() ?? new();
        if (await db.Listings.CountAsync(l => ids.Contains(l.Id) && l.Status == ListingStatus.Active, ct) != ids.Count) throw new DomainException("Chỉ được ghim dịch vụ đang hoạt động.");
        article.Update(r.Title, slug, r.Excerpt, r.Content, r.ThumbnailUrl, r.Category);
        if (r.Publish && !article.IsPublished) article.Publish(); else if (!r.Publish) article.Unpublish();
        db.ArticleListingPins.RemoveRange(await db.ArticleListingPins.Where(p => p.ArticleId == r.Id).ToListAsync(ct));
        foreach (var id in ids) db.ArticleListingPins.Add(ArticleListingPin.Create(article.Id, id));
        await audit.LogAsync("Article.Update", nameof(Article), r.Id.ToString(), new { r.Publish }); await db.SaveChangesAsync(ct);
        return new(article.Id, article.Title, article.Slug, article.Excerpt, article.Content, article.ThumbnailUrl, article.Category, article.IsPublished, article.PublishedAt, article.ViewCount);
    }
}
public record DeleteArticleCommand(Guid Id) : IRequest;
public class DeleteArticleHandler(IApplicationDbContext db, IAuditLogger audit) : IRequestHandler<DeleteArticleCommand>
{
    public async Task Handle(DeleteArticleCommand r, CancellationToken ct)
    {
        var article = await db.Articles.FindAsync(new object[] { r.Id }, ct) ?? throw new NotFoundException("Bài viết không tồn tại.");
        db.Articles.Remove(article); await audit.LogAsync("Article.Delete", nameof(Article), r.Id.ToString()); await db.SaveChangesAsync(ct);
    }
}
public record EmergencyMatchCommand(Guid ContractId, Guid ReplacementListingId, string Reason) : IRequest<Guid>;
public class EmergencyMatchHandler(IApplicationDbContext db, ICurrentUserService user, IAuditLogger audit, INotificationSender notify) : IRequestHandler<EmergencyMatchCommand, Guid>
{
    public Task<Guid> Handle(EmergencyMatchCommand r, CancellationToken ct) => db.ExecuteSerializableAsync(async token =>
    {
        if (r.Reason?.Trim().Length < 10) throw new DomainException("Cần ghi lý do và thỏa thuận xử lý tối thiểu 10 ký tự.");
        var contract = await db.BookingContracts.Include(c => c.Customer).FirstOrDefaultAsync(c => c.Id == r.ContractId, token) ?? throw new NotFoundException("Không có hợp đồng.");
        if (contract.Status != ContractStatus.Disputed) throw new DomainException("Emergency matching chỉ áp dụng khi hợp đồng đang tranh chấp. Hợp đồng cũ vẫn giữ để xử lý tài chính.");
        var listing = await db.Listings.Include(l => l.Vendor).FirstOrDefaultAsync(l => l.Id == r.ReplacementListingId, token) ?? throw new NotFoundException("Không tìm thấy dịch vụ thay thế.");
        if (!listing.Vendor.IsVerified || !listing.Vendor.IsActive || listing.Status != ListingStatus.Active || listing.VendorId == contract.VendorId) throw new DomainException("Chọn NCC khác, đã xác minh và có dịch vụ hoạt động.");
        if (await db.EmergencyMatches.AnyAsync(e => e.ContractId == r.ContractId && e.ReplacementListingId == r.ReplacementListingId, token)) throw new DomainException("Đã tạo yêu cầu hỗ trợ này.");
        var lead = Lead.Create(contract.CustomerId, listing.VendorId, listing.Id, contract.Customer.PhoneNumber, contract.WeddingDate, null, contract.ContractValue, r.Reason);
        lead.AttachSource("EmergencyMatch"); db.Leads.Add(lead); db.Vouchers.Add(Voucher.Create(lead.Id, contract.CustomerId));
        db.EmergencyMatches.Add(EmergencyMatch.Create(contract.Id, listing.Id, lead.Id, user.UserId!.Value, r.Reason!));
        await audit.LogAsync("Contract.EmergencyMatch", nameof(BookingContract), contract.Id.ToString(), new { lead.Id, r.ReplacementListingId, r.Reason });
        await db.SaveChangesAsync(token); await notify.SendAsync("Email", contract.Customer.Email, $"CSKH đã tạo yêu cầu NCC thay thế: {listing.Vendor.BrandName}. Bạn cần duyệt báo giá/hợp đồng mới; hợp đồng cũ không bị tự ý chuyển.", token);
        return lead.Id;
    }, ct);
}
