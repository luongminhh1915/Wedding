import { useState, useMemo, useRef, useEffect } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Users, Search, Shield, Lock, Unlock, Phone, Mail,
  Calendar, XCircle, AlertCircle, Building2,
  UserCheck, UserX, Loader2, UserPlus, Eye, EyeOff, Copy
} from 'lucide-react';
import { useUsers, useUpdateUserStatus, userKeys } from './hooks/useUsers';
import type { UserManagementDto } from '../../types/auth';
import axiosClient from '../../services/axiosClient';
import { useAuthStore } from '../../store/authStore';
import { EMPTY_REGISTRATION, registrationApiError } from '../auth/components/registrationValidation';
import type { RegistrationValues } from '../auth/components/registrationValidation';
import { VendorBusinessGroupsEditor } from './VendorBusinessGroupsEditor';
import OperatingAreasSelect from '../auth/components/OperatingAreasSelect';
import { normalizeOperatingAreas } from '../auth/components/operatingAreasModel';
import type { OperatingAreas } from '../auth/components/operatingAreasModel';
import { ComboDialog } from '../journey/combo/ComboDialog';
import { ADMIN_CREATABLE_ROLES, adminRoleNames, adminCreateUserRequest, administrativeActorIsCurrent, validateAdminUserCreation } from './adminCreateUserModel';
import type { AdminCreatableRole, AdminCreateUserRequest, AdminCreationErrors } from './adminCreateUserModel';

