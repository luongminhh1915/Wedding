using Microsoft.Extensions.Configuration;
using System.Net;
using Wedding.Application.Common.Interfaces;

namespace Wedding.Infrastructure.Services;

public class VietQrService : IVietQrService
{
    private readonly string _defaultBankId;
    private readonly string _defaultBankName;
    private readonly string _defaultAccountNumber;
    private readonly string _defaultAccountName;

    public VietQrService(IConfiguration configuration)
    {
        _defaultBankId = configuration["VietQr:BankId"] ?? "MB";
        _defaultBankName = configuration["VietQr:BankName"] ?? "MBBank - Ngân hàng Quân Đội";
        _defaultAccountNumber = configuration["VietQr:AccountNumber"] ?? "0338889999";
        _defaultAccountName = configuration["VietQr:AccountName"] ?? "WEDDING PLATFORM VIETNAM";
    }

    public VietQrInfo GenerateVietQr(decimal amount, string description)
    {
        var qrUrl = GenerateQrImageUrl(_defaultBankId, _defaultAccountNumber, _defaultAccountName, amount, description);

        return new VietQrInfo(
            QrImageUrl: qrUrl,
            BankId: _defaultBankId,
            BankName: _defaultBankName,
            AccountNumber: _defaultAccountNumber,
            AccountName: _defaultAccountName,
            Amount: amount,
            Description: description
        );
    }

    public string GenerateQrImageUrl(string bankId, string accountNumber, string accountName, decimal amount, string description)
    {
        var cleanBankId = bankId.Trim();
        var cleanAccountNo = accountNumber.Trim();
        var encodedAmount = ((long)amount).ToString();
        var encodedDesc = WebUtility.UrlEncode(description.Trim());
        var encodedName = WebUtility.UrlEncode(accountName.Trim());

        // Chuẩn URL VietQR QuickLink Napas247 tương thích 100% tất cả app ngân hàng tại Việt Nam
        return $"https://img.vietqr.io/image/{cleanBankId}-{cleanAccountNo}-compact2.png?amount={encodedAmount}&addInfo={encodedDesc}&accountName={encodedName}";
    }
}
