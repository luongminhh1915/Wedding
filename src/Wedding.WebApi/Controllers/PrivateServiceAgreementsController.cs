using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.PrivateServiceAgreements;

namespace Wedding.WebApi.Controllers;
[ApiController, Route("api/private-service-agreements/{kind}/{id:guid}"), Authorize(Roles = "Customer,VendorOwner")]
public class PrivateServiceAgreementsController(IMediator mediator) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get(string kind, Guid id, [FromQuery] Guid? leadId, CancellationToken ct)
        => Ok(await mediator.Send(new GetPrivateAgreementQuery(kind, id, leadId), ct));
    public record ProposalBody(string ExpectedVersion, List<PrivateServiceLine> Items, string Note, string? Terms);
    [HttpPost, Authorize(Roles = "VendorOwner")] public async Task<IActionResult> Propose(string kind, Guid id, ProposalBody body, CancellationToken ct)
        => Ok(await mediator.Send(new ProposePrivateAgreementCommand(kind, id, body.ExpectedVersion, body.Items, body.Note, body.Terms), ct));
    public record ResponseBody(string ExpectedVersion, bool Accept, string? Note);
    [HttpPost("{proposalId:guid}/respond"), Authorize(Roles = "Customer")] public async Task<IActionResult> Respond(string kind, Guid id, Guid proposalId, ResponseBody body, CancellationToken ct)
        => Ok(await mediator.Send(new RespondPrivateAgreementCommand(kind, id, proposalId, body.ExpectedVersion, body.Accept, body.Note), ct));
    public record WithdrawBody(string ExpectedVersion);
    [HttpPost("{proposalId:guid}/withdraw"), Authorize(Roles = "VendorOwner")] public async Task<IActionResult> Withdraw(string kind, Guid id, Guid proposalId, WithdrawBody body, CancellationToken ct)
        => Ok(await mediator.Send(new RespondPrivateAgreementCommand(kind, id, proposalId, body.ExpectedVersion, false, null, true), ct));
}
