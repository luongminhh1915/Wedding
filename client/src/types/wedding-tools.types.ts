export interface BudgetItem {
  id: string;
  itemName: string;
  plannedCost: number;
  actualCost: number;
  notes?: string;
  isPaid: boolean;
  createdAt: string;
}

export interface BudgetSummary {
  totalPlannedBudget: number;
  totalActualCost: number;
  remainingBudget: number;
  spentPercentage: number;
  items: BudgetItem[];
}

export interface SaveBudgetItemPayload {
  id?: string;
  itemName: string;
  plannedCost: number;
  actualCost?: number;
  notes?: string;
}

export interface ChecklistTask {
  id: string;
  title: string;
  milestone: string;
  dueDate?: string;
  isCompleted: boolean;
  notes?: string;
  createdAt: string;
}

export interface SaveChecklistTaskPayload {
  id?: string;
  title: string;
  milestone: string;
  dueDate?: string;
  notes?: string;
}