function CopyLoginName({ value }: { value: string }) {
  const [message, setMessage] = useState('');
  return <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-[#233a42]"><span>Tên đăng nhập:</span><code className="select-all rounded-md border border-[#eadbd4] bg-[#fcf6ea] px-2 py-1 font-semibold [overflow-wrap:anywhere]">{value}</code><button type="button" className="inline-flex items-center gap-1 font-semibold underline underline-offset-2" onClick={() => { void navigator.clipboard.writeText(value).then(() => setMessage('Đã sao chép')).catch(() => setMessage('Bạn có thể chọn và sao chép tên đăng nhập ở bên cạnh.')); }}><Copy size={12} aria-hidden="true" />Sao chép</button>{message && <span role="status">{message}</span>}</div>;
}

export function UserCreationForm({ onCreated, onCancel }: { onCreated: (user: UserManagementDto) => void; onCancel: () => void }) {
  const queryClient = useQueryClient();
  const formRef = useRef<HTMLFormElement>(null);
  const inFlight = useRef(false);
  const mounted = useRef(true);
  const draftRevision = useRef(0);
  const [actor] = useState(() => {
    const session = useAuthStore.getState();
    return { id: session.user?.id, token: session.token, revision: session.sessionRevision };
  });
  const [values, setValues] = useState<RegistrationValues>({ ...EMPTY_REGISTRATION });
  const [role, setRole] = useState<AdminCreatableRole>('Customer');
  const [confirmSuperAdminCreation, setConfirmSuperAdminCreation] = useState(false);
  const [confirmation, setConfirmation] = useState<{ request: AdminCreateUserRequest; revision: number } | null>(null);
  const [errors, setErrors] = useState<AdminCreationErrors>({});
  const [failure, setFailure] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  const isCurrentActor = () => administrativeActorIsCurrent(actor, useAuthStore.getState());
  const createUser = useMutation({
    gcTime: 0,
    mutationFn: async (body: AdminCreateUserRequest) => {
      if (!mounted.current || !isCurrentActor()) throw new Error('Phiên quản trị đã thay đổi. Vui lòng mở lại biểu mẫu.');
      const response = await axiosClient.post<UserManagementDto>('/api/users', body, { headers: { Authorization: `Bearer ${actor.token}` } });
      return response.data;
    },
    onSuccess: () => { if (mounted.current && isCurrentActor()) void queryClient.invalidateQueries({ queryKey: userKeys.all }); },
  });
  const changeDraft = () => { draftRevision.current++; setConfirmation(null); setFailure(''); };
  const update = (field: keyof RegistrationValues, value: string) => {
    if (inFlight.current) return;
    changeDraft();
    setValues(current => ({ ...current, [field]: value }));
    setErrors(current => ({ ...current, [field]: undefined }));
  };
  const changeOperatingAreas = (selection: OperatingAreas) => {
    if (inFlight.current) return;
    changeDraft();
    setValues(current => ({ ...current, ...selection, city: selection.operatingRegions[0] ?? '' }));
    setErrors(current => ({ ...current, city: undefined }));
  };
  const provision = async (request: AdminCreateUserRequest, revision: number) => {
    if (inFlight.current || createUser.isPending || !mounted.current) return;
    if (!isCurrentActor()) { setFailure('Phiên quản trị đã thay đổi. Vui lòng mở lại biểu mẫu.'); return; }
    if (revision !== draftRevision.current) {
      setConfirmation(null); setFailure('Thông tin đã thay đổi. Vui lòng kiểm tra lại trước khi tạo tài khoản.');
      return;
    }
    inFlight.current = true;
    setFailure('');
    try {
      const user = await createUser.mutateAsync(request);
      if (!mounted.current || !isCurrentActor()) return;
      setValues({ ...EMPTY_REGISTRATION }); setShowPassword(false); setConfirmSuperAdminCreation(false); setConfirmation(null);
      onCreated(user);
    } catch (error: unknown) {
      if (mounted.current && isCurrentActor()) setFailure(registrationApiError(error));
    } finally { inFlight.current = false; createUser.reset(); }
  };
  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (inFlight.current || createUser.isPending) return;
    if (!isCurrentActor()) { setFailure('Phiên quản trị đã thay đổi. Vui lòng mở lại biểu mẫu.'); return; }
    setFailure('');
    const nextErrors = validateAdminUserCreation(values, role, confirmSuperAdminCreation);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length) {
      requestAnimationFrame(() => formRef.current?.querySelector<HTMLElement>("[aria-invalid='true']")?.focus());
      return;
    }
    const request = adminCreateUserRequest(values, role, confirmSuperAdminCreation);
    if (role === 'SuperAdmin') setConfirmation({ request, revision: draftRevision.current });
    else await provision(request, draftRevision.current);
  };
  const closeConfirmation = () => { if (!inFlight.current && !createUser.isPending) { setConfirmation(null); setFailure(''); } };
  const closeForm = () => {
    if (inFlight.current || createUser.isPending) return;
    setValues({ ...EMPTY_REGISTRATION }); setConfirmation(null); setShowPassword(false); onCancel();
  };
  const inputClass = 'w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-[#e38c82]';
  const fields: { key: 'fullName' | 'email' | 'phoneNumber' | 'password' | 'confirmPassword'; label: string; type: string; autoComplete: string; maxLength: number }[] = [
    { key: 'fullName', label: role === 'VendorOwner' ? 'Tên nhà cung cấp' : 'Họ và tên', type: 'text', autoComplete: 'name', maxLength: 150 },
    { key: 'email', label: role === 'VendorOwner' ? 'Email liên hệ (tùy chọn)' : 'Email đăng nhập', type: 'email', autoComplete: 'email', maxLength: 150 },
    { key: 'phoneNumber', label: role === 'VendorOwner' ? 'Số điện thoại (tùy chọn)' : 'Số điện thoại', type: 'tel', autoComplete: 'tel-national', maxLength: 20 },
    { key: 'password', label: 'Mật khẩu ban đầu', type: showPassword ? 'text' : 'password', autoComplete: 'new-password', maxLength: 72 },
    { key: 'confirmPassword', label: 'Nhập lại mật khẩu', type: showPassword ? 'text' : 'password', autoComplete: 'new-password', maxLength: 72 },
  ];
  return <section className="rounded-2xl border border-[#ded8c6] bg-[#fcf6ea] p-5 sm:p-6" aria-labelledby="create-user-title">
    <h3 id="create-user-title" className="text-base font-semibold text-[#233a42]">Tạo tài khoản người dùng</h3>
    <p className="mt-2 text-sm leading-relaxed text-slate-600">SuperAdmin có thể tạo khách hàng, nhà cung cấp và nhân sự. Hãy kiểm tra vai trò trước khi cấp tài khoản.</p>
    {failure && <p role="alert" className="mt-4 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">{failure}</p>}
    <form ref={formRef} onSubmit={submit} noValidate aria-busy={createUser.isPending} className="mt-5">
      <fieldset disabled={createUser.isPending} className="min-w-0">
        <legend className="sr-only">Thông tin tài khoản người dùng mới</legend>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          {fields.map(field => <div key={field.key}>
            <label htmlFor={`create-user-${field.key}`} className="mb-1.5 block text-xs font-semibold text-slate-700">{field.label}</label>
            <input id={`create-user-${field.key}`} name={field.key} type={field.type} autoComplete={field.autoComplete} autoCapitalize={field.key === 'email' ? 'none' : undefined} spellCheck={field.key === 'email' ? false : undefined} maxLength={field.maxLength} required={role !== 'VendorOwner' || field.key !== 'email' && field.key !== 'phoneNumber'} value={values[field.key]} onChange={event => update(field.key, event.target.value)} aria-invalid={errors[field.key] ? true : undefined} aria-describedby={errors[field.key] ? `create-user-${field.key}-error` : field.key === 'password' ? 'create-user-password-hint' : undefined} className={inputClass} />
            {errors[field.key] && <p id={`create-user-${field.key}-error`} className="mt-1.5 text-xs text-red-700">{errors[field.key]}</p>}
          </div>)}
          <div>
            <label htmlFor="create-user-role" className="mb-1.5 block text-xs font-semibold text-slate-700">Vai trò được cấp</label>
            <select id="create-user-role" value={role} onChange={event => { if (inFlight.current) return; changeDraft(); setRole(event.target.value as AdminCreatableRole); setConfirmSuperAdminCreation(false); setErrors({}); }} aria-invalid={errors.role ? true : undefined} className={inputClass}>
              {ADMIN_CREATABLE_ROLES.map(value => <option key={value} value={value}>{adminRoleNames[value]}</option>)}
            </select>
            {errors.role && <p className="mt-1.5 text-xs text-red-700">{errors.role}</p>}
          </div>
          {role === 'VendorOwner' && <>
            <div>
              <label htmlFor="create-user-brandName" className="mb-1.5 block text-xs font-semibold text-slate-700">Tên thương hiệu (tùy chọn)</label>
              <input id="create-user-brandName" name="brandName" className={inputClass} maxLength={200} placeholder="Để trống để dùng tên nhà cung cấp" value={values.brandName} onChange={event => update('brandName', event.target.value)} aria-invalid={errors.brandName ? true : undefined} aria-describedby={errors.brandName ? 'create-user-brandName-error' : undefined} />
              {errors.brandName && <p id="create-user-brandName-error" className="mt-1.5 text-xs text-red-700">{errors.brandName}</p>}
            </div>
            <OperatingAreasSelect key={`admin-vendor-${actor.id}`} id="create-user-city" {...normalizeOperatingAreas(values.city, values.operatingRegions, values.nationwide)} onChange={changeOperatingAreas} error={errors.city} disabled={createUser.isPending} />
          </>}
        </div>
        {role === 'VendorOwner' && <div className="mt-4 space-y-2 rounded-xl border border-[#eadbd4] bg-white p-3 text-xs leading-relaxed text-slate-600"><p>Chỉ cần tên và mật khẩu. Hệ thống sẽ tạo tên đăng nhập riêng và hiển thị sau khi tạo; NCC dùng tên đó cùng mật khẩu để đăng nhập. Email, số điện thoại, thương hiệu và khu vực có thể bổ sung sau.</p><p>Hồ sơ shop chưa xác minh. Thao tác này không ghi nhận NCC chấp thuận điều khoản, không tự duyệt shop hoặc kích hoạt thỏa thuận hoa hồng riêng.</p></div>}
        {role === 'SuperAdmin' && <div className="mt-4 rounded-xl border border-rose-200 bg-rose-50 p-4">
          <p className="text-sm font-semibold text-rose-900">SuperAdmin có toàn quyền quản trị hệ thống và cấp tài khoản khác.</p>
          <label className="mt-3 flex items-start gap-2 text-sm text-rose-900"><input id="create-user-confirm-admin" type="checkbox" className="mt-0.5" checked={confirmSuperAdminCreation} onChange={event => { if (inFlight.current) return; changeDraft(); setConfirmSuperAdminCreation(event.target.checked); setErrors(current => ({ ...current, confirmSuperAdminCreation: undefined })); }} aria-invalid={errors.confirmSuperAdminCreation ? true : undefined} aria-describedby={errors.confirmSuperAdminCreation ? 'create-user-confirm-admin-error' : undefined} />Tôi xác nhận cấp toàn quyền SuperAdmin cho tài khoản mới.</label>
          {errors.confirmSuperAdminCreation && <p id="create-user-confirm-admin-error" className="mt-2 text-xs text-red-700">{errors.confirmSuperAdminCreation}</p>}
        </div>}
        <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
          <p id="create-user-password-hint" className="text-xs leading-relaxed text-slate-500">Mật khẩu ít nhất {role === 'SuperAdmin' ? 12 : 8} ký tự, tối đa 72 byte. Hệ thống không tự gửi mật khẩu qua email; hãy bàn giao qua kênh riêng an toàn.</p>
          <button type="button" onClick={() => setShowPassword(value => !value)} aria-pressed={showPassword} className="flex items-center gap-1.5 text-xs text-[#233a42]">{showPassword ? <EyeOff size={15} /> : <Eye size={15} />}{showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}</button>
        </div>
        <div className="mt-5 flex flex-wrap justify-end gap-3">
          <button type="button" onClick={closeForm} className="rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm text-slate-600">Đóng biểu mẫu</button>
          <button type="submit" disabled={createUser.isPending} className="flex items-center gap-2 rounded-xl bg-[#e38c82] px-4 py-2.5 text-sm font-semibold text-[#233a42] disabled:opacity-60">{createUser.isPending && <Loader2 size={16} className="animate-spin" />}{createUser.isPending ? 'Đang tạo tài khoản...' : role === 'SuperAdmin' ? 'Kiểm tra và cấp SuperAdmin' : 'Tạo tài khoản'}</button>
        </div>
      </fieldset>
    </form>
    {confirmation && <ComboDialog title="Xác nhận tạo SuperAdmin" onClose={closeConfirmation}>
      <div className="space-y-4 text-sm text-[#233a42] [overflow-wrap:anywhere]">
        <p>Tài khoản này sẽ có toàn quyền quản trị, bao gồm khả năng cấp thêm tài khoản khác. Kiểm tra kỹ người được cấp quyền.</p>
        <dl className="space-y-3 rounded-xl border border-[#eadbd4] bg-[#fcf6ea] p-4">
          <div><dt className="text-xs text-slate-500">Họ và tên</dt><dd className="font-semibold">{confirmation.request.fullName}</dd></div>
          <div><dt className="text-xs text-slate-500">Email đăng nhập</dt><dd>{confirmation.request.email}</dd></div>
          <div><dt className="text-xs text-slate-500">Vai trò</dt><dd className="font-semibold">{adminRoleNames[confirmation.request.role]}</dd></div>
        </dl>
        {failure && <p role="alert" className="rounded-xl bg-red-50 p-3 text-red-700">{failure}</p>}
        <div className="flex flex-wrap justify-end gap-3 border-t border-[#eadbd4] pt-4">
          <button type="button" autoFocus disabled={createUser.isPending} onClick={closeConfirmation} className="btn btn-outline">Hủy</button>
          <button type="button" disabled={createUser.isPending} onClick={() => void provision(confirmation.request, confirmation.revision)} className="btn btn-primary">{createUser.isPending ? 'Đang tạo tài khoản...' : 'Xác nhận tạo SuperAdmin'}</button>
        </div>
      </div>
    </ComboDialog>}
  </section>;
}

