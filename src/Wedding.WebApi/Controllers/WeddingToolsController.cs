using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.WeddingTools.Commands.DeleteBudgetItem;
using Wedding.Application.Features.WeddingTools.Commands.DeleteChecklistTask;
using Wedding.Application.Features.WeddingTools.Commands.SaveBudgetItem;
using Wedding.Application.Features.WeddingTools.Commands.SaveChecklistTask;
using Wedding.Application.Features.WeddingTools.Commands.ToggleChecklistTask;
using Wedding.Application.Features.WeddingTools.DTOs;
using Wedding.Application.Features.WeddingTools.Queries.GetBudget;
using Wedding.Application.Features.WeddingTools.Queries.GetChecklist;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/wedding-tools")]
[Authorize]
public class WeddingToolsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WeddingToolsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// [Customer] Xem bảng dự toán ngân sách cưới (FM-006: Remaining_Budget = Planned - Actual).
    /// </summary>
    [HttpGet("budget")]
    public async Task<IActionResult> GetBudget()
    {
        var result = await _mediator.Send(new GetBudgetQuery());
        return Ok(result);
    }

    /// <summary>
    /// [Customer] Lưu hoặc sửa một hạng mục chi phí ngân sách.
    /// </summary>
    [HttpPost("budget/items")]
    public async Task<IActionResult> SaveBudgetItem([FromBody] SaveBudgetItemCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// [Customer] Xóa một hạng mục chi phí ngân sách.
    /// </summary>
    [HttpDelete("budget/items/{id:guid}")]
    public async Task<IActionResult> DeleteBudgetItem(Guid id)
    {
        var result = await _mediator.Send(new DeleteBudgetItemCommand(id));
        return Ok(new { success = result, message = "Đã xóa hạng mục ngân sách thành công." });
    }

    /// <summary>
    /// [Customer] Lấy danh sách việc cần làm 12 tháng chuẩn bị cưới (Checklist).
    /// </summary>
    [HttpGet("checklist")]
    public async Task<IActionResult> GetChecklist()
    {
        var result = await _mediator.Send(new GetChecklistQuery());
        return Ok(result);
    }

    /// <summary>
    /// [Customer] Thêm hoặc cập nhật công việc trong checklist.
    /// </summary>
    [HttpPost("checklist/tasks")]
    public async Task<IActionResult> SaveChecklistTask([FromBody] SaveChecklistTaskCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// [Customer] Đánh dấu hoàn thành / chưa hoàn thành công việc.
    /// </summary>
    [HttpPut("checklist/tasks/{id:guid}/toggle")]
    public async Task<IActionResult> ToggleChecklistTask(Guid id)
    {
        var result = await _mediator.Send(new ToggleChecklistTaskCommand(id));
        return Ok(new { isCompleted = result });
    }

    /// <summary>
    /// [Customer] Xóa công việc trong checklist.
    /// </summary>
    [HttpDelete("checklist/tasks/{id:guid}")]
    public async Task<IActionResult> DeleteChecklistTask(Guid id)
    {
        var result = await _mediator.Send(new DeleteChecklistTaskCommand(id));
        return Ok(new { success = result, message = "Đã xóa công việc thành công." });
    }
}
