/** Personal planning only: recorded spending already includes any deposit.
 * Never use this display state to confirm a payment, a contract or a settlement.
 */
export function personalBudgetProgress(plannedCost: number, recordedSpent: number) {
  const balance = plannedCost - recordedSpent;
  const status = recordedSpent === 0 ? "unrecorded"
    : balance < 0 ? "over-plan"
      : balance === 0 ? "at-plan" : "partial";
  const labels = {
    unrecorded: "Chưa ghi chi",
    partial: "Đã ghi một phần",
    "at-plan": "Đã ghi đủ dự toán",
    "over-plan": "Vượt dự toán",
  };
  return { remaining: Math.max(balance, 0), excess: Math.max(-balance, 0), status, label: labels[status] };
}
