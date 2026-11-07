import { useState, useEffect } from "react";
import { ArrowLeft, BookOpen, Check, Clock, Copy, Heart, Printer, Share2, Sparkles } from "lucide-react";
import "../../../styles/editorial-newspaper.css";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import axiosClient from "../../../services/axiosClient";
import { apiError, type Article } from "../api";
import {  ErrorText, Panel } from "../ui";
import { articleImageSrc } from "./articleCmsModel";
import { sanitizeArticleHtml } from "./articleHtml";

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
  const [copied, setCopied] = useState(false);
  const [fontSize, setFontSize] = useState<"sm" | "base" | "lg">("base");
  const [progress, setProgress] = useState(0);

  const query = useQuery({
    queryKey: ["article", slug],
    queryFn: async () => (await axiosClient.get<Article | null>(`/api/articles/${encodeURIComponent(slug ?? "")}`)).data,
    retry: 1,
  });

  // Track reading progress
  useEffect(() => {
    const handleScroll = () => {
      const total = document.documentElement.scrollHeight - window.innerHeight;
      if (total > 0) {
        setProgress(Math.min(100, Math.max(0, (window.scrollY / total) * 100)));
      }
    };
    window.addEventListener("scroll", handleScroll, { passive: true });
    return () => window.removeEventListener("scroll", handleScroll);
  }, []);

  const article: Article | null = query.data ?? null;

  if (query.isPending) {
    return (
      <main className="min-h-[60vh] flex items-center justify-center bg-[#faf8f5]">
        <div className="text-center p-8">
          <div className="w-10 h-10 border-3 border-rose-500 border-t-transparent rounded-full animate-spin mx-auto mb-3" />
          <p className="text-sm font-serif text-slate-600">Đang tải bài viết Blog cưới...</p>
        </div>
      </main>
    );
  }

  if (!article) {
    return (
      <main className="min-h-[60vh] flex items-center justify-center bg-[#faf8f5] px-4">
        <div className="max-w-md w-full bg-white p-8 rounded-2xl border border-rose-100 shadow-sm text-center">
          <BookOpen className="w-12 h-12 text-rose-300 mx-auto mb-3" />
          <h2 className="text-xl font-serif font-bold text-slate-800 mb-2">Không tìm thấy bài viết</h2>
          <p className="text-xs text-slate-500 mb-6">Bài viết có thể đã đổi đường dẫn, chưa được xuất bản hoặc đã được gỡ bỏ khỏi Blog.</p>
          <Link
            to="/blog"
            className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-rose-600 hover:bg-rose-700 text-white font-semibold text-xs transition shadow-xs"
          >
            <ArrowLeft size={14} />
            <span>Quay lại Blog</span>
          </Link>
        </div>
      </main>
    );
  }

  // Calculate read time & word count
  const plainText = (article.content || "").replace(/<[^>]+>/g, " ").replace(/\s+/g, " ").trim();
  const words = plainText ? plainText.split(/\s+/).length : 0;
  const readTime = Math.max(2, Math.ceil(words / 220));

  // Extract H2 headings for Table of Contents
  const h2Matches = [...article.content.matchAll(/<h2>(.*?)<\/h2>/g)].map((m, idx) => ({
    id: `section-${idx + 1}`,
    title: m[1].replace(/<[^>]+>/g, ""),
  }));

  const copyLink = () => {
    navigator.clipboard.writeText(window.location.href);
    setCopied(true);
    setTimeout(() => setCopied(false), 2500);
  };

  const fontSizeClass = fontSize === "sm" ? "text-[0.9375rem]" : fontSize === "lg" ? "text-[1.1875rem]" : "text-[1.0625rem]";

  let headingIndex = 0;
  const renderedContent = sanitizeArticleHtml(article.content.replace(/<h2>/g, () => `<h2 id="section-${++headingIndex}">`));

  return (
    <main className="bg-[#faf8f5] min-h-screen pb-20 relative">
      {/* Reading Progress Bar */}
      <div
        className="fixed top-0 left-0 h-1 bg-gradient-to-r from-rose-500 to-amber-500 z-50 transition-all duration-150"
        style={{ width: `${progress}%` }}
      />

      {/* Editorial Article Container */}
      <article className="max-w-4xl mx-auto px-4 sm:px-6 pt-8 md:pt-12">
        {/* Navigation Breadcrumb */}
        <div className="flex items-center justify-between gap-4 mb-6 text-xs text-slate-500">
          <Link
            to="/blog"
            className="inline-flex items-center gap-1.5 text-rose-700 hover:text-rose-800 font-semibold transition"
          >
            <ArrowLeft size={14} />
            <span>Blog cưới</span>
          </Link>
          <span className="text-slate-400">/</span>
          <span className="text-slate-600 font-medium truncate">{article.category}</span>
        </div>

        {/* Newspaper Headline Section */}
        <header className="mb-8">
          <div className="inline-block px-3.5 py-1 rounded-full text-xs font-bold uppercase tracking-wider bg-rose-100/70 text-rose-800 border border-rose-200/60 mb-4">
            {article.category}
          </div>

          <h1 className="text-3xl sm:text-4xl md:text-5xl font-serif font-bold text-slate-900 leading-[1.25] tracking-tight mb-6">
            {article.title}
          </h1>

          {/* Sapo / Excerpt */}
          {article.excerpt && (
            <div className="text-lg sm:text-xl font-serif text-slate-700 leading-relaxed italic bg-gradient-to-r from-rose-50/60 to-amber-50/30 p-5 rounded-2xl border-l-4 border-rose-400 mb-6 shadow-2xs">
              {article.excerpt}
            </div>
          )}

          {/* Editorial Byline & Tools Bar */}
          <div className="pt-4 border-t border-slate-200/80 flex flex-wrap items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-full bg-gradient-to-br from-rose-500 to-amber-600 text-white flex items-center justify-center font-serif font-bold text-sm shadow-xs">
                WM
              </div>
              <div>
                <div className="flex items-center gap-1.5">
                  <span className="text-xs font-bold text-slate-800">Ban Biên Tập Wedding Match</span>
                  <span className="text-[10px] bg-emerald-100 text-emerald-800 font-bold px-1.5 py-0.2 rounded-full">
                    Đã kiểm chứng
                  </span>
                </div>
                <div className="flex items-center gap-2 text-[11px] text-slate-500 mt-0.5">
                  <Clock size={12} />
                  <span>{readTime} phút đọc</span>
                  <span>·</span>
                  <span>{words.toLocaleString("vi-VN")} từ</span>
                </div>
              </div>
            </div>

            {/* Reading font & share buttons */}
            <div className="flex items-center gap-2">
              <div className="flex items-center bg-white rounded-lg border border-slate-200 p-0.5 shadow-2xs">
                <button
                  type="button"
                  onClick={() => setFontSize("sm")}
                  className={`px-2 py-1 text-xs font-semibold rounded ${fontSize === "sm" ? "bg-rose-50 text-rose-700" : "text-slate-600"}`}
                  title="Cỡ chữ nhỏ"
                >
                  A-
                </button>
                <button
                  type="button"
                  onClick={() => setFontSize("base")}
                  className={`px-2 py-1 text-xs font-semibold rounded ${fontSize === "base" ? "bg-rose-50 text-rose-700" : "text-slate-600"}`}
                  title="Cỡ chữ chuẩn"
                >
                  A
                </button>
                <button
                  type="button"
                  onClick={() => setFontSize("lg")}
                  className={`px-2 py-1 text-xs font-semibold rounded ${fontSize === "lg" ? "bg-rose-50 text-rose-700" : "text-slate-600"}`}
                  title="Cỡ chữ lớn"
                >
                  A+
                </button>
              </div>

              <button
                type="button"
                onClick={copyLink}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-white hover:bg-slate-50 text-slate-700 border border-slate-200 text-xs font-semibold transition shadow-2xs cursor-pointer"
                title="Sao chép liên kết"
              >
                {copied ? <Check size={14} className="text-emerald-600" /> : <Copy size={14} />}
                <span>{copied ? "Đã sao chép!" : "Chia sẻ"}</span>
              </button>

              <button
                type="button"
                onClick={() => window.print()}
                className="p-1.5 rounded-lg bg-white hover:bg-slate-50 text-slate-600 border border-slate-200 transition shadow-2xs"
                title="In bài viết"
              >
                <Printer size={15} />
              </button>
            </div>
          </div>
        </header>

        {/* Hero Cover Image */}
        {articleImageSrc(article.thumbnailUrl) && (
          <figure className="mb-10 text-center">
            <img
              src={articleImageSrc(article.thumbnailUrl)}
              alt={article.title}
              className="w-full max-h-[500px] object-cover rounded-2xl shadow-md border border-slate-200/80 mx-auto"
            />
            <figcaption className="text-xs text-slate-500 italic font-serif mt-2.5">
              Hình minh họa — Ấn phẩm chuyên đề cẩm nang cưới Wedding Match
            </figcaption>
          </figure>
        )}

        {/* Table of Contents for long articles */}
        {h2Matches.length > 2 && (
          <nav className="editorial-toc" aria-label="Mục lục bài viết">
            <div className="editorial-toc-title">
              <BookOpen size={16} className="text-rose-600" />
              <span>Nội dung chính trong bài</span>
            </div>
            <ul className="editorial-toc-list">
              {h2Matches.map((h, i) => (
                <li key={i}>
                  <a href={`#section-${i + 1}`}>
                    <span className="text-xs text-rose-400 font-mono">0{i + 1}.</span>
                    <span>{h.title}</span>
                  </a>
                </li>
              ))}
            </ul>
          </nav>
        )}

        {/* Rich Editorial Body Paper */}
        <div className="bg-white rounded-2xl border border-slate-200/90 p-6 sm:p-10 md:p-14 shadow-sm">
          {/* If content is rich HTML with tags, render HTML; else fallback to pre-wrap */}
          {article.content && (article.content.includes("<p>") || article.content.includes("<h2>") || article.content.includes("<table")) ? (
            <div
              className={`editorial-content ${fontSizeClass}`}
              dangerouslySetInnerHTML={{ __html: renderedContent }}
            />
          ) : (
            <div className={`editorial-content whitespace-pre-wrap leading-8 text-slate-700 ${fontSizeClass}`}>
              {article.content}
            </div>
          )}

          {/* Editorial Signature Sign-off */}
          <div className="mt-12 pt-8 border-t border-slate-200 flex items-center justify-between gap-4">
            <div className="flex items-center gap-2 text-xs font-serif text-slate-500 italic">
              <Sparkles size={14} className="text-rose-500" />
              <span>Wedding Match — Đồng hành cùng bạn chuẩn bị ngày vui trọn vẹn</span>
            </div>
            <button
              type="button"
              onClick={copyLink}
              className="text-xs font-semibold text-rose-600 hover:text-rose-700 flex items-center gap-1"
            >
              <Share2 size={13} />
              <span>Chia sẻ bài viết</span>
            </button>
          </div>
        </div>

        {/* Pinned Services Recommendation */}
        {article.listingIds && article.listingIds.length > 0 && (
          <section className="mt-12 p-6 rounded-2xl bg-white border border-rose-100 shadow-xs">
            <div className="flex items-center gap-2 mb-4">
              <Heart className="text-rose-500 fill-rose-500" size={18} />
              <h2 className="text-lg font-serif font-bold text-slate-900">Dịch vụ cưới đề xuất trong bài</h2>
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3">
              {article.listingIds.map((id) => (
                <Link
                  key={id}
                  to={`/listings/${id}`}
                  className="p-3 rounded-xl border border-slate-200 hover:border-rose-300 hover:bg-rose-50/30 transition flex items-center justify-between"
                >
                  <span className="text-xs font-semibold text-slate-800">Xem dịch vụ #{id.slice(0, 8)}</span>
                  <span className="text-xs text-rose-600 font-bold">→</span>
                </Link>
              ))}
            </div>
          </section>
        )}

        {/* Back to Blog Button */}
        <div className="mt-10 text-center">
          <Link
            to="/blog"
            className="inline-flex items-center gap-2 px-6 py-3 rounded-xl bg-white hover:bg-slate-50 text-slate-800 font-semibold text-xs transition border border-slate-200 shadow-xs"
          >
            <ArrowLeft size={14} />
            <span>Khám phá thêm các bài viết Blog khác</span>
          </Link>
        </div>
      </article>
    </main>
  );
}
