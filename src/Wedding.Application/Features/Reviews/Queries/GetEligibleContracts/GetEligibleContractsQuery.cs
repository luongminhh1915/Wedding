using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Application.Features.Listings;

namespace Wedding.Application.Features.Reviews.Queries.GetEligibleContracts;

public record GetEligibleContractsQuery : IRequest<List<EligibleContractDto>>;

public class GetEligibleContractsQueryHandler : IRequestHandler<GetEligibleContractsQuery, List<EligibleContractDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetEligibleContractsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<EligibleContractDto>> Handle(GetEligibleContractsQuery request, CancellationToken cancellationToken)
    {
        var customer = await ReviewRules.RequireActiveCustomerAsync(_context, _currentUserService, cancellationToken);
        var userId = customer.Id;
        var visibleListingIds = _context.Listings.PubliclyVisible().Select(listing => listing.Id);

        var contracts = await _context.BookingContracts
            .Include(c => c.Vendor)
            .Include(c => c.Lead).ThenInclude(lead => lead.Listing)
            .Where(c => c.CustomerId == userId && c.Status == ContractStatus.Completed)
            .Where(c => c.Lead.ListingId.HasValue && visibleListingIds.Contains(c.Lead.ListingId.Value))
            .Where(c => !_context.Reviews.Any(r => r.BookingContractId == c.Id || r.CustomerId == userId && r.ListingId == c.Lead.ListingId))
            .Where(c => !_context.BookingContracts.Any(d => d.CustomerId == userId && d.VendorId == c.VendorId && d.Lead.ListingId == c.Lead.ListingId && d.Status == ContractStatus.Disputed))
            .OrderByDescending(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return contracts.Where(c => ReviewRules.IsEligible(c, DateTime.UtcNow)).Select(c => new EligibleContractDto(
            ContractId: c.Id,
            ContractCode: c.ContractCode,
            VendorId: c.VendorId,
            VendorBrandName: c.Vendor.BrandName,
            ListingId: c.Lead?.ListingId,
            ListingTitle: c.Lead?.Listing?.Title,
            WeddingDate: c.WeddingDate
        )).ToList();
    }
}
