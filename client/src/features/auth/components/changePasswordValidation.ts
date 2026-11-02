export type ChangePasswordValues = { currentPassword: string; newPassword: string; confirmPassword: string };
export type ChangePasswordErrors = Partial<Record<keyof ChangePasswordValues, string>>;

export function validatePasswordChange(values: ChangePasswordValues): ChangePasswordErrors {
  const errors: ChangePasswordErrors = {};
  if (!values.currentPassword) errors.currentPassword = 'Nhập mật khẩu hiện tại để xác nhận chính bạn.';
  if (!values.newPassword.trim() || values.newPassword.length < 8 || new TextEncoder().encode(values.newPassword).length > 72)
    errors.newPassword = 'Mật khẩu mới cần ít nhất 8 ký tự và tối đa 72 byte UTF-8.';
  else if (values.newPassword === values.currentPassword) errors.newPassword = 'Mật khẩu mới phải khác mật khẩu hiện tại.';
  if (!values.confirmPassword || values.confirmPassword !== values.newPassword) errors.confirmPassword = 'Mật khẩu nhập lại chưa khớp.';
  return errors;
}
