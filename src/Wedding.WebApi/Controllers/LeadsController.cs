using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Leads.Commands.AcceptLead;
using Wedding.Application.Features.Leads.Commands.SendLead;
using Wedding.Application.Features.Leads.Commands.UnlockPhone;
using Wedding.Application.Features.Leads.Queries.GetCustomerLeads;
using Wedding.Application.Features.Leads.Queries.GetLeadDetail;
using Wedding.Application.Features.Leads.Queries.GetVendorLeads;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeadsController : ControllerBase
{
    private readonly IMediator _mediator;

    public LeadsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// [Customer] Gửi yêu cầu tư vấn dịch vụ cưới.
    /// Tự động sinh mã Voucher độc nhất 8 ký tự hạn 30 ngày (BR-004)
    /// và che số điện thoại đối với nhà cung cấp (BR-002).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SendLead([FromBody] SendLeadCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetLeadById), new { id = result.LeadId }, result);
    }

    /// <summary>
    /// [VendorOwner] Hộp thư tiếp nhận Lead mới gửi tới Nhà cung cấp.
    /// BR-002: Số điện thoại bị che dạng 0987***123 trừ khi khách mở khóa.
    /// BR-003: Hiển thị hạn chót SLA 2 giờ.
    /// </summary>
    [HttpGet("vendor-inbox")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> GetVendorLeads([FromQuery] string? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetVendorLeadsQuery(status, pageNumber, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// [Customer] Xem danh sách các yêu cầu tư vấn đã gửi.
    /// Khách hàng xem được đầy đủ số điện thoại của mình.
    /// </summary>
    [HttpGet("my-leads")]
    public async Task<IActionResult> GetMyLeads([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetCustomerLeadsQuery(pageNumber, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// Xem chi tiết một yêu cầu tư vấn (Lead).
    /// Vendor chỉ thấy số đã mask nếu khách chưa mở khóa (BR-002).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetLeadById(Guid id)
    {
        var result = await _mediator.Send(new GetLeadDetailQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// [VendorOwner] Tiếp nhận Lead trong vòng SLA 2 giờ (BR-003).
    /// </summary>
    [HttpPost("{id:guid}/accept")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> AcceptLead(Guid id)
    {
        await _mediator.Send(new AcceptLeadCommand(id));
        return Ok(new { message = "Tiếp nhận yêu cầu tư vấn thành công. Hãy mở chat để trao đổi với khách hàng." });
    }

    /// <summary>
    /// [Customer] Cho phép mở khóa số điện thoại cho Nhà cung cấp xem đầy đủ (BR-002 Smart Privacy).
    /// </summary>
    [HttpPost("{id:guid}/unlock-phone")]
    public async Task<IActionResult> UnlockPhone(Guid id)
    {
        await _mediator.Send(new UnlockPhoneCommand(id));
        return Ok(new { message = "Đã mở khóa số điện thoại cho Nhà cung cấp liên hệ trực tiếp." });
    }
}
