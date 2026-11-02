using MediatR;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Commissions.Commands.ProcessBankWebhook;

// Compatibility DTO only. Unsigned/legacy payment allocation is intentionally impossible.
public record ProcessBankWebhookCommand(string Gateway, string? TransactionDate, string? AccountNumber, decimal Amount, string Content, string ReferenceCode) : IRequest<ProcessBankWebhookResult>;
public record ProcessBankWebhookResult(bool Success, int SettledCommissionsCount, decimal SettledAmount, string Message);
public class ProcessBankWebhookCommandHandler : IRequestHandler<ProcessBankWebhookCommand, ProcessBankWebhookResult>
{
    public Task<ProcessBankWebhookResult> Handle(ProcessBankWebhookCommand request, CancellationToken cancellationToken) => throw new DomainException("Unsigned webhook disabled. Use the signed billing webhook and durable transaction ledger.");
}
