import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import axiosClient from "../../../services/axiosClient";
import { apiError, type Article } from "../api";
import { Empty, ErrorText, Panel } from "../ui";
import { articleImageSrc } from "./articleCmsModel";

interface TaxProfile { legalName: string; taxNumber: string; bankCode: string; bankAccountNumber: string; bankAccountName: string; agreementAccepted: boolean; status: string }
export function VendorTaxProfileForm() {
  const qc = useQueryClient();
  const query = useQuery({ queryKey: ["tax-profile"], queryFn: async () => (await axiosClient.get<TaxProfile | null>("/api/vendor/tax-profile")).data });
  const save = useMutation({ mutationFn: (body: Omit<TaxProfile, "status">) => axiosClient.put("/api/vendor/tax-profile", body), onSuccess: () => qc.invalidateQueries() });
  if (query.isPending) return <p>Đang tải hồ sơ pháp lý...</p>;
  const p = query.data;
  return <Panel eyebrow="Thông tin pháp lý" title={`Pháp nhân & ngân hàng · ${p?.status ?? "Draft"}`}>
    <p className="text-sm muted mb-3">Đây là tài khoản nhận cọc lịch hẹn trực tiếp của NCC. Moderator cần đối chiếu chứng từ; hệ thống chưa có dịch vụ tự xác thực chủ tài khoản.</p>
    <ErrorText>{query.isError ? apiError(query.error) : save.isError ? apiError(save.error) : ""}</ErrorText>
    <form key={`${p?.status}:${p?.bankAccountNumber}`} className="grid md:grid-cols-2 gap-3" onSubmit={(e) => { e.preventDefault(); const d = new FormData(e.currentTarget); save.mutate({ legalName: String(d.get("legalName")), taxNumber: String(d.get("taxNumber")), bankCode: String(d.get("bankCode")), bankAccountNumber: String(d.get("bankAccountNumber")), bankAccountName: String(d.get("bankAccountName")), agreementAccepted: d.get("agreementAccepted") === "on" }); }}>
      {([ ["legalName", "Tên pháp nhân"], ["taxNumber", "Mã số thuế"], ["bankCode", "Mã ngân hàng (VietQR/BIN)"], ["bankAccountNumber", "Số tài khoản"], ["bankAccountName", "Tên chủ tài khoản"] ] as const).map(([name, label]) => <label key={name}><span className="label">{label}</span><input className="input" required name={name} defaultValue={p?.[name] ?? ""} disabled={p?.status === "Submitted"} /></label>)}
      <label className="flex gap-2 items-center text-sm"><input type="checkbox" name="agreementAccepted" required defaultChecked={p?.agreementAccepted} disabled={p?.status === "Submitted"} /> Đồng ý thỏa thuận đối tác; xác nhận thông tin chính xác.</label>
      <div className="md:col-span-2"><button className="btn btn-primary" disabled={save.isPending || p?.status === "Submitted"}>Lưu hồ sơ {p?.status === "Approved" ? "(cần xác minh lại)" : ""}</button>{save.isSuccess && <span role="status" className="ml-3 text-sm">Đã lưu. Tiếp tục nộp giấy tờ bên dưới.</span>}</div>
    </form>
  </Panel>;
}
export { ArticleCms } from './ArticleCms';
export function EmergencyMatchingPanel() {
  const mutation = useMutation({ mutationFn: (body: unknown) => axiosClient.post("/api/admin/emergency-matches", body) });
  return <Panel title="Tìm NCC thay thế khẩn cấp" eyebrow="CSKH"><p className="text-sm muted mb-3">Chỉ áp dụng HĐ đang tranh chấp. Tạo lead mới cho NCC thay thế; khách vẫn phải duyệt báo giá/HĐ mới. Không chuyển tiền hay xóa HĐ cũ.</p><ErrorText>{mutation.isError ? apiError(mutation.error) : ""}</ErrorText><form className="grid gap-3" onSubmit={(e) => { e.preventDefault(); const d = new FormData(e.currentTarget); mutation.mutate(Object.fromEntries(d)); }}><input required name="contractId" className="input" placeholder="ID hợp đồng đang tranh chấp" aria-label="ID hợp đồng" /><input required name="replacementListingId" className="input" placeholder="ID dịch vụ NCC thay thế đã xác minh" aria-label="ID dịch vụ thay thế" /><textarea required minLength={10} name="reason" className="input" placeholder="Lý do và thỏa thuận hỗ trợ" aria-label="Lý do hỗ trợ" /><button className="btn btn-primary" disabled={mutation.isPending}>Tạo yêu cầu thay thế</button>{mutation.isSuccess && <p role="status">Đã tạo lead mới và thông báo cho khách.</p>}</form></Panel>;
}
export function ArticlePage() {
  const { slug } = useParams();
  const query = useQuery({ queryKey: ["article", slug], queryFn: async () => (await axiosClient.get<Article | null>(`/api/articles/${encodeURIComponent(slug ?? "")}`)).data });
  if (query.isPending) return <main className="wd wd-page container">Đang tải bài viết...</main>;
  if (query.isError || !query.data) return <main className="wd wd-page container"><Empty>Không tìm thấy bài viết.</Empty><Link to="/wiki">Về cẩm nang</Link></main>;
  const article = query.data;
  return <main className="wd wd-page"><article className="container max-w-3xl"><Link to="/wiki">← Cẩm nang cưới</Link><p className="eyebrow mt-6">{article.category}</p><h1 className="serif text-4xl font-bold mt-2">{article.title}</h1><p className="text-lg muted my-5">{article.excerpt}</p>{articleImageSrc(article.thumbnailUrl) && <img className="rounded-2xl w-full mb-6" src={articleImageSrc(article.thumbnailUrl)} alt="" />}<div className="whitespace-pre-wrap leading-8">{article.content}</div>{article.listingIds && article.listingIds.length > 0 && <section className="mt-8"><h2 className="font-bold">Dịch vụ liên quan</h2>{article.listingIds.map((id) => <Link className="block underline mt-2" key={id} to={`/listings/${id}`}>Khám phá dịch vụ {id.slice(0, 8)} →</Link>)}</section>}</article></main>;
}
