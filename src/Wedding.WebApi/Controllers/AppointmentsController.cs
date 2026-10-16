using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Appointments;

namespace Wedding.WebApi.Controllers;

/// <summary>P3 – Đặt lịch hẹn với nhà cung cấp.</summary>
[ApiController]
[Route("api/appointments")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    public AppointmentsController(IMediator mediator) => _mediator = mediator;

    public record ActionBody(string? Note, DateTime? NewStartAt);

    [AllowAnonymous]
    [HttpGet("vendors/{vendorId:guid}/types")]
    public async Task<IActionResult> VendorTypes(Guid vendorId) => Ok(await _mediator.Send(new GetVendorAppointmentTypesQuery(vendorId)));

    [AllowAnonymous]
    [HttpGet("types/{typeId:guid}/slots")]
    public async Task<IActionResult> Slots(Guid typeId, [FromQuery] DateOnly date) => Ok(await _mediator.Send(new GetAvailableSlotsQuery(typeId, date)));

    [HttpPost]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Book([FromBody] BookAppointmentCommand command) => Ok(await _mediator.Send(command));

    [HttpGet("my")]
    public async Task<IActionResult> My() => Ok(await _mediator.Send(new GetMyAppointmentsQuery()));

    [HttpGet("{id:guid}.ics")]
    public async Task<IActionResult> Calendar(Guid id) => File(System.Text.Encoding.UTF8.GetBytes(await _mediator.Send(new GetAppointmentCalendarQuery(id))), "text/calendar; charset=utf-8", $"wedding-match-{id}.ics");

    /// <summary>Customer reports a transfer with pay-deposit; only the receiving vendor confirms money via confirm-deposit. Reschedules require the other party's consent.</summary>
    [HttpPost("{id:guid}/{op}")]
    public async Task<IActionResult> Act(Guid id, string op, [FromBody] ActionBody? body)
        => Ok(await _mediator.Send(new AppointmentActionCommand(id, op, body?.Note, body?.NewStartAt)));

    [HttpGet("vendor")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> VendorList([FromQuery] DateTime? from, [FromQuery] DateTime? to) => Ok(await _mediator.Send(new GetVendorAppointmentsQuery(from, to)));

    [HttpGet("vendor/types")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> MyTypes() => Ok(await _mediator.Send(new GetMyAppointmentTypesQuery()));

    [HttpPost("vendor/types")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> SaveType([FromBody] SaveAppointmentTypeCommand command) => Ok(await _mediator.Send(command));

    [HttpGet("vendor/types/{typeId:guid}/exceptions")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> Exceptions(Guid typeId) => Ok(await _mediator.Send(new GetAvailabilityExceptionsQuery(typeId)));

    [HttpPost("vendor/exceptions")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> SaveException([FromBody] SaveAvailabilityExceptionCommand command) => Ok(new { success = await _mediator.Send(command) });
}
