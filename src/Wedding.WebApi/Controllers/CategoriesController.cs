using MediatR;
using Microsoft.AspNetCore.Mvc;
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
    /// [Public] Lấy 7 ngành hàng dịch vụ cưới kèm số lượng bài đăng Active.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _mediator.Send(new GetPublicCategoriesQuery());
        return Ok(result);
    }
}
