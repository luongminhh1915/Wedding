using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;

namespace Wedding.Application.Features.Categories.Queries.GetPublicCategories;

public record CategoryDto(Guid Id, string Name, string Slug, int ListingCount);

public record GetPublicCategoriesQuery : IRequest<List<CategoryDto>>;

public class GetPublicCategoriesQueryHandler : IRequestHandler<GetPublicCategoriesQuery, List<CategoryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPublicCategoriesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CategoryDto>> Handle(GetPublicCategoriesQuery request, CancellationToken cancellationToken)
    {
        return await _context.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Slug,
                c.Listings.Count(l => l.Status == Domain.Enums.ListingStatus.Active)
            ))
            .ToListAsync(cancellationToken);
    }
}
