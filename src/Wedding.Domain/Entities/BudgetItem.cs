using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class BudgetItem : BaseEntity
{
    public Guid CustomerId { get; private set; }
    public Guid? WeddingProfileId { get; private set; }
    public Guid? ContractId { get; private set; }
    public string? PlanTemplateKey { get; private set; }
    public string Owner { get; private set; } = "Shared";
    public Guid? CategoryId { get; private set; }
    public string ItemName { get; private set; } = string.Empty;
    public decimal PlannedCost { get; private set; }
    public decimal ActualCost { get; private set; }
    public string? Notes { get; private set; }
    public bool IsPaid { get; private set; } = false;

    // Navigation Properties
    public User Customer { get; private set; } = null!;
    public Category? Category { get; private set; }

    private BudgetItem() { } // Dành cho EF Core

    public static BudgetItem Create(Guid customerId, Guid? categoryId, string itemName, 
        decimal plannedCost, decimal actualCost = 0, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            throw new DomainException("Tên hạng mục ngân sách không được để trống.");
        if (plannedCost < 0 || actualCost < 0)
            throw new DomainException("Chi phí không thể là số âm.");

        return new BudgetItem
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            CategoryId = categoryId,
            ItemName = itemName.Trim(),
            PlannedCost = plannedCost,
            ActualCost = actualCost,
            Notes = notes,
            IsPaid = actualCost > 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateCosts(decimal plannedCost, decimal actualCost, string? notes, bool isPaid, bool preservePlanTemplate = false)
    {
        if (plannedCost < 0 || actualCost < 0)
            throw new DomainException("Chi phí không thể là số âm.");

        PlannedCost = plannedCost;
        ActualCost = actualCost;
        Notes = notes;
        IsPaid = isPaid;
        if (!preservePlanTemplate) PlanTemplateKey = null;
        SetUpdated();
    }

    public void AttachToWedding(Guid weddingProfileId, string owner = "Shared")
    { WeddingProfileId = weddingProfileId; AssignOwner(owner); }
    public void AssignOwner(string owner)
    { if (owner is not ("Shared" or "Bride" or "Groom")) throw new DomainException("Người phụ trách không hợp lệ."); Owner = owner; SetUpdated(); }
    public void Rename(string name)
    { if (string.IsNullOrWhiteSpace(name) || name.Length > 200) throw new DomainException("Tên khoản chi bắt buộc, tối đa 200 ký tự."); ItemName = name.Trim(); SetUpdated(); }
    public void LinkContract(Guid contractId) { ContractId = contractId; PlanTemplateKey = null; SetUpdated(); }
    public void MarkPlanTemplate(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100) throw new DomainException("Khóa mẫu ngân sách không hợp lệ.");
        PlanTemplateKey = key; SetUpdated();
    }
    public void AssignCategory(Guid? categoryId) { CategoryId = categoryId; SetUpdated(); }
    public void SynchronizeContract(decimal total, decimal paid)
    { UpdateCosts(total, paid, Notes, paid >= total); }
}
