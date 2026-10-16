using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Planner;

namespace Wedding.WebApi.Controllers;

[ApiController, Route("api/wedding"), Authorize(Roles = "Customer")]
public class WeddingController(IMediator mediator) : ControllerBase
{
    [HttpGet("onboarding-status")] public async Task<IActionResult> Onboarding() => Ok(await mediator.Send(new OnboardingStatusQuery()));
    [HttpGet("profile")] public async Task<IActionResult> Profile() => Ok(await mediator.Send(new GetWeddingProfileQuery()));
    [HttpPut("profile"), HttpPut("dates")] public async Task<IActionResult> Save(SaveWeddingProfileCommand command) => Ok(await mediator.Send(command));
    [HttpPost("onboarding")] public async Task<IActionResult> Onboarding(SaveWeddingProfileCommand command) => Ok(await mediator.Send(command with { GeneratePlan = true }));
    [HttpGet("members")] public async Task<IActionResult> Members() => Ok(await mediator.Send(new GetWeddingMembersQuery()));
    [HttpPut("budget")] public async Task<IActionResult> Budget(UpdateWeddingBudgetCommand command) => Ok(await mediator.Send(command));
    [HttpPost("members/invite")] public async Task<IActionResult> Invite(CreateWeddingJoinCommand command) => Ok(await mediator.Send(command));
    [HttpPost("join/{code}")] public async Task<IActionResult> Join(string code) => Ok(await mediator.Send(new JoinWeddingCommand(code)));
    [HttpPut("members/{id:guid}")] public async Task<IActionResult> Member(Guid id, MemberRoleRequest request) => Ok(await mediator.Send(new UpdateWeddingMemberCommand(id, request.Role)));
    [HttpDelete("members/{id:guid}")] public async Task<IActionResult> RemoveMember(Guid id) => Ok(await mediator.Send(new UpdateWeddingMemberCommand(id, null)));
    [HttpGet("guests"), HttpGet("guests/export")] public async Task<IActionResult> Guests() => Ok(await mediator.Send(new GetWeddingGuestsQuery()));
    [HttpPost("guests"), HttpPost("guests/import")] public async Task<IActionResult> Guests(SaveWeddingGuestsCommand command) => Ok(await mediator.Send(command));
    [HttpDelete("guests/{id:guid}")] public async Task<IActionResult> DeleteGuest(Guid id) => Ok(await mediator.Send(new DeleteWeddingGuestCommand(id)));
    [HttpPost("tables")] public async Task<IActionResult> Table(SaveSeatTableCommand command) => Ok(await mediator.Send(command));
    [HttpDelete("tables/{id:guid}")] public async Task<IActionResult> DeleteTable(Guid id) => Ok(await mediator.Send(new DeleteSeatTableCommand(id)));
    [HttpPost("tables/auto-assign")] public async Task<IActionResult> AutoAssign() => Ok(await mediator.Send(new AutoAssignTablesCommand()));
}
public record MemberRoleRequest(string? Role);
