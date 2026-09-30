using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Contracts.Commands.CompleteContract;
using Wedding.Application.Features.Contracts.Commands.ConfirmContract;
using Wedding.Application.Features.Contracts.Commands.CreateContractDraft;
using Wedding.Application.Features.Contracts.Commands.RejectContract;
using Wedding.Application.Features.Contracts.Commands.RequestAdvancePayment;
using Wedding.Application.Features.Contracts.Queries.GetContractDetail;
using Wedding.Application.Features.Contracts.Queries.GetCustomerContracts;
using Wedding.Application.Features.Contracts.Queries.GetVendorContracts;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContractsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContractsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// [VendorOwner] Tạo hợp đồng chốt khách thực tế: Nhập mã Voucher (BR-004) + giá trị HĐ + tiền cọc.
    /// HĐ được tạo ở trạng thái PendingVerification và có hạn chót xác thực trong vòng 72h (BR-005).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> CreateContract([FromBody] CreateContractDraftCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetContractById), new { id = result.Id }, result);
    }

    /// <summary>
    /// [VendorOwner] Danh sách các hợp đồng dịch vụ cưới của Nhà cung cấp.
    /// </summary>
    [HttpGet("vendor")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> GetVendorContracts([FromQuery] string? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetVendorContractsQuery(status, pageNumber, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// [Customer] Danh sách các hợp đồng cưới của tôi (Cô dâu / Chú rể).
    /// </summary>
    [HttpGet("my-contracts")]
    public async Task<IActionResult> GetMyContracts([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetCustomerContractsQuery(pageNumber, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// Xem chi tiết thông tin một hợp đồng (Kiểm tra quyền Customer/Vendor).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetContractById(Guid id)
    {
        var result = await _mediator.Send(new GetContractDetailQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// [Customer] Xác thực giao dịch hợp đồng 2 chiều trong 72h (BR-005).
    /// Sau khi duyệt, hợp đồng chuyển sang 'Confirmed', voucher được redeem và tự động sinh bản ghi Commission Kỳ 1 (BR-006).
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> ConfirmContract(Guid id)
    {
        var result = await _mediator.Send(new ConfirmContractCommand(id));
        return Ok(new { message = "Xác nhận hợp đồng thành công. Chúc mừng bạn đã hoàn tất đặt dịch vụ cưới!", contract = result });
    }

    /// <summary>
    /// [Customer] Báo sai lệch thông tin hợp đồng. Hợp đồng chuyển về 'Draft' để NCC chỉnh sửa lại.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> RejectContract(Guid id, [FromBody] RejectContractRequest request)
    {
        await _mediator.Send(new RejectContractCommand(id, request.Reason));
        return Ok(new { message = "Đã gửi thông báo sai lệch tới Nhà cung cấp để điều chỉnh lại hợp đồng." });
    }

    /// <summary>
    /// [VendorOwner/Admin] Hoàn tất đám cưới sau ngày cưới.
    /// Hợp đồng chuyển sang 'Completed' và tự động sinh bản ghi Commission Kỳ 2 (50% còn lại theo FM-004 & BR-006).
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompleteContract(Guid id)
    {
        var result = await _mediator.Send(new CompleteContractCommand(id));
        return Ok(new { message = "Hoàn tất hợp đồng thành công. Đã sinh hoa hồng Kỳ 2 cho kỳ đối soát tiếp theo.", contract = result });
    }

    /// <summary>
    /// [VendorOwner] Gửi yêu cầu xin thanh toán trước một phần số tiền của hợp đồng.
    /// </summary>
    [HttpPost("{id:guid}/advance-payment")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> RequestAdvancePayment(Guid id, [FromBody] AdvancePaymentRequestDto request)
    {
        var result = await _mediator.Send(new RequestAdvancePaymentCommand(
            id,
            request.Amount,
            request.Reason,
            request.BankName,
            request.BankAccountNumber,
            request.BankAccountName,
            request.Note
        ));
        return Ok(result);
    }
}

public record RejectContractRequest(string Reason);
public record AdvancePaymentRequestDto(
    decimal Amount,
    string Reason,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountName,
    string? Note
);
