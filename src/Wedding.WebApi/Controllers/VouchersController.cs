using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Vouchers.Queries.GetCustomerVouchers;
using Wedding.Application.Features.Vouchers.Queries.ValidateVoucher;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VouchersController : ControllerBase
{
    private readonly IMediator _mediator;

    public VouchersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// [Customer] Xem danh sách mã ưu đãi Voucher đã nhận từ các yêu cầu tư vấn.
    /// Mỗi Voucher có hạn 30 ngày kể từ ngày cấp (BR-004).
    /// </summary>
    [HttpGet("my-vouchers")]
    public async Task<IActionResult> GetMyVouchers()
    {
        var result = await _mediator.Send(new GetCustomerVouchersQuery());
        return Ok(result);
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của mã Voucher 8 ký tự (BR-004) phục vụ việc lập hợp đồng.
    /// </summary>
    [HttpGet("validate/{code}")]
    public async Task<IActionResult> ValidateVoucher(string code, [FromQuery] Guid? vendorId)
    {
        var result = await _mediator.Send(new ValidateVoucherQuery(code, vendorId));
        return Ok(result);
    }
}
