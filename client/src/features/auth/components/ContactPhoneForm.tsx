import { useRef, useState } from 'react';
import axiosClient from '../../../services/axiosClient';
import { useAuthStore } from '../../../store/authStore';
import type { AuthResponse } from '../../../types/auth';

export default function ContactPhoneForm({ onClose }: { onClose: () => void }) {
  const session = useRef(useAuthStore.getState().token);
  const user = useAuthStore(state => state.user);
  const [phoneNumber, setPhoneNumber] = useState(() => /^0\d{9}$/.test(user?.phoneNumber || '') ? user!.phoneNumber : '');
  const [error, setError] = useState('');
  const [pending, setPending] = useState(false);
  const sending = useRef(false);
  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (sending.current) return;
    const phone = phoneNumber.trim();
    if (!/^0\d{9}$/.test(phone)) { setError('Số điện thoại cần 10 chữ số và bắt đầu bằng 0.'); return; }
    if (!session.current || useAuthStore.getState().token !== session.current) { onClose(); return; }
    sending.current = true; setPending(true); setError('');
    try {
      const response = await axiosClient.patch<AuthResponse>('/api/auth/contact', { phoneNumber: phone }, { headers: { Authorization: `Bearer ${session.current}` } });
      // Never replace a newer account/session with this response.
      if (useAuthStore.getState().token === session.current && useAuthStore.getState().user?.id === response.data.user.id)
        useAuthStore.getState().setAuth(response.data.user, response.data.token);
      onClose();
    } catch (failure: unknown) {
      if (useAuthStore.getState().token !== session.current) { onClose(); return; }
      const message = (failure as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(message || 'Chưa thể cập nhật liên hệ. Vui lòng thử lại.');
    } finally { sending.current = false; setPending(false); }
  }
  return <form onSubmit={submit} className="space-y-4">
    <p className="text-sm muted">Thêm số liên hệ để đặt lịch hẹn và nhận tư vấn, đặc biệt khi bạn đăng nhập bằng Google hoặc mã email. Không thay đổi kế hoạch hay câu trả lời thiết lập đám cưới.</p>
    <div><label htmlFor="account-contact-phone" className="block text-sm font-semibold mb-2">Số điện thoại liên hệ</label>
      <input id="account-contact-phone" type="tel" inputMode="tel" autoComplete="tel-national" maxLength={10} required value={phoneNumber} disabled={pending}
        onChange={event => { setPhoneNumber(event.target.value); setError(''); }} aria-invalid={!!error} aria-describedby="contact-phone-note"
        className="w-full rounded-xl border border-slate-300 bg-white px-3 py-3 text-sm text-slate-900" placeholder="09xxxxxxxx" />
      <p id="contact-phone-note" className="text-xs muted mt-2">Đây là số bạn tự cung cấp, chưa được xác minh OTP. Thay số sẽ làm các phiên cũ hết hiệu lực; phiên hiện tại được cấp lại an toàn.</p>
    </div>
    {error && <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm text-red-700">{error}</p>}
    <button type="submit" className="btn btn-primary w-full" disabled={pending}>{pending ? 'Đang lưu liên hệ...' : 'Lưu số điện thoại'}</button>
  </form>;
}
