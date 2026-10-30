using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Auth.Commands;

public record RequestPasswordResetCommand(string? Email) : IRequest<PasswordResetRequestResponse>
{
    public override string ToString() => "RequestPasswordResetCommand { [REDACTED] }";
}

public record PasswordResetRequestResponse(string Message);

public class RequestPasswordResetCommandHandler(
    IApplicationDbContext context,
    IPasswordResetDelivery delivery) : IRequestHandler<RequestPasswordResetCommand, PasswordResetRequestResponse>
{
    private const string Acknowledgement = "Nếu email khớp với tài khoản đang hoạt động, hướng dẫn đặt lại mật khẩu sẽ được gửi. Hãy kiểm tra cả thư mục thư rác.";

    public async Task<PasswordResetRequestResponse> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        // Keep the response identical for malformed, unknown, inactive, throttled, and eligible accounts.
        if (!delivery.PasswordRecoveryEnabled) return new(Acknowledgement);
        var email = AuthInputRules.Email(request.Email);
        if (!AuthInputRules.IsEmail(email)) return new(Acknowledgement);

        var pending = await context.ExecuteSerializableAsync(async ct =>
        {
            var user = await context.Users.SingleOrDefaultAsync(candidate => candidate.Email == email && candidate.IsActive, ct);
            if (user is null) return (TokenId: (Guid?)null, UserId: (Guid?)null, RawToken: (string?)null, Email: (string?)null);

            var now = DateTime.UtcNow;
            var since = now.AddMinutes(-1);
            if (await context.PasswordResetTokens.AnyAsync(token => token.UserId == user.Id && token.CreatedAt > since, ct))
                return (TokenId: (Guid?)null, UserId: (Guid?)null, RawToken: (string?)null, Email: (string?)null);
            if (await context.PasswordResetTokens.CountAsync(token => token.UserId == user.Id && token.CreatedAt > now.AddHours(-1), ct) >= 5)
                return (TokenId: (Guid?)null, UserId: (Guid?)null, RawToken: (string?)null, Email: (string?)null);

            var resetToken = PasswordResetToken.Create(user.Id, out var rawToken);
            context.PasswordResetTokens.Add(resetToken);
            await context.SaveChangesAsync(ct);
            return (TokenId: (Guid?)resetToken.Id, UserId: (Guid?)user.Id, RawToken: (string?)rawToken, Email: (string?)user.Email);
        }, cancellationToken);

        if (pending.TokenId.HasValue && pending.RawToken is not null && pending.Email is not null)
        {
            try
            {
                await delivery.SendPasswordResetAsync(pending.Email, pending.RawToken, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await RevokeUndeliveredToken(pending.TokenId.Value);
                throw;
            }
            catch
            {
                // Do not turn SMTP/account state into an account-enumeration signal.
                await RevokeUndeliveredToken(pending.TokenId.Value);
                return new(Acknowledgement);
            }

            // Keep a previously delivered link usable unless this new message was accepted by SMTP.
            try { await RevokeSupersededTokens(pending.UserId!.Value, pending.TokenId.Value); }
            catch { /* SMTP accepted the link; allow it and keep the generic response if cleanup is unavailable. */ }
        }

        return new(Acknowledgement);
    }

    private async Task RevokeUndeliveredToken(Guid tokenId)
    {
        try
        {
            var token = await context.PasswordResetTokens.SingleOrDefaultAsync(item => item.Id == tokenId, CancellationToken.None);
            if (token is null) return;
            token.Revoke(DateTime.UtcNow);
            await context.SaveChangesAsync(CancellationToken.None);
        }
        catch { /* A delivery or cleanup failure must not reveal whether the account exists. */ }
    }

    private async Task RevokeSupersededTokens(Guid userId, Guid currentTokenId)
    {
        await context.ExecuteSerializableAsync(async ct =>
        {
            var older = await context.PasswordResetTokens
                .Where(token => token.UserId == userId && token.Id != currentTokenId && token.UsedAt == null)
                .ToListAsync(ct);
            foreach (var token in older) token.Revoke(DateTime.UtcNow);
            if (older.Count > 0) await context.SaveChangesAsync(ct);
            return true;
        }, CancellationToken.None);
    }
}

public record ResetPasswordCommand(string? Token, string? NewPassword, string? ConfirmPassword) : IRequest
{
    public override string ToString() => "ResetPasswordCommand { [REDACTED] }";
}

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Token).NotEmpty().MaximumLength(256).WithMessage("Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
        RuleFor(command => command.NewPassword!).NewPassword();
        RuleFor(command => command.ConfirmPassword).Equal(command => command.NewPassword)
            .WithMessage("Mật khẩu nhập lại chưa khớp.");
    }
}

public class ResetPasswordCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher hasher) : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var validation = new ResetPasswordCommandValidator().Validate(request);
        if (!validation.IsValid) throw new DomainException(validation.Errors[0].ErrorMessage);

        var tokenHash = PasswordResetToken.Hash(request.Token!);
        await context.ExecuteSerializableAsync(async ct =>
        {
            var proof = await context.PasswordResetTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, ct);
            var now = DateTime.UtcNow;
            if (proof is null || !proof.TryConsume(now))
                throw new DomainException("Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Hãy yêu cầu liên kết mới.");

            var user = await context.Users.SingleOrDefaultAsync(candidate => candidate.Id == proof.UserId, ct);
            if (user is not { IsActive: true })
                throw new DomainException("Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Hãy liên hệ hỗ trợ nếu tài khoản bị khóa.");

            var otherTokens = await context.PasswordResetTokens
                .Where(token => token.UserId == user.Id && token.Id != proof.Id && token.UsedAt == null).ToListAsync(ct);
            foreach (var otherToken in otherTokens) otherToken.Revoke(now);

            user.UpdatePassword(hasher.Hash(request.NewPassword!));
            context.AuditLogs.Add(AuditLog.Create(null, "Anonymous", "Auth.PasswordReset", "User", user.Id.ToString()));
            await context.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }
}
