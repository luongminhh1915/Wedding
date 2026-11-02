using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Application.Features.Listings;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Reviews;

public static class ReviewTransactionEvidence
{
    public static async Task<(ComboOrder Order, Vendor Vendor, List<BookingContract> Contracts)> CompletedComboAsync(
        IApplicationDbContext db, Guid orderId, Guid customerId, CancellationToken ct)
    {
        var order = await db.ComboOrders.Include(candidate => candidate.Items)
            .FirstOrDefaultAsync(candidate => candidate.Id == orderId && candidate.CustomerId == customerId, ct)
            ?? throw new NotFoundException("Không tìm thấy giao dịch combo của bạn.");
        if (order.Items.Count == 0 || order.Items.Any(item => !item.LeadId.HasValue))
            throw new DomainException("Mọi thành phần cần thiết của combo phải có hợp đồng đã hoàn tất.");
        var leadIds = order.Items.Select(item => item.LeadId!.Value).ToList();
        var leads = await db.Leads.Where(lead => leadIds.Contains(lead.Id)).ToDictionaryAsync(lead => lead.Id, ct);
        if (leads.Count != order.Items.Count || order.Items.Any(item =>
            !leads.TryGetValue(item.LeadId!.Value, out var lead) || lead.ComboOrderId != order.Id ||
            lead.CustomerId != customerId || lead.VendorId != item.VendorId || lead.ListingId != item.ListingId))
            throw new DomainException("Không thể xác minh giao dịch với đủ hợp đồng thành phần.");
        var contracts = await db.BookingContracts.Where(contract => leadIds.Contains(contract.LeadId) &&
            contract.CustomerId == customerId && contract.Status == ContractStatus.Completed).ToListAsync(ct);
        if (contracts.Count != order.Items.Count || contracts.Any(contract =>
            !leads.TryGetValue(contract.LeadId, out var lead) || contract.VendorId != lead.VendorId))
            throw new DomainException("Mọi hợp đồng thành phần của combo phải thuộc bạn và ở trạng thái Hoàn tất.");
        var vendorIds = order.Items.Select(item => item.VendorId).Distinct().ToList();
        if (vendorIds.Count != 1) throw new DomainException("Không thể xác minh shop duy nhất của giao dịch combo.");
        var vendor = await db.Vendors.Include(candidate => candidate.User)
            .FirstOrDefaultAsync(candidate => candidate.Id == vendorIds[0], ct)
            ?? throw new NotFoundException("Shop của giao dịch không tồn tại.");
        return (order, vendor, contracts);
    }

    public static bool HasMatchingTemplateSnapshot(ComboOrder order, ComboTemplateRevision? revision)
    {
        if (revision is null || !order.ComboTemplateId.HasValue || !order.ComboTemplateRevisionId.HasValue ||
            string.IsNullOrWhiteSpace(order.ComboTemplateSnapshotJson)) return false;
        try
        {
            using var json = JsonDocument.Parse(order.ComboTemplateSnapshotJson);
            var root = json.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("templateId", out var templateId) &&
                templateId.ValueKind == JsonValueKind.String &&
                Guid.TryParse(templateId.GetString(), out var id) && id == order.ComboTemplateId.Value &&
                root.TryGetProperty("schemaVersion", out var version) &&
                version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var schemaVersion) && schemaVersion == 1 &&
                root.TryGetProperty("snapshotKind", out var kind) &&
                kind.ValueKind == JsonValueKind.String && kind.GetString() == "ApprovedRevision" &&
                root.TryGetProperty("revisionId", out var revisionId) &&
                revisionId.ValueKind == JsonValueKind.String &&
                Guid.TryParse(revisionId.GetString(), out var parsedRevisionId) &&
                parsedRevisionId == order.ComboTemplateRevisionId.Value &&
                revision.Id == parsedRevisionId &&
                    revision.ComboTemplateId == order.ComboTemplateId.Value &&
                    root.TryGetProperty("revisionSequence", out var sequence) &&
                    sequence.ValueKind == JsonValueKind.Number &&
                    sequence.TryGetInt32(out var revisionSequence) && revisionSequence == revision.Sequence &&
                    root.TryGetProperty("revisionSnapshotJson", out var revisionSnapshot) &&
                    revisionSnapshot.ValueKind == JsonValueKind.String &&
                    revisionSnapshot.GetString() == revision.SnapshotJson;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException) { return false; }
    }
}

