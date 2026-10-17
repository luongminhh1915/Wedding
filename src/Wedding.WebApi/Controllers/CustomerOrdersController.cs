using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.CustomerOrders;

namespace Wedding.WebApi.Controllers;

[ApiController, Route("api/customer-orders"), Authorize]
public class CustomerOrdersController(IMediator mediator) : ControllerBase
{
    [HttpGet, Authorize(Roles = "Customer")]
    public async Task<IActionResult> Mine() => Ok(await mediator.Send(new GetCustomerOrdersQuery()));
    public record ReasonBody(string Reason);
    public record RequestBody(string Type, string Reason, string? ProposedChanges);
    public record CustomerOrderReviewBody(bool Accept, string Response);
    public record LeadRevisionBody(string Reason, Guid? ListingId, string? PhoneNumber, DateTime? WeddingDate, int? EstimatedGuests, decimal? EstimatedBudget, string? Notes);
    [HttpPost("{kind}/{id:guid}/archive"), Authorize(Roles = "Customer")]
    public async Task<IActionResult> Archive(string kind, Guid id) => Ok(await mediator.Send(new CustomerOrderActionCommand(kind, id, "archive")));
    [HttpPost("{kind}/{id:guid}/restore"), Authorize(Roles = "Customer")]
    public async Task<IActionResult> Restore(string kind, Guid id) => Ok(await mediator.Send(new CustomerOrderActionCommand(kind, id, "restore")));
    [HttpPost("{kind}/{id:guid}/withdraw"), Authorize(Roles = "Customer")]
    public async Task<IActionResult> Withdraw(string kind, Guid id, ReasonBody body) => Ok(await mediator.Send(new CustomerOrderActionCommand(kind, id, "withdraw", body.Reason)));
    [HttpPost("contract/{id:guid}/stop-pending"), Authorize(Roles = "Customer")]
    public async Task<IActionResult> Stop(Guid id, ReasonBody body) => Ok(await mediator.Send(new CustomerOrderActionCommand("contract", id, "stop-pending", body.Reason)));
    [HttpPost("lead/{id:guid}/revise"), Authorize(Roles = "Customer")]
    public async Task<IActionResult> ReviseLead(Guid id, LeadRevisionBody body) => Ok(await mediator.Send(new ReviseCustomerLeadCommand(id, body.Reason, body.ListingId, body.PhoneNumber, body.WeddingDate, body.EstimatedGuests, body.EstimatedBudget, body.Notes)));
    [HttpPost("{kind}/{id:guid}/requests"), Authorize(Roles = "Customer")]
    public async Task<IActionResult> CreateRequest(string kind, Guid id, RequestBody body) => Ok(await mediator.Send(new CreateOrderChangeRequestCommand(kind, id, body.Type, body.Reason, body.ProposedChanges)));
    [HttpGet("requests/vendor"), Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> VendorQueue() => Ok(await mediator.Send(new GetOrderChangeRequestsQuery(false)));
    [HttpGet("requests/finance"), Authorize(Roles = "Finance,SuperAdmin")]
    public async Task<IActionResult> FinanceQueue() => Ok(await mediator.Send(new GetOrderChangeRequestsQuery(true)));
    [HttpPost("requests/{id:guid}/vendor-review"), Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> VendorReview(Guid id, CustomerOrderReviewBody body) => Ok(await mediator.Send(new ReviewOrderChangeRequestCommand(id, false, body.Accept, body.Response)));
    [HttpPost("requests/{id:guid}/finance-review"), Authorize(Roles = "Finance,SuperAdmin")]
    public async Task<IActionResult> FinanceReview(Guid id, CustomerOrderReviewBody body) => Ok(await mediator.Send(new ReviewOrderChangeRequestCommand(id, true, body.Accept, body.Response)));
}
