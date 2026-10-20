// Định dạng tiền/ngày theo giờ Việt Nam cho các màn hình hành trình cặp đôi.
export const vnd = (n: number | null | undefined) => `${Math.round(n ?? 0).toLocaleString("vi-VN")}đ`;
export const shortVnd = (n: number) => (n >= 1e9 ? `${(n / 1e9).toFixed(1)} tỷ` : n >= 1e6 ? `${Math.round(n / 1e6)} triệu` : vnd(n));
export const vnDateTime = (iso: string) =>
  new Date(iso).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh", hour: "2-digit", minute: "2-digit", day: "2-digit", month: "2-digit", year: "numeric" });
export const vnDate = (iso?: string | null) => (iso ? new Date(iso).toLocaleDateString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "—");
