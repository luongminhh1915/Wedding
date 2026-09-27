using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Listings.Commands.ApproveListing;
using Wedding.Application.Features.Listings.Commands.CreateListing;
using Wedding.Application.Features.Listings.Commands.RejectListing;
using Wedding.Application.Features.Listings.Commands.SubmitForApproval;
using Wedding.Application.Features.Listings.Commands.UpdateListing;
using Wedding.Application.Features.Listings.Queries.GetListingDetail;
using Wedding.Application.Features.Listings.Queries.GetPendingListings;
using Wedding.Application.Features.Listings.Queries.GetPublicListings;
using Wedding.Application.Features.Listings.Queries.GetVendorListings;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ListingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ListingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ─── VENDOR ENDPOINTS ──────────────────────────────────────────────────────

    /// <summary>
    /// [VendorOwner] Tạo bài đăng dịch vụ mới (trạng thái Draft).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> CreateListing([FromBody] CreateListingCommand command)
    {
        var listingId = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetListingById), new { id = listingId },
            new { listingId, message = "Tạo bài đăng thành công. Hãy nộp duyệt khi đã sẵn sàng." });
    }

    /// <summary>
    /// [VendorOwner] Cập nhật nội dung bài đăng (chỉ cho phép khi ở Draft hoặc Rejected).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> UpdateListing(Guid id, [FromBody] UpdateListingRequest request)
    {
        var command = new UpdateListingCommand(id, request.Title, request.MinPrice, request.MaxPrice, request.Description, request.Location);
        await _mediator.Send(command);
        return Ok(new { message = "Cập nhật bài đăng thành công." });
    }

    /// <summary>
    /// [VendorOwner] Nộp bài đăng lên hàng chờ kiểm duyệt (PendingApproval).
    /// BR-008: Moderator phải xử lý trong SLA 24h.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> SubmitForApproval(Guid id)
    {
        await _mediator.Send(new SubmitForApprovalCommand(id));
        return Ok(new { message = "Bài đăng đã được nộp duyệt. Moderator sẽ xem xét trong vòng 24h (BR-008)." });
    }

    /// <summary>
    /// [VendorOwner] Lấy danh sách tất cả bài đăng của NCC đang đăng nhập.
    /// </summary>
    [HttpGet("my-listings")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> GetMyListings()
    {
        var result = await _mediator.Send(new GetVendorListingsQuery());
        return Ok(result);
    }

    // ─── MODERATOR ENDPOINTS ───────────────────────────────────────────────────

    /// <summary>
    /// [Moderator] Lấy danh sách bài đăng đang chờ kiểm duyệt (BR-008 SLA 24h).
    /// </summary>
    [HttpGet("pending")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> GetPendingListings()
    {
        var result = await _mediator.Send(new GetPendingListingsQuery());
        return Ok(result);
    }

    /// <summary>
    /// [Moderator] Duyệt bài đăng — chuyển sang Active.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> ApproveListing(Guid id)
    {
        await _mediator.Send(new ApproveListingCommand(id));
        return Ok(new { message = "Bài đăng đã được duyệt và hiển thị công khai." });
    }

    /// <summary>
    /// [Moderator] Từ chối bài đăng kèm lý do (BR-008).
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> RejectListing(Guid id, [FromBody] RejectListingRequest request)
    {
        await _mediator.Send(new RejectListingCommand(id, request.Reason));
        return Ok(new { message = "Bài đăng đã bị từ chối. Lý do đã được thông báo đến NCC." });
    }

    // ─── PUBLIC ENDPOINTS (không cần auth) ────────────────────────────────────

    /// <summary>
    /// [Public] Khám phá danh sách gói dịch vụ Active. Hỗ trợ lọc theo ngành, giá, địa điểm, từ khóa.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicListings(
        [FromQuery] Guid? categoryId,
        [FromQuery] string? location,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? keyword,
        [FromQuery] string sortBy = "newest",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12)
    {
        var result = await _mediator.Send(new GetPublicListingsQuery(
            categoryId, location, minPrice, maxPrice, keyword, sortBy, page, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// [Public] Lấy chi tiết một bài đăng theo ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetListingById(Guid id)
    {
        var result = await _mediator.Send(new GetListingDetailQuery(id));
        return Ok(result);
    }
}

// ─── REQUEST MODELS (chỉ dùng cho body binding của Controller) ─────────────

public record UpdateListingRequest(
    string Title,
    decimal MinPrice,
    decimal MaxPrice,
    string Description,
    string Location
);

public record RejectListingRequest(string Reason);
