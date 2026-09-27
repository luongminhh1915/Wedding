using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Application.Features.Reviews.Queries.GetEligibleContracts;
using Wedding.Application.Features.Reviews.Queries.GetListingReviews;
using Wedding.Application.Features.Reviews.Queries.GetPendingReviews;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReviewsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// [Customer] Gửi đánh giá dịch vụ cưới.
    /// Nếu có BookingContractId ở trạng thái Completed sẽ tự động được gắn nhãn Verified Buyer (BR-009).
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SubmitReview([FromBody] SubmitReviewCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// [Customer] Lấy danh sách hợp đồng đã hoàn tất của tôi để chọn viết đánh giá Verified.
    /// </summary>
    [HttpGet("eligible-contracts")]
    [Authorize]
    public async Task<IActionResult> GetEligibleContracts()
    {
        var result = await _mediator.Send(new GetEligibleContractsQuery());
        return Ok(result);
    }

    /// <summary>
    /// [Public API] Lấy danh sách đánh giá đã được duyệt của một bài đăng dịch vụ (Listing).
    /// </summary>
    [HttpGet("listing/{listingId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetListingReviews(Guid listingId)
    {
        var result = await _mediator.Send(new GetListingReviewsQuery(ListingId: listingId));
        return Ok(result);
    }

    /// <summary>
    /// [Public API] Lấy danh sách đánh giá đã được duyệt của một Nhà cung cấp (Vendor).
    /// </summary>
    [HttpGet("vendor/{vendorId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVendorReviews(Guid vendorId)
    {
        var result = await _mediator.Send(new GetListingReviewsQuery(VendorId: vendorId));
        return Ok(result);
    }

    /// <summary>
    /// [Moderator/Admin] Lấy hàng đợi các bài đánh giá đang chờ kiểm duyệt (Pending).
    /// </summary>
    [HttpGet("moderation/pending")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> GetPendingReviews()
    {
        var result = await _mediator.Send(new GetPendingReviewsQuery());
        return Ok(result);
    }

    /// <summary>
    /// [Moderator/Admin] Phê duyệt bài đánh giá và tự động tính lại điểm uy tín RatingAvg của NCC theo FM-005.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> ApproveReview(Guid id)
    {
        var result = await _mediator.Send(new ApproveReviewCommand(id));
        return Ok(new { message = "Đã phê duyệt đánh giá và cập nhật lại điểm uy tín NCC thành công.", review = result });
    }

    /// <summary>
    /// [Moderator/Admin] Từ chối bài đánh giá vi phạm chính sách hoặc spam.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> RejectReview(Guid id, [FromBody] RejectReviewRequest? request)
    {
        var result = await _mediator.Send(new RejectReviewCommand(id, request?.Reason));
        return Ok(new { message = "Đã từ chối bài đánh giá.", review = result });
    }

    /// <summary>
    /// [VendorOwner] Nhà cung cấp phản hồi lại bình luận đánh giá của khách hàng.
    /// </summary>
    [HttpPost("{id:guid}/reply")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> ReplyReview(Guid id, [FromBody] ReplyReviewRequest request)
    {
        var result = await _mediator.Send(new VendorReplyReviewCommand(id, request.Reply));
        return Ok(new { message = "Phản hồi đánh giá thành công.", review = result });
    }
}

public record RejectReviewRequest(string? Reason);
public record ReplyReviewRequest(string Reply);
