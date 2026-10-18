import { useRef, useState } from "react";
import type { InputHTMLAttributes } from "react";
import { Link, useLocation, useNavigate, useSearchParams } from "react-router-dom";
import { AlertCircle, ArrowRight, Building2, Eye, EyeOff, User } from "lucide-react";
import { useAuth } from "../hooks/useAuth";
import { safeReturnPath, useIntentStore } from "../../../store/intentStore";
import { EMPTY_REGISTRATION, normalizeRegistration, registrationApiError, validateRegistration } from "./registrationValidation";
import type { RegistrationErrors, RegistrationRole, RegistrationValues } from "./registrationValidation";
import ProvinceSelect from "./ProvinceSelect";
import "./register-page.css";

const inputClass = "w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm focus:outline-none focus:ring-2 focus:ring-[#c9a740] focus:bg-white transition";
const labelClass = "block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5";
type FieldProps = InputHTMLAttributes<HTMLInputElement> & { label: string; error?: string; hint?: string };

function RegistrationField({ id, label, error, hint, ...props }: FieldProps) {
  return <div>
    <label htmlFor={id} className={labelClass}>{label}</label>
    <input {...props} id={id} className={inputClass} aria-invalid={error ? true : undefined} aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined} />
    {hint && <p id={`${id}-hint`} className="mt-1.5 text-xs leading-relaxed text-slate-500">{hint}</p>}
    {error && <p id={`${id}-error`} className="mt-1.5 text-xs text-red-700">{error}</p>}
  </div>;
}

function PasswordField({ id, label, error, hint, ...props }: FieldProps) {
  const [visible, setVisible] = useState(false);
  return <div>
    <div className="flex items-center justify-between gap-2 mb-1.5">
      <label htmlFor={id} className={labelClass}>{label}</label>
      <button type="button" onClick={() => setVisible((value) => !value)} className="flex shrink-0 items-center gap-1.5 text-xs text-slate-600 hover:text-slate-900" aria-label={`${visible ? "Ẩn" : "Hiện"} ${label.toLowerCase()}`} aria-pressed={visible} aria-controls={id}>
        {visible ? <EyeOff className="h-4 w-4" aria-hidden="true" /> : <Eye className="h-4 w-4" aria-hidden="true" />}{visible ? "Ẩn" : "Hiện"}
      </button>
    </div>
    <input {...props} id={id} type={visible ? "text" : "password"} className={inputClass} aria-invalid={error ? true : undefined} aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined} />
    {hint && <p id={`${id}-hint`} className="mt-1.5 text-xs leading-relaxed text-slate-500">{hint}</p>}
    {error && <p id={`${id}-error`} className="mt-1.5 text-xs text-red-700">{error}</p>}
  </div>;
}

