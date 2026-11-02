using System.Net.Mail;
using System.Text;
using FluentValidation;

namespace Wedding.Application.Features.Auth.Commands;

/// <summary>Keep public account inputs within the persisted schema and BCrypt's byte limit.</summary>
public static class AuthInputRules
{
    public static string Email(string? value) => (value ?? "").Trim().ToLowerInvariant();
    public static bool IsEmail(string? value)
    {
        var email = Email(value);
        return email.Length is > 0 and <= 150 && MailAddress.TryCreate(email, out var address)
            && address.Address == email && address.Host.Contains('.');
    }

    public static bool IsNewPassword(string? value) => value is { Length: >= 8 and <= 72 }
        && !string.IsNullOrWhiteSpace(value) && Encoding.UTF8.GetByteCount(value) <= 72;

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) => rule
        .Must(IsEmail).WithMessage("Email không hợp lệ hoặc vượt quá 150 ký tự.");

    public static IRuleBuilderOptions<T, string> NewPassword<T>(this IRuleBuilder<T, string> rule) => rule
        .Must(IsNewPassword).WithMessage("Mật khẩu cần ít nhất 8 ký tự và tối đa 72 byte UTF-8.");

    public static IRuleBuilderOptions<T, string> Phone<T>(this IRuleBuilder<T, string> rule) => rule
        .Must(value => value != null && System.Text.RegularExpressions.Regex.IsMatch(value.Trim(), @"^0[0-9]{9}$"))
        .WithMessage("Số điện thoại cần 10 chữ số, bắt đầu bằng 0.");

    public static bool RequiredText(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max;
}
