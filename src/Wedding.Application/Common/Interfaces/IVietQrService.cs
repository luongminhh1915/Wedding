namespace Wedding.Application.Common.Interfaces;

public record VietQrInfo(
    string QrImageUrl,
    string BankId,
    string BankName,
    string AccountNumber,
    string AccountName,
    decimal Amount,
    string Description
);

public interface IVietQrService
{
    VietQrInfo GenerateVietQr(decimal amount, string description);
    string GenerateQrImageUrl(string bankId, string accountNumber, string accountName, decimal amount, string description);
}
