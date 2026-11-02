using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Categories;
using Wedding.Application.Features.Categories.Queries.GetPublicCategories;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CategoriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// [Public] Lấy ngành hàng đang hoạt động kèm nhóm kinh doanh và số lượng bài đăng công khai.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _mediator.Send(new GetPublicCategoriesQuery());
        return Ok(result);
    }

    [HttpGet("my")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<ActionResult<VendorCategoryEligibilityDto>> My(CancellationToken ct)
        => Ok(await _mediator.Send(new GetMyCategoriesQuery(), ct));
}
