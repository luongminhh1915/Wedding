/** Presentation validation only. Never rounds or recalculates contractual/commission amounts. */
export function contractAmounts(value: number | null, deposit: number | null, emptyDepositIsZero = false):
  { valid: true; contractValue: number; depositAmount: number } | { valid: false; error: string } {
  if (value == null || !Number.isFinite(value) || value <= 0)
    return { valid: false, error: "Vui lòng nhập giá trị hợp đồng hợp lệ." };
  const actualDeposit = deposit == null && emptyDepositIsZero ? 0 : deposit;
  if (actualDeposit == null || !Number.isFinite(actualDeposit) || actualDeposit < 0 || actualDeposit > value)
    return { valid: false, error: "Tiền cọc không hợp lệ (phải từ 0 đến giá trị HĐ)." };
  return { valid: true, contractValue: value, depositAmount: actualDeposit };
}
