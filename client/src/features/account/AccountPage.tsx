import { useRef, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Bell, ChevronRight, LogOut, PencilLine, Settings2, ShieldCheck, UserRound } from 'lucide-react';
import { useAuthStore } from '../../store/authStore';
import axiosClient from '../../services/axiosClient';
import type { User } from '../../types/auth';
import { dashboardPath } from '../../utils/navigation';
import { ChangePasswordForm } from '../auth/components/ChangePasswordDialog';
import GoogleAccountLink from './GoogleAccountLink';
import FacebookAccountLink from './FacebookAccountLink';
import ContactPhoneForm from '../auth/components/ContactPhoneForm';
import { accountTab, validateDisplayName } from './accountPolicy';
import { apiError } from '../journey/api';
import './account.css';

const sections = [
  { id: 'personal', label: 'Thông tin cá nhân', icon: UserRound },
  { id: 'edit', label: 'Chỉnh sửa thông tin', icon: PencilLine },
  { id: 'security', label: 'Mật khẩu & bảo mật', icon: ShieldCheck },
  { id: 'settings', label: 'Cài đặt tài khoản', icon: Settings2 },
] as const;
const roleNames: Record<User['role'], string> = { Customer: 'Khách hàng', VendorOwner: 'Chủ nhà cung cấp', Moderator: 'Kiểm duyệt viên', Finance: 'Tài chính', CustomerCare: 'Chăm sóc khách hàng', SuperAdmin: 'Quản trị viên' };

export default function AccountPage() {
  const user = useAuthStore(state => state.user)!;
  const [params, setParams] = useSearchParams();
  const active = accountTab(params.get('tab'));
  const navigate = useNavigate();
  return <main className="account-page">
    <header className="account-heading"><div><span className="wm-eyebrow">KHÔNG GIAN RIÊNG CỦA BẠN</span><h1>Tài khoản</h1><p>Thông tin và bảo mật, được sắp xếp gọn gàng ở một nơi.</p></div><Link className="wm-button small" to={dashboardPath(user)}>Về không gian của bạn <ChevronRight size={16} /></Link></header>
    <div className="account-grid">
      <aside className="account-sidebar"><div className="account-identity"><span className="account-initial" aria-hidden="true">{user.fullName.slice(0, 1)}</span><strong>{user.fullName}</strong><small>{roleNames[user.role]}</small></div>
        <nav aria-label="Khu vực tài khoản">{sections.map(({ id, label, icon: Icon }) => <Link key={id} to={id === 'personal' ? '/account' : `/account?tab=${id}`} aria-current={active === id ? 'page' : undefined}><Icon size={17} />{label}</Link>)}</nav>
      </aside>
      <div className="account-panel" key={active}>
        {active === 'personal' && <section aria-labelledby="personal-title"><h2 id="personal-title">Thông tin cá nhân</h2><p>Thông tin dùng để nhận diện và liên hệ trên Wedding Match.</p><dl className="account-details"><div><dt>Họ và tên</dt><dd>{user.fullName}</dd></div><div><dt>Email đăng nhập</dt><dd>{user.email}</dd></div><div><dt>Số điện thoại</dt><dd>{/^0\d{9}$/.test(user.phoneNumber) ? user.phoneNumber : 'Chưa cập nhật'}</dd></div><div><dt>Loại tài khoản</dt><dd>{roleNames[user.role]}</dd></div>{user.vendorBrandName && <div><dt>Thương hiệu</dt><dd>{user.vendorBrandName}</dd></div>}</dl><Link to="/account?tab=edit" className="wm-button small"><PencilLine size={15} />Chỉnh sửa thông tin</Link><p className="account-note">Đây là hồ sơ tài khoản, không phải biểu mẫu thiết lập đám cưới. Cập nhật tại đây không làm tạo lại checklist hoặc ngân sách.</p></section>}
        {active === 'edit' && <EditAccount key={user.id} />}
        {active === 'security' && <><ChangePasswordForm />{user.role === 'Customer' && <><GoogleAccountLink /><FacebookAccountLink /></>}</>}
        {active === 'settings' && <section><h2>Cài đặt tài khoản</h2><p>Các tùy chọn đang hoạt động cho tài khoản của bạn.</p><div className="account-settings"><Link to="/notifications"><Bell size={20} /><span><strong>Thông báo trong ứng dụng</strong><small>Xem tất cả, mở nội dung và đánh dấu đã đọc.</small></span><ChevronRight size={18} /></Link><Link to="/account?tab=security"><ShieldCheck size={20} /><span><strong>Bảo vệ tài khoản</strong><small>Đổi mật khẩu sẽ thu hồi các phiên đăng nhập cũ.</small></span><ChevronRight size={18} /></Link><button type="button" onClick={() => { useAuthStore.getState().logout(); navigate('/login', { replace: true }); }}><LogOut size={20} /><span><strong>Đăng xuất khỏi trình duyệt</strong><small>Không xóa tài khoản hoặc dữ liệu đã lưu trong SQL Server.</small></span></button></div><p className="account-note">Email đăng nhập và quyền tài khoản không thay đổi tại đây. Chưa có chức năng xóa tài khoản hoặc tùy chọn gửi email/SMS tự phục vụ.</p></section>}
      </div>
    </div>
    {/* Keep query navigation explicit and usable without hiding other account sections in a modal. */}
    {params.get('saved') === 'contact' && <p role="status" className="account-feedback">Đã lưu số liên hệ. Các phiên cũ đã hết hiệu lực nếu bạn thay số.<button onClick={() => setParams({ tab: active })} aria-label="Ẩn xác nhận">×</button></p>}
  </main>;
}

