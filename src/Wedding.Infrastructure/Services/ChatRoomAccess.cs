using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;

namespace Wedding.Infrastructure.Services;

/// <summary>
/// Matches the HTTP chat policy without trusting a room ID or a long-lived
/// SignalR handshake. Group addresses include the account's current session
/// version and role so revoked sockets cannot receive subsequent broadcasts.
/// </summary>
public sealed class ChatRoomAccess(IApplicationDbContext context)
{
    public async Task<ChatRoomMembership?> GetMembershipAsync(
        ClaimsPrincipal? principal, Guid leadId, bool forSending, CancellationToken cancellationToken = default)
    {
        if (leadId == Guid.Empty || principal?.Identity?.IsAuthenticated != true) return null;
        var identifiers = principal.FindAll(ClaimTypes.NameIdentifier).ToArray();
        if (identifiers.Length != 1 || !Guid.TryParse(identifiers[0].Value, out var userId) || userId == Guid.Empty) return null;

        // No tracked user: every hub invocation must observe account changes
        // made after the WebSocket's authentication handshake.
        var actor = await context.Users.AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => new { user.Id, user.Role, user.SecurityVersion })
            .SingleOrDefaultAsync(cancellationToken);
        if (actor is null || !JwtSessionPolicy.Matches(principal, actor.Role.ToString(), actor.SecurityVersion)) return null;

        var lead = await context.Leads.AsNoTracking().Where(item => item.Id == leadId)
            .Select(item => new { item.CustomerId, VendorOwnerId = item.Vendor.UserId })
            .SingleOrDefaultAsync(cancellationToken);
        if (lead is null) return null;
        var isCustomer = lead.CustomerId == userId;
        var isVendor = lead.VendorOwnerId == userId;
        var isStaffReader = actor.Role is UserRole.SuperAdmin or UserRole.Moderator;
        if (!isCustomer && !isVendor && (forSending || !isStaffReader)) return null;

        return new ChatRoomMembership(
            GroupName(leadId, actor.Id, actor.Role, actor.SecurityVersion), isCustomer, isVendor);
    }

    public async Task<IReadOnlyList<string>> GetCurrentRecipientGroupsAsync(
        Guid leadId, CancellationToken cancellationToken = default)
    {
        if (leadId == Guid.Empty) return [];
        var lead = await context.Leads.AsNoTracking().Where(item => item.Id == leadId)
            .Select(item => new { item.CustomerId, VendorOwnerId = item.Vendor.UserId })
            .SingleOrDefaultAsync(cancellationToken);
        if (lead is null) return [];

        // Never broadcast to the legacy shared lead-{id} group. A connection
        // authenticated before a password/status/role change is deliberately
        // absent from these current database-derived addresses.
        var recipients = await context.Users.AsNoTracking()
            .Where(user => user.IsActive && (user.Id == lead.CustomerId || user.Id == lead.VendorOwnerId
                || user.Role == UserRole.SuperAdmin || user.Role == UserRole.Moderator))
            .Select(user => new { user.Id, user.Role, user.SecurityVersion })
            .ToListAsync(cancellationToken);
        return recipients.Select(user => GroupName(leadId, user.Id, user.Role, user.SecurityVersion)).ToArray();
    }

    private static string GroupName(Guid leadId, Guid userId, UserRole role, int version) =>
        $"lead-{leadId:N}-user-{userId:N}-role-{role}-version-{version.ToString(CultureInfo.InvariantCulture)}";
}

public sealed record ChatRoomMembership(string GroupName, bool IsCustomer, bool IsVendor);