export default function RegisterForm() {
  const [params, setParams] = useSearchParams();
  const role: RegistrationRole = params.get("role") === "vendor" ? "vendor" : "customer";
  const [customer, setCustomer] = useState<RegistrationValues>({ ...EMPTY_REGISTRATION });
  const [vendor, setVendor] = useState<RegistrationValues>({ ...EMPTY_REGISTRATION });
  const [errors, setErrors] = useState<RegistrationErrors>({});
  const [errorMessage, setErrorMessage] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const inFlight = useRef(false);
  const formRef = useRef<HTMLFormElement>(null);
  const { registerCustomer, registerVendor, isRegisteringCustomer, isRegisteringVendor } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const values = role === "customer" ? customer : vendor;
  const isLoading = submitting || isRegisteringCustomer || isRegisteringVendor;
  const prefix = `register-${role}`;

  const update = (field: keyof RegistrationValues, value: string) => {
    const setValues = role === "customer" ? setCustomer : setVendor;
    setValues((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: undefined }));
    setErrorMessage("");
  };
  const chooseRole = (next: RegistrationRole) => {
    if (inFlight.current) return;
    const nextParams = new URLSearchParams(params);
    nextParams.set("role", next);
    setParams(nextParams, { replace: true, state: location.state });
    setErrors({}); setErrorMessage("");
  };
  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (inFlight.current || isLoading) return;
    setErrorMessage("");
    const nextErrors = validateRegistration(values, role);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length) {
      requestAnimationFrame(() => formRef.current?.querySelector<HTMLInputElement>("[aria-invalid='true']")?.focus());
      return;
    }
    inFlight.current = true;
    setSubmitting(true);
    const fields = normalizeRegistration(values);
    const credentials = { fullName: fields.fullName, email: fields.email, phoneNumber: fields.phoneNumber, password: fields.password };
    try {
      if (role === "customer") await registerCustomer(credentials);
      else await registerVendor({ ...credentials, brandName: fields.brandName, city: fields.city, commissionRate: Number(fields.commissionRate) });
      const returnTo = safeReturnPath((location.state as { from?: string } | null)?.from) ?? safeReturnPath(useIntentStore.getState().returnTo);
      navigate("/auth/continue", { replace: true, state: { from: returnTo } });
    } catch (error: unknown) {
      setErrorMessage(registrationApiError(error));
    } finally {
      inFlight.current = false;
      setSubmitting(false);
    }
  };

  return <main className="auth-page auth-register">
    <div className="auth-story">
      <span className="wm-eyebrow">MỘT KHỞI ĐẦU THẬT ĐẸP</span>
      <h1>Ngày hạnh phúc,<br /><em>mình cùng lên plan.</em></h1>
      <p>Tạo tài khoản và gom checklist, ngân sách, thiệp cưới cùng những dịch vụ yêu thích vào một nơi.</p>
      <img src="/wedding-match/milo.png" alt="Milo chào mừng hai bạn" />
    </div>
    <div className="auth-card">
      <div className="text-center mb-6">
        <h2>Bắt đầu cùng Wedding Match</h2>
        <p className="text-xs text-slate-500 mt-3">Chọn loại tài khoản phù hợp với bạn</p>
      </div>
      <div role="group" aria-label="Loại tài khoản đăng ký" className="flex bg-slate-100 p-1.5 rounded-2xl mb-5">
        <button type="button" aria-pressed={role === "customer"} disabled={isLoading} onClick={() => chooseRole("customer")} className={`flex-1 py-2.5 px-2 rounded-xl text-sm font-semibold flex items-center justify-center gap-2 transition disabled:opacity-60 ${role === "customer" ? "bg-white text-[#25334c] shadow-sm" : "text-slate-600 hover:text-slate-900"}`}>
          <User className="w-4 h-4 shrink-0" aria-hidden="true" /><span>Cô dâu / Chú rể</span>
        </button>
        <button type="button" aria-pressed={role === "vendor"} disabled={isLoading} onClick={() => chooseRole("vendor")} className={`flex-1 py-2.5 px-2 rounded-xl text-sm font-semibold flex items-center justify-center gap-2 transition disabled:opacity-60 ${role === "vendor" ? "bg-white text-[#25334c] shadow-sm" : "text-slate-600 hover:text-slate-900"}`}>
          <Building2 className="w-4 h-4 shrink-0" aria-hidden="true" /><span>Nhà cung cấp</span>
        </button>
      </div>
      <p className="wm-register-context">{role === "customer" ? "Sau khi đăng ký, hai bạn sẽ trả lời 10 câu hỏi ngắn để khởi tạo checklist và ngân sách, rồi đến trang tổng quan." : "Sau khi đăng ký, bạn đến cổng nhà cung cấp để hoàn thiện hồ sơ thương hiệu và gửi dịch vụ xét duyệt."}</p>
      {errorMessage && <div role="alert" className="mb-5 p-4 rounded-xl bg-red-50 border border-red-200 flex items-start gap-3 text-red-700 text-sm">
        <AlertCircle className="w-5 h-5 shrink-0 mt-0.5" aria-hidden="true" /><span>{errorMessage}</span>
      </div>}
      <form ref={formRef} onSubmit={handleSubmit} noValidate aria-busy={isLoading} className="space-y-4">
        <fieldset disabled={isLoading} className="space-y-4 min-w-0">
          <legend className="sr-only">Thông tin đăng ký {role === "vendor" ? "nhà cung cấp" : "khách hàng"}</legend>
          {role === "vendor" && <>
            <p className="p-3 bg-[#f7f3e5] rounded-xl border border-[#e6d9af] text-[#66552d] text-xs leading-relaxed">Tài khoản này là chủ quản lý thương hiệu. Hồ sơ và dịch vụ cần được kiểm duyệt trước khi hiển thị cho khách hàng.</p>
            <RegistrationField id={`${prefix}-brand`} name="organization" label="Tên thương hiệu" autoComplete="organization" required maxLength={200} value={values.brandName} onChange={(event) => update("brandName", event.target.value)} error={errors.brandName} placeholder="La Rose Bridal" />
          </>}
          <RegistrationField id={`${prefix}-name`} name="fullName" label={role === "vendor" ? "Họ và tên người đại diện" : "Họ và tên"} autoComplete="name" required maxLength={150} value={values.fullName} onChange={(event) => update("fullName", event.target.value)} error={errors.fullName} placeholder="Nguyễn Văn A" />
          <RegistrationField id={`${prefix}-email`} name="email" label="Email đăng nhập" type="email" autoComplete="email" inputMode="email" autoCapitalize="none" spellCheck={false} required maxLength={150} value={values.email} onChange={(event) => update("email", event.target.value)} error={errors.email} placeholder="ban@example.com" />
          <RegistrationField id={`${prefix}-phone`} name="phoneNumber" label={role === "vendor" ? "Hotline nhà cung cấp" : "Số điện thoại"} type="tel" autoComplete="tel-national" inputMode="tel" required maxLength={20} value={values.phoneNumber} onChange={(event) => update("phoneNumber", event.target.value)} error={errors.phoneNumber} placeholder="0912345678" />
          {role === "vendor" && <>
            <ProvinceSelect key={`${role}-city`} id={`${prefix}-city`} value={values.city} onChange={(value) => update("city", value)} error={errors.city} disabled={isLoading} />
            <div>
              <label htmlFor={`${prefix}-commission`} className={labelClass}>Nhóm dịch vụ / hoa hồng</label>
              <select id={`${prefix}-commission`} name="commissionRate" value={values.commissionRate} onChange={(event) => update("commissionRate", event.target.value)} className={inputClass} aria-invalid={errors.commissionRate ? true : undefined} aria-describedby={errors.commissionRate ? `${prefix}-commission-error` : undefined}>
                <option value="0.03">Tiệc cưới: 3%</option><option value="0.08">Trang trí / Thiệp: 8%</option><option value="0.1">Quay chụp / Váy / Makeup: 10%</option><option value="0.12">Wedding Planner: 12%</option>
              </select>
              {errors.commissionRate && <p id={`${prefix}-commission-error`} className="mt-1.5 text-xs text-red-700">{errors.commissionRate}</p>}
            </div>
          </>}
          <PasswordField key={`${role}-password`} id={`${prefix}-password`} name="password" label="Mật khẩu" autoComplete="new-password" required minLength={8} maxLength={72} value={values.password} onChange={(event) => update("password", event.target.value)} error={errors.password} hint="Ít nhất 8 ký tự. Bạn có thể dùng một cụm từ dễ nhớ; tối đa 72 byte." />
          <PasswordField key={`${role}-confirmation`} id={`${prefix}-confirmation`} name="confirmPassword" label="Nhập lại mật khẩu" autoComplete="new-password" required minLength={8} maxLength={72} value={values.confirmPassword} onChange={(event) => update("confirmPassword", event.target.value)} error={errors.confirmPassword} />
          <button type="submit" disabled={isLoading} className="w-full mt-2 py-3 px-4 rounded-xl bg-[#25334c] text-white font-semibold text-sm hover:bg-[#354762] transition disabled:opacity-50 flex items-center justify-center gap-2">
            <span>{isLoading ? "Đang tạo tài khoản..." : role === "vendor" ? "Tạo tài khoản nhà cung cấp" : "Tạo tài khoản khách hàng"}</span><ArrowRight className="w-4 h-4" aria-hidden="true" />
          </button>
        </fieldset>
      </form>
      <p className="mt-5 text-xs leading-relaxed text-slate-500">Tài khoản quản trị, kiểm duyệt, tài chính và chăm sóc khách hàng được quản trị viên cấp nội bộ, không đăng ký tại đây.</p>
      <div className="mt-6 text-center text-sm text-slate-500">Đã có tài khoản?{" "}<Link to="/login" state={location.state} className="font-semibold text-[#25334c] hover:underline">Đăng nhập</Link></div>
    </div>
  </main>;
}
