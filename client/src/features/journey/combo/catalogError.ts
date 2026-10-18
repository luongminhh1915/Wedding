/** Keep API diagnostics out of customer-facing errors and never expose response bodies. */
export function catalogErrorMessage(error: unknown, development = false): string {
  const response = typeof error === "object" && error !== null && "response" in error
    ? error.response : null;
  const status = typeof response === "object" && response !== null && "status" in response
    ? response.status : undefined;

  if (status === 404) {
    return development
      ? "API chưa có danh mục mới hoặc chưa đồng bộ với giao diện. Hãy khởi động lại backend bằng bản code mới rồi thử lại."
      : "Danh mục dịch vụ tạm thời chưa sẵn sàng. Vui lòng thử lại sau ít phút.";
  }
  return "Kiểm tra kết nối với API rồi thử lại.";
}