export default function UserManagement() {
  const isSuperAdmin = useAuthStore(state => state.user?.role === 'SuperAdmin');
  const actorId = useAuthStore(state => state.user?.id);
  const sessionRevision = useAuthStore(state => state.sessionRevision);
  const { data: users = [], isLoading, error } = useUsers();
  const updateStatusMutation = useUpdateUserStatus();

  const [search, setSearch] = useState('');
  const [selectedRole, setSelectedRole] = useState<string>('ALL');
  const [selectedStatus, setSelectedStatus] = useState<string>('ALL');
  const [selectedUser, setSelectedUser] = useState<UserManagementDto | null>(null);
  const [confirmLockModal, setConfirmLockModal] = useState<{ user: UserManagementDto; targetStatus: boolean } | null>(null);
  const [showStaffForm, setShowStaffForm] = useState(false);
  const [staffSuccess, setStaffSuccess] = useState('');
  const [createdLoginName, setCreatedLoginName] = useState('');

  // Thống kê nhanh
  const stats = useMemo(() => {
    return {
      total: users.length,
      customers: users.filter(u => u.role === 'Customer').length,
      vendors: users.filter(u => u.role === 'VendorOwner').length,
      staff: users.filter(u => ['SuperAdmin', 'Moderator', 'Finance', 'CustomerCare'].includes(u.role)).length,
      locked: users.filter(u => !u.isActive).length,
    };
  }, [users]);

  // Bộ lọc
  const filteredUsers = useMemo(() => {
    return users.filter(user => {
      const matchSearch =
        user.fullName.toLowerCase().includes(search.toLowerCase()) ||
        (user.loginName ?? '').toLowerCase().includes(search.toLowerCase()) ||
        user.email.toLowerCase().includes(search.toLowerCase()) ||
        user.phoneNumber.includes(search) ||
        (user.vendorBrandName && user.vendorBrandName.toLowerCase().includes(search.toLowerCase()));

      const matchRole = selectedRole === 'ALL' || user.role === selectedRole;
      const matchStatus =
        selectedStatus === 'ALL' ||
        (selectedStatus === 'ACTIVE' && user.isActive) ||
        (selectedStatus === 'INACTIVE' && !user.isActive);

      return matchSearch && matchRole && matchStatus;
    });
  }, [users, search, selectedRole, selectedStatus]);

  const handleToggleStatus = async () => {
    if (!confirmLockModal) return;
    try {
      await updateStatusMutation.mutateAsync({
        id: confirmLockModal.user.id,
        isActive: confirmLockModal.targetStatus,
      });
      setConfirmLockModal(null);
      if (selectedUser?.id === confirmLockModal.user.id) {
        setSelectedUser({ ...selectedUser, isActive: confirmLockModal.targetStatus });
      }
    } catch {
      alert('Có lỗi xảy ra khi cập nhật trạng thái tài khoản.');
    }
  };

  const getRoleBadge = (role: string) => {
    switch (role) {
      case 'SuperAdmin':
        return <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-rose-100 text-rose-800 border border-rose-200">SuperAdmin</span>;
      case 'Moderator':
        return <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-100 text-amber-800 border border-amber-200">Moderator</span>;
      case 'VendorOwner':
        return <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-purple-100 text-purple-800 border border-purple-200">Nhà Cung Cấp</span>;
      case 'Finance':
        return <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800 border border-emerald-200">Kế Toán</span>;
      case 'CustomerCare':
        return <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-cyan-100 text-cyan-800 border border-cyan-200">CSKH</span>;
      default:
        return <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-sky-100 text-sky-800 border border-sky-200">Khách Hàng</span>;
    }
  };

  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center py-20 bg-white rounded-2xl border border-slate-200">
        <Loader2 className="w-8 h-8 animate-spin text-rose-500 mb-3" />
        <p className="text-sm font-medium text-slate-600">Đang tải danh sách người dùng...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6 bg-red-50 rounded-2xl border border-red-200 text-center">
        <AlertCircle className="w-8 h-8 text-red-500 mx-auto mb-2" />
        <p className="font-semibold text-red-800">Không thể tải dữ liệu người dùng</p>
        <p className="text-sm text-red-600 mt-1">Vui lòng kiểm tra quyền truy cập hoặc kết nối mạng.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {isSuperAdmin && <div className="flex flex-wrap items-center justify-between gap-3">
        <div><h2 className="text-lg font-semibold text-[#233a42]">Tài khoản & phân quyền</h2><p className="mt-1 text-sm text-slate-500">SuperAdmin có thể tạo tài khoản cho tất cả vai trò. Khách hàng và NCC vẫn có thể tự đăng ký.</p></div>
        {!showStaffForm && <button type="button" onClick={() => { setShowStaffForm(true); setStaffSuccess(''); }} className="flex items-center gap-2 rounded-xl bg-[#e38c82] px-4 py-2.5 text-sm font-semibold text-[#233a42]"><UserPlus size={17} />Tạo tài khoản người dùng</button>}
      </div>}
      {isSuperAdmin && showStaffForm && <UserCreationForm key={`${actorId}:${sessionRevision}`} onCancel={() => setShowStaffForm(false)} onCreated={user => { setShowStaffForm(false); setCreatedLoginName(user.loginName ?? ''); setStaffSuccess(`Đã tạo tài khoản ${user.fullName} với vai trò ${adminRoleNames[user.role as AdminCreatableRole] ?? user.role}. Phiên quản trị hiện tại được giữ nguyên.`); }} />}
      {staffSuccess && <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800"><p role="status">{staffSuccess}</p>{createdLoginName && <><CopyLoginName value={createdLoginName} /><p className="mt-2 text-xs">Bàn giao tên đăng nhập này cùng mật khẩu đã đặt cho NCC.</p></>}</div>}
      {/* Top Banner & Stats */}
      <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">Tổng người dùng</p>
            <p className="text-2xl font-bold text-slate-900 mt-1">{stats.total}</p>
          </div>
          <div className="w-10 h-10 rounded-lg bg-slate-100 flex items-center justify-center text-slate-700">
            <Users className="w-5 h-5" />
          </div>
        </div>

        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">Khách hàng</p>
            <p className="text-2xl font-bold text-sky-600 mt-1">{stats.customers}</p>
          </div>
          <div className="w-10 h-10 rounded-lg bg-sky-50 flex items-center justify-center text-sky-600">
            <UserCheck className="w-5 h-5" />
          </div>
        </div>

        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">Nhà cung cấp (NCC)</p>
            <p className="text-2xl font-bold text-purple-600 mt-1">{stats.vendors}</p>
          </div>
          <div className="w-10 h-10 rounded-lg bg-purple-50 flex items-center justify-center text-purple-600">
            <Building2 className="w-5 h-5" />
          </div>
        </div>

        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">Đội ngũ Quản trị</p>
            <p className="text-2xl font-bold text-rose-600 mt-1">{stats.staff}</p>
          </div>
          <div className="w-10 h-10 rounded-lg bg-rose-50 flex items-center justify-center text-rose-600">
            <Shield className="w-5 h-5" />
          </div>
        </div>

        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex items-center justify-between col-span-2 md:col-span-1">
          <div>
            <p className="text-xs font-medium text-slate-500">Tài khoản bị khóa</p>
            <p className="text-2xl font-bold text-amber-600 mt-1">{stats.locked}</p>
          </div>
          <div className="w-10 h-10 rounded-lg bg-amber-50 flex items-center justify-center text-amber-600">
            <UserX className="w-5 h-5" />
          </div>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex flex-col md:flex-row items-center gap-3">
        <div className="relative flex-1 w-full">
          <Search className="w-4 h-4 text-slate-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
          <input
            type="text"
            placeholder="Tìm theo họ tên, email, số điện thoại, thương hiệu..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full pl-9 pr-4 py-2 text-sm bg-slate-50 border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
          />
        </div>

        <div className="flex items-center gap-2 w-full md:w-auto">
          <select
            value={selectedRole}
            onChange={(e) => setSelectedRole(e.target.value)}
            className="px-3 py-2 text-sm bg-slate-50 border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500 text-slate-700"
          >
            <option value="ALL">Tất cả vai trò</option>
            <option value="Customer">Khách hàng (Customer)</option>
            <option value="VendorOwner">Nhà cung cấp (VendorOwner)</option>
            <option value="Moderator">Kiểm duyệt (Moderator)</option>
            <option value="Finance">Kế toán (Finance)</option>
            <option value="CustomerCare">CSKH (CustomerCare)</option>
            <option value="SuperAdmin">SuperAdmin</option>
          </select>

          <select
            value={selectedStatus}
            onChange={(e) => setSelectedStatus(e.target.value)}
            className="px-3 py-2 text-sm bg-slate-50 border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500 text-slate-700"
          >
            <option value="ALL">Tất cả trạng thái</option>
            <option value="ACTIVE">Đang hoạt động</option>
            <option value="INACTIVE">Đã khóa</option>
          </select>
        </div>
      </div>

      {/* Main Table */}
      <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-50 border-b border-slate-200 text-xs font-semibold text-slate-500 uppercase tracking-wider">
              <tr>
                <th className="px-6 py-3.5">Người dùng</th>
                <th className="px-6 py-3.5">Vai trò</th>
                <th className="px-6 py-3.5">Liên hệ</th>
                <th className="px-6 py-3.5">Trạng thái</th>
                <th className="px-6 py-3.5">Ngày tham gia</th>
                <th className="px-6 py-3.5 text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {filteredUsers.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-6 py-12 text-center text-slate-500">
                    <Users className="w-8 h-8 text-slate-300 mx-auto mb-2" />
                    Không tìm thấy người dùng phù hợp.
                  </td>
                </tr>
              ) : (
                filteredUsers.map((user) => (
                  <tr key={user.id} className="hover:bg-slate-50/70 transition">
                    <td className="px-6 py-4">
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 rounded-full bg-gradient-to-tr from-rose-500 to-rose-400 text-white font-bold flex items-center justify-center text-sm shadow-sm flex-shrink-0">
                          {user.fullName.charAt(0).toUpperCase()}
                        </div>
                        <div>
                          <p className="font-semibold text-slate-900">{user.fullName}</p>
                          <p className="select-all text-xs text-slate-500">{user.loginName ? `Đăng nhập: ${user.loginName}` : user.email || 'Chưa có email liên hệ'}</p>
                          {user.vendorBrandName && (
                            <span className="inline-flex items-center gap-1 text-[11px] text-purple-700 font-medium mt-0.5">
                              <Building2 className="w-3 h-3" />
                              {user.vendorBrandName}
                            </span>
                          )}
                        </div>
                      </div>
                    </td>
                    <td className="px-6 py-4">
                      {getRoleBadge(user.role)}
                    </td>
                    <td className="px-6 py-4">
                      <div className="text-xs text-slate-600 space-y-1">
                        <div className="flex items-center gap-1.5">
                          <Phone className="w-3.5 h-3.5 text-slate-400" />
                          <span>{user.phoneNumber || 'Chưa có số điện thoại'}</span>
                        </div>
                        <div className="flex items-center gap-1.5">
                          <Mail className="w-3.5 h-3.5 text-slate-400" />
                          <span className="truncate max-w-[150px]">{user.email || 'Chưa có email liên hệ'}</span>
                        </div>
                      </div>
                    </td>
                    <td className="px-6 py-4">
                      {user.isActive ? (
                        <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200">
                          <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />
                          Hoạt động
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-red-50 text-red-700 border border-red-200">
                          <XCircle className="w-3.5 h-3.5 text-red-500" />
                          Đã khóa
                        </span>
                      )}
                    </td>
                    <td className="px-6 py-4 text-xs text-slate-500">
                      <div className="flex items-center gap-1">
                        <Calendar className="w-3.5 h-3.5 text-slate-400" />
                        <span>{new Date(user.createdAt).toLocaleDateString('vi-VN')}</span>
                      </div>
                    </td>
                    <td className="px-6 py-4 text-right">
                      <div className="flex items-center justify-end gap-2">
                        <button
                          onClick={() => setSelectedUser(user)}
                          className="px-3 py-1.5 text-xs font-medium text-slate-700 hover:text-slate-900 bg-slate-100 hover:bg-slate-200 rounded-lg transition"
                        >
                          Chi tiết
                        </button>

                        {user.role !== 'SuperAdmin' && (
                          user.isActive ? (
                            <button
                              onClick={() => setConfirmLockModal({ user, targetStatus: false })}
                              className="inline-flex items-center gap-1 px-3 py-1.5 text-xs font-medium text-red-700 hover:text-red-800 bg-red-50 hover:bg-red-100 border border-red-200 rounded-lg transition"
                              title="Khóa tài khoản"
                            >
                              <Lock className="w-3.5 h-3.5" />
                              Khóa
                            </button>
                          ) : (
                            <button
                              onClick={() => setConfirmLockModal({ user, targetStatus: true })}
                              className="inline-flex items-center gap-1 px-3 py-1.5 text-xs font-medium text-emerald-700 hover:text-emerald-800 bg-emerald-50 hover:bg-emerald-100 border border-emerald-200 rounded-lg transition"
                              title="Mở khóa tài khoản"
                            >
                              <Unlock className="w-3.5 h-3.5" />
                              Mở
                            </button>
                          )
                        )}
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Confirmation Modal */}
      {confirmLockModal && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-xl border border-slate-100 animate-in fade-in zoom-in-95 duration-150">
            <div className="flex items-center gap-3 mb-4">
              <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${
                confirmLockModal.targetStatus ? 'bg-emerald-100 text-emerald-600' : 'bg-red-100 text-red-600'
              }`}>
                {confirmLockModal.targetStatus ? <Unlock className="w-5 h-5" /> : <Lock className="w-5 h-5" />}
              </div>
              <div>
                <h3 className="text-base font-bold text-slate-900">
                  {confirmLockModal.targetStatus ? 'Kích hoạt lại tài khoản' : 'Khóa tài khoản người dùng'}
                </h3>
                <p className="text-xs text-slate-500">Hành động này áp dụng ngay lập tức</p>
              </div>
            </div>

            <p className="text-sm text-slate-600 mb-6">
              Bạn có chắc chắn muốn {confirmLockModal.targetStatus ? 'mở khóa' : 'khóa'} tài khoản của{' '}
              <strong className="text-slate-900">{confirmLockModal.user.fullName}</strong> ({confirmLockModal.user.email})?
              {!confirmLockModal.targetStatus && ' Người dùng này sẽ không thể đăng nhập hoặc thực hiện thao tác trên hệ thống.'}
            </p>

            <div className="flex items-center justify-end gap-3">
              <button
                onClick={() => setConfirmLockModal(null)}
                className="px-4 py-2 text-sm font-medium text-slate-600 hover:text-slate-900 hover:bg-slate-100 rounded-xl transition"
              >
                Hủy
              </button>
              <button
                onClick={handleToggleStatus}
                disabled={updateStatusMutation.isPending}
                className={`px-4 py-2 text-sm font-semibold text-white rounded-xl transition flex items-center gap-2 ${
                  confirmLockModal.targetStatus
                    ? 'bg-emerald-600 hover:bg-emerald-700'
                    : 'bg-red-600 hover:bg-red-700'
                }`}
              >
                {updateStatusMutation.isPending && <Loader2 className="w-4 h-4 animate-spin" />}
                {confirmLockModal.targetStatus ? 'Xác nhận Mở khóa' : 'Xác nhận Khóa tài khoản'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* User Detail Modal */}
      {selectedUser && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="max-h-[90dvh] overflow-y-auto bg-white rounded-2xl max-w-lg w-full p-6 shadow-xl border border-slate-100 animate-in fade-in zoom-in-95 duration-150">
            <div className="flex items-start justify-between mb-5 pb-4 border-b border-slate-100">
              <div className="flex items-center gap-3">
                <div className="w-12 h-12 rounded-full bg-rose-500 text-white font-bold flex items-center justify-center text-lg shadow-sm">
                  {selectedUser.fullName.charAt(0).toUpperCase()}
                </div>
                <div>
                  <h3 className="text-lg font-bold text-slate-900">{selectedUser.fullName}</h3>
                  <div className="mt-0.5">{getRoleBadge(selectedUser.role)}</div>
                </div>
              </div>
              <button
                onClick={() => setSelectedUser(null)}
                className="text-slate-400 hover:text-slate-600 p-1 rounded-lg hover:bg-slate-100 transition"
              >
                ✕
              </button>
            </div>

            <div className="space-y-4 text-sm">
              <div className="grid grid-cols-2 gap-4 bg-slate-50 p-4 rounded-xl border border-slate-100">
                <div>
                  <p className="text-xs text-slate-500 font-medium">Email</p>
                  <p className="font-semibold text-slate-800 break-all">{selectedUser.email || 'Chưa có email liên hệ'}</p>
                  {selectedUser.loginName && <CopyLoginName value={selectedUser.loginName} />}
                </div>
                <div>
                  <p className="text-xs text-slate-500 font-medium">Số điện thoại</p>
                  <p className="font-semibold text-slate-800">{selectedUser.phoneNumber || 'Chưa có số điện thoại'}</p>
                </div>
                <div>
                  <p className="text-xs text-slate-500 font-medium">Trạng thái</p>
                  <p className={`font-semibold ${selectedUser.isActive ? 'text-emerald-600' : 'text-red-600'}`}>
                    {selectedUser.isActive ? 'Đang hoạt động' : 'Đã bị khóa'}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-slate-500 font-medium">Ngày đăng ký</p>
                  <p className="font-semibold text-slate-800">{new Date(selectedUser.createdAt).toLocaleDateString('vi-VN')}</p>
                </div>
              </div>

              {selectedUser.vendorBrandName && (
                <div className="bg-purple-50 border border-purple-100 p-4 rounded-xl flex items-center gap-3">
                  <Building2 className="w-8 h-8 text-purple-600 flex-shrink-0" />
                  <div>
                    <p className="text-xs font-semibold text-purple-600 uppercase tracking-wide">Thương hiệu đối tác</p>
                    <p className="text-base font-bold text-purple-900">{selectedUser.vendorBrandName}</p>
                    <p className="text-xs text-purple-700">Mã NCC: {selectedUser.vendorId}</p>
                  </div>
                </div>
              )}

              {selectedUser.role === 'VendorOwner' && selectedUser.vendorId && <VendorBusinessGroupsEditor key={selectedUser.vendorId} vendorId={selectedUser.vendorId} />}
              <div className="bg-slate-50 p-3 rounded-lg text-xs text-slate-500 space-y-1">
                <p><strong>User ID:</strong> <span className="font-mono">{selectedUser.id}</span></p>
                {selectedUser.lastLoginAt && (
                  <p><strong>Lần đăng nhập gần nhất:</strong> {new Date(selectedUser.lastLoginAt).toLocaleString('vi-VN')}</p>
                )}
              </div>
            </div>

            <div className="mt-6 flex justify-end gap-2">
              <button
                onClick={() => setSelectedUser(null)}
                className="px-4 py-2 text-sm font-medium text-slate-700 bg-slate-100 hover:bg-slate-200 rounded-xl transition"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
