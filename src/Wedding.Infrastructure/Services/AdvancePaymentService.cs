using System.Collections.Concurrent;
using System.Text.Json;
using Wedding.Application.Common.Interfaces;

namespace Wedding.Infrastructure.Services;

public class AdvancePaymentService : IAdvancePaymentService
{
    private readonly ConcurrentDictionary<Guid, AdvancePaymentNoticeDto> _store = new();
    private readonly string _filePath;
    private readonly object _fileLock = new();

    public AdvancePaymentService()
    {
        _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "advance_payments.json");
        LoadFromFile();

        // Nếu chưa có thông báo nào, khởi tạo mẫu 1 thông báo chuyển tiền 20.000.000 đ cho hợp đồng HD-20260930-8202
        if (_store.IsEmpty)
        {
            var demoContractId = Guid.Parse("da6fa15c-c59a-45a7-94e0-552563e0eb96");
            var demoNotice = new AdvancePaymentNoticeDto(
                Id: Guid.NewGuid(),
                ContractId: demoContractId,
                ContractCode: "HD-20260930-8202",
                VendorId: Guid.Parse("e595124a-bc7b-42f7-9172-8ef8fcc25df0"),
                VendorBrandName: "White Palace Convention Center",
                Amount: 20000000m,
                Reason: "Yêu cầu thanh toán trước một phần hoa hồng qua chuyển khoản",
                BankName: "MB Bank (Quân Đội)",
                BankAccountNumber: "0338889999",
                BankAccountName: "WHITE PALACE CONVENTION CENTER",
                Note: "Đã chuyển 20 triệu qua VietQR với nội dung: HH E59512 T09",
                Status: "PendingApproval",
                RequestedAt: DateTime.UtcNow.AddMinutes(-30)
            );
            _store.TryAdd(demoNotice.Id, demoNotice);
            SaveToFile();
        }
    }

    private void LoadFromFile()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var list = JsonSerializer.Deserialize<List<AdvancePaymentNoticeDto>>(json);
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        _store[item.Id] = item;
                    }
                }
            }
        }
        catch
        {
            // fallback gracefully
        }
    }

    private void SaveToFile()
    {
        lock (_fileLock)
        {
            try
            {
                var list = _store.Values.ToList();
                var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // ignore write error
            }
        }
    }

    public Task<AdvancePaymentNoticeDto> CreateRequestAsync(
        Guid contractId,
        string contractCode,
        Guid vendorId,
        string vendorBrandName,
        decimal amount,
        string reason,
        string? bankName = null,
        string? bankAccountNumber = null,
        string? bankAccountName = null,
        string? note = null)
    {
        var notice = new AdvancePaymentNoticeDto(
            Id: Guid.NewGuid(),
            ContractId: contractId,
            ContractCode: contractCode,
            VendorId: vendorId,
            VendorBrandName: vendorBrandName,
            Amount: amount,
            Reason: reason,
            BankName: bankName,
            BankAccountNumber: bankAccountNumber,
            BankAccountName: bankAccountName,
            Note: note,
            Status: "PendingApproval",
            RequestedAt: DateTime.UtcNow
        );

        _store[notice.Id] = notice;
        SaveToFile();

        return Task.FromResult(notice);
    }

    public Task<List<AdvancePaymentNoticeDto>> GetAllAsync()
    {
        var list = _store.Values.OrderByDescending(n => n.RequestedAt).ToList();
        return Task.FromResult(list);
    }

    public Task<List<AdvancePaymentNoticeDto>> GetByContractIdAsync(Guid contractId)
    {
        var list = _store.Values
            .Where(n => n.ContractId == contractId)
            .OrderByDescending(n => n.RequestedAt)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<AdvancePaymentNoticeDto?> GetPendingByContractIdAsync(Guid contractId)
    {
        var notice = _store.Values
            .Where(n => n.ContractId == contractId && n.Status == "PendingApproval")
            .OrderByDescending(n => n.RequestedAt)
            .FirstOrDefault();
        return Task.FromResult(notice);
    }

    public Task<AdvancePaymentNoticeDto?> GetByIdAsync(Guid id)
    {
        _store.TryGetValue(id, out var notice);
        return Task.FromResult(notice);
    }

    public Task<AdvancePaymentNoticeDto?> ApproveAsync(Guid id, string? adminNote = null)
    {
        if (_store.TryGetValue(id, out var existing))
        {
            var updated = existing with
            {
                Status = "Approved",
                ProcessedAt = DateTime.UtcNow,
                AdminNote = adminNote ?? "Admin đã xác nhận nhận tiền thành công."
            };
            _store[id] = updated;
            SaveToFile();
            return Task.FromResult<AdvancePaymentNoticeDto?>(updated);
        }
        return Task.FromResult<AdvancePaymentNoticeDto?>(null);
    }

    public Task<AdvancePaymentNoticeDto?> RejectAsync(Guid id, string? reason = null)
    {
        if (_store.TryGetValue(id, out var existing))
        {
            var updated = existing with
            {
                Status = "Rejected",
                ProcessedAt = DateTime.UtcNow,
                AdminNote = reason ?? "Admin đã từ chối/báo chưa nhận được tiền."
            };
            _store[id] = updated;
            SaveToFile();
            return Task.FromResult<AdvancePaymentNoticeDto?>(updated);
        }
        return Task.FromResult<AdvancePaymentNoticeDto?>(null);
    }
}
