import type { ServiceDetailsDto } from "../../../types/listing.types";
import { emptyFullServiceDetails, FULL_SERVICE_FIELDS, SERVICE_DETAIL_SECTIONS } from "../../listings/serviceDetailsModel";

export function ServiceDetailsEditor({ value, onChange, fullServiceEnabled, disabled, errors }: {
  value: ServiceDetailsDto;
  onChange: (value: ServiceDetailsDto) => void;
  fullServiceEnabled: boolean;
  disabled: boolean;
  errors: Record<string, string>;
}) {
  const inputClass = "mt-1.5 w-full min-w-0 rounded-xl border border-[#dcd8ca] bg-white px-3 py-2.5 text-sm text-[#25334c] focus:outline-2 focus:outline-[#c9a740] disabled:opacity-60";
  const error = (key: string) => errors[`serviceDetails.${key}`];
  return <fieldset disabled={disabled} className="min-w-0 space-y-3">
    <legend className="mb-2 text-base font-semibold text-[#25334c]">2. Nội dung dịch vụ bổ sung</legend>
    <p className="text-xs leading-relaxed text-slate-500">Thông tin dưới đây giúp khách hiểu phạm vi phục vụ. Đây là nội dung mô tả, không tự sửa giá giao dịch, chính sách hủy, báo giá, hợp đồng hay cơ sở tính hoa hồng.</p>
    {errors.serviceDetails && <p role="alert" className="text-xs text-red-700">{errors.serviceDetails}</p>}
    {SERVICE_DETAIL_SECTIONS.map((section, index) => <details key={section.title} className="rounded-2xl border border-[#e4dfcf] bg-[#faf8f0] p-4" open={index === 0 || section.fields.some(field => !!error(field.key))}>
      <summary className="cursor-pointer text-sm font-semibold text-[#25334c]">{section.title}</summary>
      <div className="mt-4 grid min-w-0 gap-4 sm:grid-cols-2">{section.fields.map(field => <div key={field.key} className={field.key === "priceUnit" ? "min-w-0" : "min-w-0 sm:col-span-2"}>
        <label htmlFor={`listing-details-${field.key}`} className="block text-xs font-semibold text-slate-700">{field.label}</label>
        {field.key === "priceUnit" ? <input id={`listing-details-${field.key}`} type="text" className={inputClass} maxLength={field.maxLength} value={value[field.key]} placeholder="Ví dụ: theo gói / bàn / giờ" onChange={event => onChange({ ...value, [field.key]: event.target.value })} aria-invalid={!!error(field.key) || undefined} aria-describedby={error(field.key) ? `listing-details-${field.key}-error` : undefined} /> : <textarea id={`listing-details-${field.key}`} className={inputClass} rows={field.key === "shortDescription" ? 2 : 3} maxLength={field.maxLength} value={value[field.key]} onChange={event => onChange({ ...value, [field.key]: event.target.value })} aria-invalid={!!error(field.key) || undefined} aria-describedby={error(field.key) ? `listing-details-${field.key}-error` : undefined} />}
        <div className="mt-1 flex flex-wrap justify-between gap-2 text-[11px] text-slate-500">{error(field.key) ? <p id={`listing-details-${field.key}-error`} className="text-red-700">{error(field.key)}</p> : <span>Tùy chọn</span>}<span>{value[field.key].length}/{field.maxLength}</span></div>
      </div>)}</div>
    </details>)}
    <details className="rounded-2xl border border-[#e4dfcf] bg-[#faf8f0] p-4" open={!!error("portfolioUrls") || !!error("videoUrl")}>
      <summary className="cursor-pointer text-sm font-semibold text-[#25334c]">Portfolio & video giới thiệu</summary>
      <div className="mt-4 space-y-4">
        <div><label htmlFor="listing-details-portfolio" className="block text-xs font-semibold text-slate-700">Liên kết portfolio / dự án đã thực hiện</label><textarea id="listing-details-portfolio" className={inputClass} rows={3} maxLength={11000} value={value.portfolioUrls.join("\n")} onChange={event => onChange({ ...value, portfolioUrls: event.target.value.split(/\r?\n/) })} placeholder="Mỗi dòng một liên kết HTTP(S), tối đa 10 liên kết." aria-invalid={!!error("portfolioUrls") || undefined} aria-describedby="listing-details-portfolio-hint" /><p id="listing-details-portfolio-hint" className={`mt-1 text-xs leading-relaxed ${error("portfolioUrls") ? "text-red-700" : "text-slate-500"}`}>{error("portfolioUrls") || "Không nhập mã nhúng, đường dẫn thiết bị hoặc liên kết có tài khoản/mật khẩu. Khách mở liên kết theo lựa chọn; hệ thống không tự tải nội dung."}</p></div>
        <div><label htmlFor="listing-details-video" className="block text-xs font-semibold text-slate-700">Liên kết video giới thiệu</label><input id="listing-details-video" className={inputClass} type="url" maxLength={1000} value={value.videoUrl ?? ""} onChange={event => onChange({ ...value, videoUrl: event.target.value })} placeholder="https://..." aria-invalid={!!error("videoUrl") || undefined} aria-describedby="listing-details-video-hint" /><p id="listing-details-video-hint" className={`mt-1 text-xs leading-relaxed ${error("videoUrl") ? "text-red-700" : "text-slate-500"}`}>{error("videoUrl") || "Hiển thị dưới dạng liên kết an toàn, không tự phát hoặc nhúng video."}</p></div>
      </div>
    </details>
    {(fullServiceEnabled || value.fullService !== null) && <details className="rounded-2xl border border-[#cfc197] bg-[#f7f3e7] p-4" open={FULL_SERVICE_FIELDS.some(field => !!error(`fullService.${field.key}`))}>
      <summary className="cursor-pointer text-sm font-semibold text-[#25334c]">Tổ chức cưới trọn gói A–Z</summary>
      <p className="mt-3 text-xs leading-relaxed text-slate-600">Đơn vị tổ chức chịu trách nhiệm quản lý, phối hợp và điều hành, không đơn thuần là combo nhiều dịch vụ. Mô tả phí ở đây không thay thế việc Admin phê duyệt cơ sở tính hoa hồng; không tự tính trên ngân sách cưới hoặc chi hộ.</p>
      <div className="mt-4 grid gap-4">{FULL_SERVICE_FIELDS.map(field => <div key={field.key} className="min-w-0"><label htmlFor={`listing-details-full-${field.key}`} className="block text-xs font-semibold text-slate-700">{field.label}</label><textarea id={`listing-details-full-${field.key}`} className={inputClass} rows={3} maxLength={4000} value={value.fullService?.[field.key] ?? ""} onChange={event => onChange({ ...value, fullService: { ...emptyFullServiceDetails(), ...value.fullService, [field.key]: event.target.value } })} aria-invalid={!!error(`fullService.${field.key}`) || undefined} aria-describedby={error(`fullService.${field.key}`) ? `listing-details-full-${field.key}-error` : undefined} />{error(`fullService.${field.key}`) && <p id={`listing-details-full-${field.key}-error`} className="mt-1 text-xs text-red-700">{error(`fullService.${field.key}`)}</p>}</div>)}</div>
    </details>}
  </fieldset>;
}
