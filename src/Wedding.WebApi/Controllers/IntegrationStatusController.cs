using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;

namespace Wedding.WebApi.Controllers;
[ApiController, Authorize(Roles = "SuperAdmin"), Route("api/admin/integrations")]
public class IntegrationStatusController(IConfiguration configuration, IApplicationDbContext db, IOtpDelivery otp) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(new
    {
        databaseProvider = configuration["Database:Provider"] ?? "Sqlite", emailOtp = otp.EmailEnabled,
        google = !string.IsNullOrWhiteSpace(configuration["Google:ClientId"]),
        bankWebhook = (configuration["BankWebhook:Secret"]?.Length ?? 0) >= 32 && !string.IsNullOrWhiteSpace(configuration["Payments:AccountNumber"]) && configuration["Payments:AccountNumber"] == configuration["BankWebhook:AccountNumber"],
        zalo = false, eInvoiceAutomatic = false,
        legacyContractsNeedingFinancialReview = await db.BookingContracts.CountAsync(c => c.CommissionRateSnapshot == null && (c.Status == ContractStatus.Confirmed || c.Status == ContractStatus.Completed), ct)
    });
}
