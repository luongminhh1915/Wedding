using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

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
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để xem danh sách hợp đồng hợp lệ.");

        var contracts = await _context.BookingContracts
            .Include(c => c.Vendor)
            .Include(c => c.Lead)
            .Where(c => c.CustomerId == userId && (c.Status == ContractStatus.Completed || c.Status == ContractStatus.Confirmed))
            .OrderByDescending(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return contracts.Select(c => new EligibleContractDto(
            ContractId: c.Id,
            ContractCode: c.ContractCode,
            VendorId: c.VendorId,
            VendorBrandName: c.Vendor.BrandName,
            ListingId: c.Lead?.ListingId,
            ListingTitle: null,
            WeddingDate: c.WeddingDate
        )).ToList();
    }
}