function EditAccount() {
  const user = useAuthStore(state => state.user)!;
  const session = useRef(useAuthStore.getState().token);
  const [name, setName] = useState(user.fullName);
  const [pending, setPending] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const inFlight = useRef(false);
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const editor = params.get('field') === 'phone' && user.role === 'Customer' ? 'phone' : 'name';
  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (inFlight.current) return;
    const validation = validateDisplayName(name); setError(validation ?? ''); setMessage('');
    if (validation || !session.current || useAuthStore.getState().token !== session.current) return;
    inFlight.current = true; setPending(true);
    try {
      const response = await axiosClient.patch<User>('/api/auth/profile', { fullName: name.trim() }, { headers: { Authorization: `Bearer ${session.current}` } });
      if (useAuthStore.getState().token !== session.current) return;
      useAuthStore.getState().confirmSession(response.data, session.current);
      setName(response.data.fullName); setMessage('Đã lưu họ và tên.');
    } catch (failure) { if (useAuthStore.getState().token === session.current) setError(apiError(failure)); }
    finally { inFlight.current = false; setPending(false); }
  }
  return <section><h2>Chỉnh sửa thông tin</h2><p>Chọn thông tin cần sửa. Không thay đổi email đăng nhập hoặc vai trò.</p>
    {user.role === 'Customer' && <nav className="account-edit-nav" aria-label="Thông tin cần chỉnh sửa"><Link to="/account?tab=edit" aria-current={editor === 'name' ? 'page' : undefined}>Họ và tên</Link><Link to="/account?tab=edit&field=phone" aria-current={editor === 'phone' ? 'page' : undefined}>Số điện thoại</Link></nav>}
    {editor === 'phone' ? <ContactPhoneForm onClose={() => {}} onSaved={() => navigate('/account?saved=contact', { replace: true })} /> : <form onSubmit={submit} noValidate aria-busy={pending} className="account-form"><label htmlFor="account-full-name">Họ và tên</label><input id="account-full-name" autoComplete="name" maxLength={150} required value={name} onChange={event => { setName(event.target.value); setError(''); setMessage(''); }} disabled={pending} aria-invalid={!!error} aria-describedby={error ? 'account-name-error' : undefined} />{error && <p role="alert" id="account-name-error" className="account-error">{error}</p>}{message && <p role="status" className="account-success">{message}</p>}<button type="submit" className="wm-button" disabled={pending}>{pending ? 'Đang lưu...' : 'Lưu thay đổi'}</button></form>}
    {user.role === 'VendorOwner' && <p className="account-note">Thông tin thương hiệu và hotline NCC được quản lý trong hồ sơ nhà cung cấp; đổi tên tài khoản không sửa hoặc công bố lại hồ sơ thương hiệu.</p>}
  </section>;
}
