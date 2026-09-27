using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class ChecklistTask : BaseEntity
{
    public Guid CustomerId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Milestone { get; private set; } = "Trước 6 tháng";
    public DateTime? DueDate { get; private set; }
    public bool IsCompleted { get; private set; } = false;
    public DateTime? CompletedAt { get; private set; }
    public string? Notes { get; private set; }

    // Navigation Property
    public User Customer { get; private set; } = null!;

    private ChecklistTask() { } // Dành cho EF Core

    public static ChecklistTask Create(Guid customerId, string title, string milestone, 
        DateTime? dueDate = null, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Tên công việc không được để trống.");

        return new ChecklistTask
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Title = title.Trim(),
            Milestone = milestone.Trim(),
            DueDate = dueDate,
            IsCompleted = false,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void ToggleComplete()
    {
        IsCompleted = !IsCompleted;
        CompletedAt = IsCompleted ? DateTime.UtcNow : null;
        SetUpdated();
    }

    public void Update(string title, string milestone, DateTime? dueDate, string? notes)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Tên công việc không được để trống.");

        Title = title.Trim();
        Milestone = milestone.Trim();
        DueDate = dueDate;
        Notes = notes;
        SetUpdated();
    }
}
