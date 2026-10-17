import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Eye, EyeOff, KeyRound, X } from 'lucide-react';
import axiosClient from '../../../services/axiosClient';
import { useAuthStore } from '../../../store/authStore';
import { validatePasswordChange, type ChangePasswordErrors, type ChangePasswordValues } from './changePasswordValidation';
import ContactPhoneForm from './ContactPhoneForm';

const EMPTY: ChangePasswordValues = { currentPassword: '', newPassword: '', confirmPassword: '' };

export default function ChangePasswordDialog({ onClose }: { onClose: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [values, setValues] = useState(EMPTY);
  const [errors, setErrors] = useState<ChangePasswordErrors>({});
  const [message, setMessage] = useState('');
  const [pending, setPending] = useState(false);
  const [visible, setVisible] = useState(false);
  const [tab, setTab] = useState<'password' | 'contact'>('password');
  const isCustomer = useAuthStore(state => state.user?.role === 'Customer');
  const sending = useRef(false);
  const session = useRef(useAuthStore.getState().token);
  const navigate = useNavigate();
  useEffect(() => { const element = dialog.current; element?.showModal(); return () => element?.close(); }, []);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (sending.current) return;
    const validation = validatePasswordChange(values);
    setErrors(validation); setMessage('');
    if (Object.keys(validation).length) return;
    if (!session.current || useAuthStore.getState().token !== session.current) { onClose(); return; }
    sending.current = true; setPending(true);
    try {
      // Bind this operation to the identity present when the dialog opened, not a
      // different actor who might have signed in from another browser tab.
      await axiosClient.post('/api/auth/change-password', values, { headers: { Authorization: `Bearer ${session.current}` } });
      if (useAuthStore.getState().token === session.current) {
        useAuthStore.getState().logout();
        navigate('/login', { replace: true, state: { passwordChanged: true } });
      }
      onClose();
    } catch (error: unknown) {
      if (useAuthStore.getState().token !== session.current) { onClose(); return; }
      const body = (error as { response?: { data?: { message?: string } } })?.response?.data;
      setMessage(body?.message || 'Chưa thể đổi mật khẩu. Vui lòng kiểm tra kết nối và thử lại.');
    } finally { sending.current = false; setPending(false); }
  }

  const fields = [
    { name: 'currentPassword', label: 'Mật khẩu hiện tại', autoComplete: 'current-password' },
    { name: 'newPassword', label: 'Mật khẩu mới', autoComplete: 'new-password' },
    { name: 'confirmPassword', label: 'Nhập lại mật khẩu mới', autoComplete: 'new-password' },
  ] as const;
  return <dialog ref={dialog} className="wm-auth-dialog" aria-labelledby="change-password-title"
    onCancel={event => { if (pending) event.preventDefault(); else onClose(); }}
    onClick={event => { if (event.target === event.currentTarget && !pending) onClose(); }}>
    <button className="wm-auth-close" type="button" aria-label="Đóng đổi mật khẩu" disabled={pending} onClick={onClose}><X size={20} /></button>
    <div className="pt-3 pb-2"><KeyRound size={25} className="mb-3 text-amber-600" /><h2 id="change-password-title" className="serif text-2xl">Tài khoản của bạn</h2>
      {isCustomer && <div className="flex gap-2 mt-4 mb-5" role="group" aria-label="Cài đặt tài khoản"><button type="button" className={`btn ${tab === 'password' ? 'btn-primary' : 'btn-ghost'}`} aria-pressed={tab === 'password'} disabled={pending} onClick={() => setTab('password')}>Đổi mật khẩu</button><button type="button" className={`btn ${tab === 'contact' ? 'btn-primary' : 'btn-ghost'}`} aria-pressed={tab === 'contact'} disabled={pending} onClick={() => setTab('contact')}>Số liên hệ</button></div>}
      {tab === 'contact' ? <ContactPhoneForm onClose={onClose} /> : <>
      <p className="text-sm muted mt-2 mb-5">Sau khi đổi mật khẩu, các phiên cũ sẽ không còn hợp lệ. Bạn cần đăng nhập lại bằng mật khẩu mới.</p>
      <p className="text-xs muted mb-4">Nếu chỉ dùng Google hoặc mã email, hãy tiếp tục đăng nhập bằng phương thức đó. Không thể đặt mật khẩu mới khi chưa xác minh lại danh tính.</p>
      <form onSubmit={submit} className="space-y-4">
        {fields.map(field => <div key={field.name}>
          <label htmlFor={`password-${field.name}`} className="block text-sm font-semibold mb-2">{field.label}</label>
          <input id={`password-${field.name}`} type={visible ? 'text' : 'password'} autoComplete={field.autoComplete}
            autoFocus={field.name === 'currentPassword'} required maxLength={field.name === 'currentPassword' ? 256 : 72}
            value={values[field.name]} disabled={pending} aria-invalid={!!errors[field.name]} aria-describedby={errors[field.name] ? `error-${field.name}` : undefined}
            onChange={event => { setValues(current => ({ ...current, [field.name]: event.target.value })); setErrors(current => ({ ...current, [field.name]: undefined })); setMessage(''); }}
            className="w-full rounded-xl border border-slate-300 bg-white px-3 py-3 text-sm text-slate-900" />
          {errors[field.name] && <p id={`error-${field.name}`} className="text-xs text-red-700 mt-1">{errors[field.name]}</p>}
        </div>)}
        <button type="button" className="text-xs flex items-center gap-2" aria-pressed={visible} onClick={() => setVisible(value => !value)}>{visible ? <EyeOff size={16} /> : <Eye size={16} />}{visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}</button>
        {message && <p role="alert" className="p-3 rounded-lg bg-red-50 text-red-700 text-sm">{message}</p>}
        <button type="submit" className="btn btn-primary w-full" disabled={pending}>{pending ? 'Đang đổi mật khẩu...' : 'Đổi mật khẩu và đăng nhập lại'}</button>
      </form>
      </>}
    </div>
  </dialog>;
}