public class SubmitShopReviewCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    : IRequestHandler<SubmitShopReviewCommand, ReviewDto>
{
    public Task<ReviewDto> Handle(SubmitShopReviewCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(token => SubmitAsync(request, token), ct);

    private async Task<ReviewDto> SubmitAsync(SubmitShopReviewCommand request, CancellationToken ct)
    {
        if (request.BookingContractId.HasValue == request.ComboOrderId.HasValue)
            throw new DomainException("Chọn đúng một giao dịch đã hoàn tất.");
        var customer = await ReviewRules.RequireActiveCustomerAsync(db, user, ct);
        Guid vendorId;
        string vendorName;
        if (request.BookingContractId is { } contractId)
        {
            var contract = await db.BookingContracts.Include(item => item.Lead)
                .FirstOrDefaultAsync(item => item.Id == contractId && item.CustomerId == customer.Id, ct)
                ?? throw new DomainException("Giao dịch không thuộc tài khoản của bạn.");
            if (contract.Status != ContractStatus.Completed || contract.Lead.ComboOrderId.HasValue)
                throw new DomainException("Đánh giá Shop từ giao dịch riêng chỉ hợp lệ khi hợp đồng đã hoàn tất.");
            if (await db.BookingContracts.AnyAsync(item => item.CustomerId == customer.Id &&
                item.VendorId == contract.VendorId && item.Lead.ListingId == contract.Lead.ListingId &&
                item.Status == ContractStatus.Disputed, ct))
                throw new DomainException("Không thể đánh giá Shop khi giao dịch dịch vụ đang tranh chấp.");
            vendorId = contract.VendorId;
            vendorName = await db.Vendors.Where(vendor => vendor.Id == vendorId)
                .Select(vendor => vendor.BrandName).SingleAsync(ct);
        }
        else
        {
            var evidence = await ReviewTransactionEvidence.CompletedComboAsync(db, request.ComboOrderId!.Value, customer.Id, ct);
            vendorId = evidence.Vendor.Id;
            vendorName = evidence.Vendor.BrandName;
        }

        var duplicate = request.ComboOrderId.HasValue
            ? await db.Reviews.AnyAsync(review => review.TargetType == ReviewTargetType.Shop &&
                review.ComboOrderId == request.ComboOrderId && review.VendorId == vendorId, ct)
            : await db.Reviews.AnyAsync(review => review.TargetType == ReviewTargetType.Shop &&
                review.BookingContractId == request.BookingContractId && review.VendorId == vendorId, ct);
        if (duplicate) throw new DomainException("Giao dịch này đã có đánh giá Shop.");
        var review = Review.CreateShop(request.BookingContractId, request.ComboOrderId, customer.Id, vendorId,
            request.Rating, request.Content, request.PhotosJson);
        db.Reviews.Add(review);
        await db.SaveChangesAsync(ct);
        return ReviewRules.Dto(review) with { VendorBrandName = vendorName };
    }
}

public class SubmitComboReviewCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    : IRequestHandler<SubmitComboReviewCommand, ReviewDto>
{
    public Task<ReviewDto> Handle(SubmitComboReviewCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(token => SubmitAsync(request, token), ct);

    private async Task<ReviewDto> SubmitAsync(SubmitComboReviewCommand request, CancellationToken ct)
    {
        var customer = await ReviewRules.RequireActiveCustomerAsync(db, user, ct);
        var evidence = await ReviewTransactionEvidence.CompletedComboAsync(db, request.ComboOrderId, customer.Id, ct);
        if (!evidence.Order.ComboTemplateId.HasValue || !evidence.Order.ComboTemplateRevisionId.HasValue)
            throw new DomainException("Không thể đánh giá Combo này vì chưa có snapshot/version công khai đáng tin cậy tại thời điểm giao dịch.");
        var revision = await db.ComboTemplateRevisions.FirstOrDefaultAsync(candidate =>
            candidate.Id == evidence.Order.ComboTemplateRevisionId &&
            candidate.ComboTemplateId == evidence.Order.ComboTemplateId, ct)
            ?? throw new DomainException("Không thể xác minh phiên bản Combo đã chốt trong giao dịch.");
        if (!ReviewTransactionEvidence.HasMatchingTemplateSnapshot(evidence.Order, revision))
            throw new DomainException("Không thể đánh giá Combo này vì snapshot không khớp phiên bản đã chốt.");
        var template = await db.ComboTemplates.FirstOrDefaultAsync(candidate =>
            candidate.Id == evidence.Order.ComboTemplateId.Value, ct)
            ?? throw new DomainException("Combo mẫu liên kết với giao dịch không còn tồn tại.");
        if (await db.Reviews.AnyAsync(review => review.TargetType == ReviewTargetType.Combo &&
            review.ComboOrderId == evidence.Order.Id && review.ComboTemplateId == template.Id, ct))
            throw new DomainException("Giao dịch này đã có đánh giá cho Combo.");
        var review = Review.CreateCombo(evidence.Order.Id, template.Id, customer.Id, evidence.Vendor.Id,
            request.Rating, request.Content, request.PhotosJson);
        db.Reviews.Add(review);
        await db.SaveChangesAsync(ct);
        return ReviewRules.Dto(review) with { VendorBrandName = evidence.Vendor.BrandName, ComboTemplateName = template.Name };
    }
}

public record GetEligibleReviewTargetsQuery : IRequest<List<EligibleReviewTargetDto>>;

public class GetEligibleReviewTargetsQueryHandler(IApplicationDbContext db, ICurrentUserService user)
    : IRequestHandler<GetEligibleReviewTargetsQuery, List<EligibleReviewTargetDto>>
{
    public async Task<List<EligibleReviewTargetDto>> Handle(GetEligibleReviewTargetsQuery request, CancellationToken ct)
    {
        var customer = await ReviewRules.RequireActiveCustomerAsync(db, user, ct);
        var result = new List<EligibleReviewTargetDto>();
        var contracts = await db.BookingContracts.Include(contract => contract.Lead).ThenInclude(lead => lead.Listing)
            .Where(contract => contract.CustomerId == customer.Id && contract.Status == ContractStatus.Completed)
            .OrderByDescending(contract => contract.CreatedAt).ToListAsync(ct);
        foreach (var contract in contracts)
        {
            var listing = contract.Lead.Listing;
            var listingVisible = listing != null &&
                await db.Listings.PubliclyVisible().AnyAsync(candidate => candidate.Id == listing.Id, ct);
            if (listingVisible && !await db.Reviews.AnyAsync(review => review.TargetType == ReviewTargetType.Service &&
                review.BookingContractId == contract.Id && review.ListingId == listing!.Id, ct))
                result.Add(new("Service", contract.Id, contract.ContractCode, listing!.Id, listing.Title,
                    contract.Id, contract.Lead.ComboOrderId, listing.Id));
            if (!contract.Lead.ComboOrderId.HasValue &&
                !await db.Reviews.AnyAsync(review => review.TargetType == ReviewTargetType.Shop &&
                    review.BookingContractId == contract.Id && review.VendorId == contract.VendorId, ct))
            {
                var vendorName = await db.Vendors.Where(vendor => vendor.Id == contract.VendorId)
                    .Select(vendor => vendor.BrandName).SingleAsync(ct);
                result.Add(new("Shop", contract.Id, contract.ContractCode, contract.VendorId, vendorName,
                    contract.Id, null, null));
            }
        }

        var comboOrders = await db.ComboOrders.Where(order => order.CustomerId == customer.Id)
            .OrderByDescending(order => order.CreatedAt).ToListAsync(ct);
        foreach (var order in comboOrders)
        {
            try
            {
                var evidence = await ReviewTransactionEvidence.CompletedComboAsync(db, order.Id, customer.Id, ct);
                if (!await db.Reviews.AnyAsync(review => review.TargetType == ReviewTargetType.Shop &&
                    review.ComboOrderId == order.Id && review.VendorId == evidence.Vendor.Id, ct))
                    result.Add(new("Shop", order.Id, order.Code, evidence.Vendor.Id, evidence.Vendor.BrandName, null, order.Id, null));
                var revision = order.ComboTemplateRevisionId.HasValue
                    ? await db.ComboTemplateRevisions.AsNoTracking().FirstOrDefaultAsync(candidate =>
                        candidate.Id == order.ComboTemplateRevisionId &&
                        candidate.ComboTemplateId == order.ComboTemplateId, ct)
                    : null;
                if (revision == null || !ReviewTransactionEvidence.HasMatchingTemplateSnapshot(order, revision) ||
                    await db.Reviews.AnyAsync(review => review.TargetType == ReviewTargetType.Combo &&
                        review.ComboOrderId == order.Id && review.ComboTemplateId == order.ComboTemplateId, ct))
                    continue;
                var template = await db.ComboTemplates.AsNoTracking()
                    .FirstOrDefaultAsync(candidate => candidate.Id == order.ComboTemplateId, ct);
                if (template != null)
                    result.Add(new("Combo", order.Id, order.Code, template.Id, template.Name, null, order.Id, null));
            }
            catch (DomainException) { }
        }
        return result;
    }
}

public class SubmitReviewEditRequestCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    : IRequestHandler<SubmitReviewEditRequestCommand, ReviewDto>
{
    public Task<ReviewDto> Handle(SubmitReviewEditRequestCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(token => SubmitAsync(request, token), ct);

    private async Task<ReviewDto> SubmitAsync(SubmitReviewEditRequestCommand request, CancellationToken ct)
    {
        var customer = await ReviewRules.RequireActiveCustomerAsync(db, user, ct);
        var review = await db.Reviews.Include(item => item.EditRequests).Include(item => item.ComboTemplate)
            .FirstOrDefaultAsync(item => item.Id == request.ReviewId, ct)
            ?? throw new NotFoundException("Đánh giá không tồn tại.");
        if (review.CustomerId != customer.Id) throw new DomainException("Chỉ chủ sở hữu được chỉnh sửa đánh giá.");
        if (review.Status != ReviewStatus.Approved || DateTime.UtcNow > review.CreatedAt.AddDays(7))
            throw new DomainException("Chỉ đánh giá đã duyệt trong vòng 7 ngày từ lần đăng đầu tiên được chỉnh sửa.");
        if (review.EditRequests.Any(edit => edit.Status == ReviewEditStatus.Pending))
            throw new DomainException("Đánh giá đã có yêu cầu chỉnh sửa đang chờ duyệt.");
        if (review.EditRequests.Any(edit => edit.Status == ReviewEditStatus.Approved))
            throw new DomainException("Đánh giá đã sử dụng hết một lần chỉnh sửa được duyệt.");
        if (review.EditRequests.Count >= 3)
            throw new DomainException("Đã đạt tối đa ba lần gửi yêu cầu chỉnh sửa.");
        var attemptNumber = review.EditRequests.Count + 1;
        // The existing HTTP body cannot distinguish omitted optional fields from null.
        // Treat both as unchanged; a supplied "[]" explicitly clears photo references.
        // Validate the merged criteria through the existing domain rules, without
        // changing historical scores or introducing a new criterion-clear contract.
        var edit = ReviewEditRequest.Create(review, attemptNumber, customer.Id,
            request.Rating, request.Content, request.PhotosJson ?? review.PhotosJson,
            request.QualityRating ?? review.QualityRating,
            request.ServiceRating ?? review.ServiceRating,
            request.PunctualityRating ?? review.PunctualityRating,
            request.ValueRating ?? review.ValueRating);
        db.ReviewEditRequests.Add(edit);
        await db.SaveChangesAsync(ct);
        return ReviewRules.Dto(review, attemptNumber, edit.Status.ToString());
    }
}

public class ModerateReviewEditRequestCommandHandler(IApplicationDbContext db, ICurrentUserService user)
    : IRequestHandler<ModerateReviewEditRequestCommand, ReviewDto>
{
    public Task<ReviewDto> Handle(ModerateReviewEditRequestCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(token => ModerateAsync(request, token), ct);

    private async Task<ReviewDto> ModerateAsync(ModerateReviewEditRequestCommand request, CancellationToken ct)
    {
        if (!user.UserId.HasValue || user.UserId == Guid.Empty ||
            user.Role is not (nameof(UserRole.Moderator) or nameof(UserRole.SuperAdmin)))
            throw new DomainException("Chỉ Moderator hoặc SuperAdmin được kiểm duyệt phiên bản đánh giá.");
        var moderator = await db.Users.AsNoTracking().FirstOrDefaultAsync(actor =>
            actor.Id == user.UserId && actor.IsActive &&
            (actor.Role == UserRole.Moderator || actor.Role == UserRole.SuperAdmin), ct)
            ?? throw new DomainException("Tài khoản moderator không hoạt động hoặc quyền đã thay đổi.");
        var edit = await db.ReviewEditRequests.Include(item => item.Review).ThenInclude(review => review.ComboTemplate)
            .Include(item => item.Review).ThenInclude(review => review.Customer)
            .Include(item => item.Review).ThenInclude(review => review.Vendor)
            .FirstOrDefaultAsync(item => item.Id == request.EditRequestId, ct)
            ?? throw new NotFoundException("Yêu cầu chỉnh sửa không tồn tại.");
        if (edit.Status != ReviewEditStatus.Pending || edit.Review.Status != ReviewStatus.Approved)
            throw new DomainException("Yêu cầu không còn chờ duyệt hoặc bản công khai không còn hợp lệ.");
        if (request.Approve)
        {
            if (await ReviewRules.HasDisputedEvidenceAsync(db, edit.Review, ct))
                throw new DomainException("Tạm dừng duyệt bản sửa đánh giá trong thời gian tranh chấp.");
            if (await db.ReviewEditRequests.AnyAsync(item => item.ReviewId == edit.ReviewId &&
                item.Status == ReviewEditStatus.Approved, ct))
                throw new DomainException("Đánh giá đã được chỉnh sửa một lần.");
            edit.Approve(moderator.Id);
            edit.Review.ApplyApprovedEdit(edit);
        }
        else
        {
            edit.Reject(moderator.Id, request.Reason ?? "");
        }
        await db.SaveChangesAsync(ct);
        if (request.Approve && edit.Review.TargetType == ReviewTargetType.Shop)
            await ReviewRules.RefreshVendorAsync(db, edit.Review.VendorId, ct);
        await db.SaveChangesAsync(ct);
        var attemptCount = await db.ReviewEditRequests.CountAsync(item => item.ReviewId == edit.ReviewId, ct);
        return ReviewRules.Dto(edit.Review, attemptCount, edit.Status.ToString());
    }
}

public class GetIndependentReviewsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetIndependentReviewsQuery, ReviewPageDto>
{
    public async Task<ReviewPageDto> Handle(GetIndependentReviewsQuery request, CancellationToken ct)
    {
        if (!Enum.TryParse<ReviewTargetType>(request.TargetType, true, out var targetType) ||
            !Enum.IsDefined(targetType) || request.TargetId == Guid.Empty ||
            request.Page < 1 || request.PageSize is < 1 or > 100 ||
            request.Rating is < 1 or > 5 ||
            request.Sort is not ("newest" or "oldest" or "rating-high" or "rating-low"))
            throw new DomainException("Bộ lọc đánh giá không hợp lệ.");
        var visibleListings = db.Listings.PubliclyVisible().Select(listing => (Guid?)listing.Id);
        IQueryable<Review> query = db.Reviews.Where(review => review.TargetType == targetType &&
            review.Status == ReviewStatus.Approved);
        switch (targetType)
        {
            case ReviewTargetType.Service:
                if (!await db.Listings.PubliclyVisible().AnyAsync(listing => listing.Id == request.TargetId, ct))
                    throw new NotFoundException("Dịch vụ không tồn tại hoặc chưa công khai.");
                query = query.Where(review => review.ListingId == request.TargetId && visibleListings.Contains(review.ListingId));
                break;
            case ReviewTargetType.Shop:
                if (!await db.Vendors.AnyAsync(vendor => vendor.Id == request.TargetId &&
                    vendor.IsActive && vendor.User.IsActive, ct))
                    throw new NotFoundException("Shop không tồn tại hoặc chưa công khai.");
                query = query.Where(review => review.VendorId == request.TargetId);
                break;
            case ReviewTargetType.Combo:
                var template = await db.ComboTemplates.AsNoTracking().Include(item => item.Items)
                    .PubliclyAvailable().FirstOrDefaultAsync(item => item.Id == request.TargetId, ct);
                if (template == null || !await ComboTemplateAvailability.ComponentsEligibleAsync(db, template, ct))
                    throw new NotFoundException("Combo không tồn tại hoặc chưa công khai.");
                query = query.Where(review => review.ComboTemplateId == request.TargetId);
                break;
        }

        var aggregate = await query.GroupBy(_ => 1).Select(group => new
        {
            TotalReviews = group.Count(),
            Weight = group.Sum(review => review.IsVerifiedBuyer ? 1m : .5m),
            WeightedRating = group.Sum(review =>
                (review.QualityRating.HasValue
                    ? (review.QualityRating.Value + review.ServiceRating!.Value +
                       review.PunctualityRating!.Value + review.ValueRating!.Value) / 4m
                    : review.Rating) * (review.IsVerifiedBuyer ? 1m : .5m))
        }).SingleOrDefaultAsync(ct);
        var distributionRows = await query.GroupBy(review => review.Rating)
            .Select(group => new { Rating = group.Key, Count = group.Count() }).ToListAsync(ct);
        var distribution = Enumerable.Range(1, 5).ToDictionary(star => star,
            star => distributionRows.Where(item => item.Rating == star).Select(item => item.Count).FirstOrDefault());
        var average = aggregate == null || aggregate.Weight == 0 ? 0m :
            Math.Round(aggregate.WeightedRating / aggregate.Weight, 1, MidpointRounding.AwayFromZero);
        var summary = new ReviewRatingSummaryDto(average, aggregate?.TotalReviews ?? 0, distribution);
        var filtered = query;
        if (request.Rating.HasValue) filtered = filtered.Where(review => review.Rating == request.Rating.Value);
        if (request.VerifiedOnly.HasValue) filtered = filtered.Where(review => review.IsVerifiedBuyer == request.VerifiedOnly.Value);
        filtered = request.Sort switch
        {
            "oldest" => filtered.OrderBy(review => review.CreatedAt),
            "rating-high" => filtered.OrderByDescending(review => review.Rating).ThenByDescending(review => review.CreatedAt),
            "rating-low" => filtered.OrderBy(review => review.Rating).ThenByDescending(review => review.CreatedAt),
            _ => filtered.OrderByDescending(review => review.CreatedAt)
        };
        var count = await filtered.CountAsync(ct);
        var reviews = await filtered.Include(review => review.Customer).Include(review => review.Vendor)
            .Include(review => review.Listing).Include(review => review.ComboTemplate)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).AsNoTracking().ToListAsync(ct);
        var items = reviews.Select(review => new PublicReviewDto(review.Id, review.ListingId,
            review.Listing?.Title ?? review.ComboTemplate?.Name, review.Customer.FullName, review.VendorId,
            review.Vendor.BrandName, review.Rating, review.Content, review.PhotosJson, review.IsVerifiedBuyer,
            review.Status.ToString(), review.VendorReply, review.VendorRepliedAt, review.CreatedAt,
            review.QualityRating, review.ServiceRating, review.PunctualityRating, review.ValueRating,
            review.AverageRating, review.TargetType.ToString(), review.ComboTemplateId)).ToList();
        return new(summary, items, request.Page, request.PageSize, count,
            (int)Math.Ceiling(count / (double)request.PageSize));
    }
}

public record GetPendingReviewEditsQuery : IRequest<List<PendingReviewEditDto>>;
public record GetMyReviewsQuery : IRequest<List<ReviewDto>>;

public class GetMyReviewsQueryHandler(IApplicationDbContext db, ICurrentUserService user)
    : IRequestHandler<GetMyReviewsQuery, List<ReviewDto>>
{
    public async Task<List<ReviewDto>> Handle(GetMyReviewsQuery request, CancellationToken ct)
    {
        var customer = await ReviewRules.RequireActiveCustomerAsync(db, user, ct);
        var reviews = await db.Reviews.Where(review => review.CustomerId == customer.Id)
            .Include(review => review.Customer).Include(review => review.Vendor)
            .Include(review => review.Listing).Include(review => review.ComboTemplate)
            .Include(review => review.BookingContract).Include(review => review.EditRequests)
            .OrderByDescending(review => review.CreatedAt).AsNoTracking().ToListAsync(ct);
        return reviews.Select(review => ReviewRules.Dto(review, review.EditRequests.Count,
            review.EditRequests.OrderByDescending(edit => edit.AttemptNumber).Select(edit => edit.Status.ToString()).FirstOrDefault(),
            review.EditRequests.Any(edit => edit.Status == ReviewEditStatus.Approved),
            review.EditRequests.Any(edit => edit.Status == ReviewEditStatus.Pending))).ToList();
    }
}

public class GetPendingReviewEditsQueryHandler(IApplicationDbContext db, ICurrentUserService user)
    : IRequestHandler<GetPendingReviewEditsQuery, List<PendingReviewEditDto>>
{
    public async Task<List<PendingReviewEditDto>> Handle(GetPendingReviewEditsQuery request, CancellationToken ct)
    {
        if (!user.UserId.HasValue || user.UserId == Guid.Empty ||
            user.Role is not (nameof(UserRole.Moderator) or nameof(UserRole.SuperAdmin)) ||
            !await db.Users.AnyAsync(actor => actor.Id == user.UserId && actor.IsActive &&
                (actor.Role == UserRole.Moderator || actor.Role == UserRole.SuperAdmin), ct))
            throw new DomainException("Chỉ Moderator hoặc SuperAdmin được xem phiên bản đánh giá chờ duyệt.");
        var pending = await db.ReviewEditRequests.Where(edit => edit.Status == ReviewEditStatus.Pending)
            .Include(edit => edit.Review).ThenInclude(review => review.Customer)
            .Include(edit => edit.Review).ThenInclude(review => review.Vendor)
            .Include(edit => edit.Review).ThenInclude(review => review.Listing)
            .Include(edit => edit.Review).ThenInclude(review => review.ComboTemplate)
            .Include(edit => edit.Review).ThenInclude(review => review.BookingContract)
            .OrderBy(edit => edit.RequestedAt).AsNoTracking().ToListAsync(ct);
        return pending.Select(edit => new PendingReviewEditDto(edit.Id, edit.ReviewId, edit.AttemptNumber,
            edit.BeforeContent, edit.BeforeRating, edit.AfterContent, edit.AfterRating, edit.RequestedAt,
            ReviewRules.Dto(edit.Review, edit.AttemptNumber, edit.Status.ToString()),
            edit.BeforePhotosJson, edit.AfterPhotosJson,
            edit.BeforeQualityRating, edit.AfterQualityRating,
            edit.BeforeServiceRating, edit.AfterServiceRating,
            edit.BeforePunctualityRating, edit.AfterPunctualityRating,
            edit.BeforeValueRating, edit.AfterValueRating)).ToList();
    }
}
