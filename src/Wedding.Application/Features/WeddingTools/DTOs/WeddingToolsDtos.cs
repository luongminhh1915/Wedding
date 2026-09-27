namespace Wedding.Application.Features.WeddingTools.DTOs;

public record BudgetItemDto(
    Guid Id,
    string ItemName,
    decimal PlannedCost,
    decimal ActualCost,
    string? Notes,
    bool IsPaid,
    DateTime CreatedAt
);

public record BudgetSummaryDto(
    decimal TotalPlannedBudget,
    decimal TotalActualCost,
    decimal RemainingBudget,
    double SpentPercentage,
    List<BudgetItemDto> Items
);

public record CreateBudgetItemRequest(
    string ItemName,
    decimal PlannedCost,
    decimal ActualCost = 0,
    string? Notes = null
);

public record ChecklistTaskDto(
    Guid Id,
    string Title,
    string Milestone,
    DateTime? DueDate,
    bool IsCompleted,
    string? Notes,
    DateTime CreatedAt
);

public record CreateChecklistTaskRequest(
    string Title,
    string Milestone,
    DateTime? DueDate = null,
    string? Notes = null
);
