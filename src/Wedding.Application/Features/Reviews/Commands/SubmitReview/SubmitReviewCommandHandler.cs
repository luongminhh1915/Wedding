using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Reviews.Commands.SubmitReview;

public class SubmitReviewCommandHandler : IRequestHandler<SubmitReviewCommand, ReviewDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    public SubmitReviewCommandHandler(IApplicationDbContext db, ICurrentUserService user) { _db = db; _user = user; }
    public Task<ReviewDto> Handle(SubmitReviewCommand r, CancellationToken ct) => _db.ExecuteSerializableAsync(token => SubmitAsync(r, token), ct);
    private async Task<ReviewDto> SubmitAsync(SubmitReviewCommand r, CancellationToken ct)
    {
        var uid = _user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var customer = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid && u.Role == UserRole.Customer, ct) ?? throw new DomainException("Chỉ khách hàng được gửi đánh giá.");
        var listing = await _db.Listings.Include(l => l.Vendor).FirstOrDefaultAsync(l => l.Id == r.ListingId, ct) ?? throw new NotFoundException("Không tìm thấy dịch vụ.");
        if (await _db.Reviews.AnyAsync(x => x.CustomerId == uid && x.ListingId == listing.Id, ct)) throw new DomainException("Mỗi khách chỉ có một đánh giá cho dịch vụ này.");
        var contracts = await _db.BookingContracts.Include(c => c.Lead).Where(c => c.CustomerId == uid && c.VendorId == listing.VendorId && c.Lead.ListingId == listing.Id).ToListAsync(ct);
        if (contracts.Any(c => c.Status == ContractStatus.Disputed)) throw new DomainException("Không được gửi đánh giá khi hợp đồng dịch vụ đang tranh chấp, kể cả đánh giá không gắn mã HĐ.");
        BookingContract? contract;
        if (r.BookingContractId.HasValue)
        {
            contract = contracts.FirstOrDefault(c => c.Id == r.BookingContractId);
            if (contract == null || !ReviewRules.IsEligible(contract, DateTime.UtcNow)) throw new DomainException("Hợp đồng phải đúng dịch vụ/NCC và đã hoàn tất hoặc qua ngày cưới 3 ngày, không có tranh chấp.");
        }
        else contract = contracts.Where(c => ReviewRules.IsEligible(c, DateTime.UtcNow)).OrderByDescending(c => c.CreatedAt).FirstOrDefault();
        if (contract != null && await _db.Reviews.AnyAsync(x => x.BookingContractId == contract.Id, ct)) throw new DomainException("Hợp đồng đã có đánh giá.");
        var review = Review.Create(contract?.Id, listing.Id, uid, listing.VendorId, r.Rating, r.Content, r.PhotosJson, contract != null, r.QualityRating, r.ServiceRating, r.PunctualityRating, r.ValueRating);
        _db.Reviews.Add(review); await _db.SaveChangesAsync(ct);
        return ReviewRules.Dto(review) with { CustomerName = customer.FullName, ListingTitle = listing.Title, VendorBrandName = listing.Vendor.BrandName, ContractCode = contract?.ContractCode };
    }
}
