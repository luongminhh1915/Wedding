import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { ImagePlus, Loader2, PencilLine, Plus, Trash2 } from "lucide-react";
import axiosClient from "../../../services/axiosClient";
import { useAuthStore } from "../../../store/authStore";
import { apiError, type Article } from "../api";
import { ErrorText, Panel } from "../ui";
import { ArticleCoverPicker } from "./ArticleCoverPicker";
import { articleImageSrc, articleSlug, isCurrentCmsActor } from "./articleCmsModel";
import type { CmsActor } from "./articleCmsModel";

type ArticleDraft = { title: string; slug: string; excerpt: string; content: string; thumbnailUrl: string; category: string; publish: boolean; listingIds: string };
type ArticleBody = Omit<ArticleDraft, "thumbnailUrl" | "listingIds"> & { thumbnailUrl: string | null; listingIds?: string[] };
const sessionMessage = "Phiên biên tập đã thay đổi. Vui lòng mở lại cẩm nang.";
const errorMessage = (error: unknown) => error instanceof Error && !("response" in error) ? error.message : apiError(error);

export function ArticleEditorForm({ article, actor, onSaved, onNew, onBusyChange }: {
  article: Article | null; actor: CmsActor; onSaved: (title: string, published: boolean) => void;
  onNew: () => void; onBusyChange: (busy: boolean) => void;
}) {
  const [draft, setDraft] = useState<ArticleDraft>(() => ({
    title: article?.title ?? "", slug: article?.slug ?? "", excerpt: article?.excerpt ?? "", content: article?.content ?? "",
    thumbnailUrl: article?.thumbnailUrl ?? "", category: article?.category ?? "Cẩm nang cưới",
    publish: article?.isPublished ?? false, listingIds: article?.listingIds?.join(", ") ?? "",
  }));
  const [manualSlug, setManualSlug] = useState(!!article);
  const [uploading, setUploading] = useState(false);
  const [failure, setFailure] = useState("");
  const uploadingRef = useRef(false);
  const savingRef = useRef(false);
  const mounted = useRef(true);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  const current = () => mounted.current && isCurrentCmsActor(actor, useAuthStore.getState());
  const save = useMutation({
    gcTime: 0,
    mutationFn: async (body: ArticleBody) => {
      if (!current()) throw new Error(sessionMessage);
      return article ? axiosClient.put(`/api/admin/articles/${article.id}`, body, { headers: { Authorization: `Bearer ${actor.token}` } })
        : axiosClient.post("/api/admin/articles", body, { headers: { Authorization: `Bearer ${actor.token}` } });
    },
  });
  const busy = uploading || save.isPending;
  const change = <K extends keyof ArticleDraft>(key: K, value: ArticleDraft[K]) => {
    if (savingRef.current) return;
    setFailure("");
    setDraft(previous => ({ ...previous, [key]: value,
      ...(key === "title" && !manualSlug ? { slug: articleSlug(String(value)) } : {}),
    }));
  };
  const uploadingChanged = (value: boolean) => {
    uploadingRef.current = value; setUploading(value); onBusyChange(value || savingRef.current);
  };
  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (uploadingRef.current || savingRef.current) return;
    if (!current()) { setFailure(sessionMessage); return; }
    if (!draft.title.trim() || !draft.content.trim() || !draft.category.trim()) { setFailure("Nhập tiêu đề, danh mục và nội dung bài viết."); return; }
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(draft.slug.trim())) { setFailure("Đường dẫn chỉ gồm chữ thường không dấu, số và dấu gạch ngang."); return; }
    const ids = [...new Set(draft.listingIds.split(/[\s,]+/).filter(Boolean))];
    if (article && (ids.length > 10 || ids.some(id => !/^[a-f0-9]{8}-(?:[a-f0-9]{4}-){3}[a-f0-9]{12}$/i.test(id)))) {
      setFailure("Chỉ ghim tối đa 10 ID dịch vụ hợp lệ."); return;
    }
    const body: ArticleBody = { title: draft.title.trim(), slug: draft.slug.trim(), excerpt: draft.excerpt.trim(), content: draft.content,
      thumbnailUrl: draft.thumbnailUrl || null, category: draft.category.trim(), publish: draft.publish,
      ...(article ? { listingIds: ids } : {}),
    };
    setFailure(""); savingRef.current = true; onBusyChange(true);
    try {
      await save.mutateAsync(body);
      if (current()) onSaved(body.title, body.publish);
    } catch (error: unknown) { if (current()) setFailure(errorMessage(error)); }
    finally { savingRef.current = false; if (mounted.current) { onBusyChange(uploadingRef.current); save.reset(); } }
  };
  return <form className="grid min-w-0 gap-4" aria-label={article ? "Chỉnh sửa bài viết" : "Bài viết mới"} aria-busy={busy} onSubmit={event => void submit(event)}>
    <div><h3 className="text-lg font-bold text-[#233a42]">{article ? "Chỉnh sửa bài viết" : "Bài viết mới"}</h3><p className="mt-1 text-sm text-slate-600">Lưu bản nháp để biên tập tiếp, hoặc bật Xuất bản để bài viết xuất hiện trong Cẩm nang.</p></div>
    <ErrorText>{failure}</ErrorText>
    <fieldset disabled={save.isPending} className="grid min-w-0 gap-4">
      <label><span className="label">Tiêu đề</span><input className="input" name="title" required maxLength={300} value={draft.title} onChange={event => change("title", event.target.value)} /></label>
      <div><label><span className="label">Đường dẫn bài viết</span><input className="input" name="slug" required maxLength={300} value={draft.slug} onChange={event => { setManualSlug(true); change("slug", event.target.value); }} aria-describedby="article-slug-help" /></label>
        <div className="mt-2 flex flex-wrap items-center justify-between gap-2"><p id="article-slug-help" className="text-xs text-slate-500 [overflow-wrap:anywhere]">Tự tạo từ tiêu đề; bạn có thể chỉnh lại. /wiki/{draft.slug || "ten-bai-viet"}</p><button type="button" className="text-xs font-semibold underline text-[#8d4e47]" onClick={() => { setManualSlug(false); change("slug", articleSlug(draft.title)); }}>Gợi ý từ tiêu đề</button></div>
      </div>
      <label><span className="label">Danh mục</span><input className="input" name="category" required maxLength={100} value={draft.category} onChange={event => change("category", event.target.value)} /></label>
      <ArticleCoverPicker value={draft.thumbnailUrl} actor={actor} onChange={value => change("thumbnailUrl", value)} onUploadingChange={uploadingChanged} disabled={save.isPending} />
      <label><span className="label">Tóm tắt</span><textarea className="input" name="excerpt" rows={3} maxLength={1000} value={draft.excerpt} onChange={event => change("excerpt", event.target.value)} /></label>
      <label><span className="label">Nội dung bài viết</span><textarea className="input" name="content" rows={12} required value={draft.content} onChange={event => change("content", event.target.value)} /><span className="mt-1 block text-xs text-slate-500">Viết nội dung dạng văn bản; xuống dòng để chia đoạn.</span></label>
      {article && <details className="rounded-xl border border-[#eadbd4] p-3"><summary className="cursor-pointer text-sm font-semibold">Dịch vụ liên quan (không bắt buộc)</summary><label className="mt-3 block"><span className="label">ID dịch vụ ghim, tối đa 10</span><input className="input" name="listingIds" value={draft.listingIds} onChange={event => change("listingIds", event.target.value)} /><span className="mt-1 block text-xs text-slate-500">Cách nhau bằng dấu phẩy; chỉ dịch vụ đang hoạt động mới được ghim.</span></label></details>}
      <label className="flex items-center gap-2 rounded-xl bg-[#f6e7dd] p-3 text-sm font-semibold text-[#233a42]"><input name="publish" type="checkbox" checked={draft.publish} onChange={event => change("publish", event.target.checked)} />Xuất bản bài viết</label>
    </fieldset>
    <div className="flex flex-wrap gap-3"><button type="submit" className="btn btn-primary" disabled={busy}>{save.isPending && <Loader2 size={16} className="animate-spin" aria-hidden="true" />}{save.isPending ? "Đang lưu..." : draft.publish ? "Lưu & xuất bản" : "Lưu bản nháp"}</button><button type="button" className="btn btn-outline" disabled={busy} onClick={onNew}>Bài mới</button></div>
  </form>;
}

export function ArticleCms() {
  const id = useAuthStore(state => state.user?.id);
  const role = useAuthStore(state => state.user?.role);
  const revision = useAuthStore(state => state.sessionRevision);
  if (!id || (role !== "SuperAdmin" && role !== "Moderator")) return <ErrorText>Chỉ Admin và kiểm duyệt viên được quản lý cẩm nang.</ErrorText>;
  return <ArticleCmsWorkspace key={`${id}:${role}:${revision}`} />;
}

function ArticleCmsWorkspace() {
  const qc = useQueryClient();
  const [actor] = useState<CmsActor>(() => { const session = useAuthStore.getState(); return { id: session.user?.id, role: session.user?.role, token: session.token, revision: session.sessionRevision }; });
  const [editing, setEditing] = useState<Article | null>(null);
  const [version, setVersion] = useState(0);
  const [editorBusy, setEditorBusy] = useState(false);
  const [success, setSuccess] = useState("");
  const [deleteError, setDeleteError] = useState("");
  const deleteInFlight = useRef(false);
  const mounted = useRef(true);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  const current = () => mounted.current && isCurrentCmsActor(actor, useAuthStore.getState());
  const readArticles = async (path: string, signal: AbortSignal) => {
    if (!current()) throw new Error(sessionMessage);
    const result = await axiosClient.get<Article[]>(path, { signal, headers: { Authorization: `Bearer ${actor.token}` } });
    if (!current()) throw new Error(sessionMessage);
    return result.data;
  };
  const query = useQuery({ queryKey: ["cms-articles", actor.id, actor.revision], queryFn: ({ signal }) => readArticles("/api/admin/articles", signal) });
  const detail = useQuery({ queryKey: ["cms-detail", actor.id, actor.revision, editing?.id, version], enabled: !!editing,
    queryFn: async ({ signal }) => { const result = (await readArticles(`/api/admin/articles?slug=${encodeURIComponent(editing!.slug)}`, signal))[0]; if (!result || result.id !== editing!.id) throw new Error("Bài viết không còn tồn tại. Vui lòng tải lại danh sách."); return result; },
  });
  const refresh = () => { void qc.invalidateQueries({ queryKey: ["cms-articles"] }); void qc.invalidateQueries({ queryKey: ["articles"] }); void qc.invalidateQueries({ queryKey: ["article"] }); };
  const remove = useMutation({ gcTime: 0, mutationFn: async (id: string) => {
    if (!current()) throw new Error(sessionMessage);
    return axiosClient.delete(`/api/admin/articles/${id}`, { headers: { Authorization: `Bearer ${actor.token}` } });
  } });
  const newArticle = () => { if (editorBusy || deleteInFlight.current) return; setEditing(null); setVersion(value => value + 1); setSuccess(""); setDeleteError(""); };
  const deleteArticle = async (article: Article) => {
    if (editorBusy || deleteInFlight.current) return;
    if (!current()) { setDeleteError(sessionMessage); return; }
    if (!window.confirm(`Xóa bài “${article.title}”?`)) return;
    deleteInFlight.current = true; setDeleteError("");
    try {
      await remove.mutateAsync(article.id);
      if (!current()) return;
      refresh(); if (editing?.id === article.id) { setEditing(null); setVersion(value => value + 1); }
      setSuccess("Đã xóa bài viết.");
    } catch (error: unknown) { if (current()) setDeleteError(errorMessage(error)); }
    finally { deleteInFlight.current = false; if (mounted.current) remove.reset(); }
  };
  return <div className="wd min-w-0"><Panel title="Cẩm nang & bài viết" eyebrow="CMS" action={<button type="button" className="btn btn-outline" disabled={editorBusy || remove.isPending} onClick={newArticle}><Plus size={15} aria-hidden="true" />Viết bài mới</button>}>
    <p className="mb-4 text-sm text-slate-600">Admin và kiểm duyệt viên có thể tạo bài viết, tải ảnh bìa và xuất bản lên Cẩm nang cưới.</p>
    <ErrorText>{deleteError}</ErrorText>
    {success && <p role="status" className="mb-4 rounded-xl border border-[#c7d3c6] bg-[#edf3e9] p-3 text-sm text-[#233a42]">{success}</p>}
    {query.isPending ? <p role="status" className="py-4 text-sm muted">Đang tải danh sách bài viết...</p> : query.isError ? <div className="mb-5"><ErrorText>{errorMessage(query.error)}</ErrorText><button type="button" className="btn btn-outline mt-2" onClick={() => void query.refetch()}>Tải lại bài viết</button></div> : <div className="mb-6 space-y-3">
      {query.data.length ? query.data.map(article => <article key={article.id} className="flex min-w-0 flex-wrap items-center justify-between gap-3 rounded-xl border border-[#eadbd4] bg-[#fffaf4] p-3">
        <div className="flex min-w-0 flex-1 items-center gap-3">{articleImageSrc(article.thumbnailUrl) ? <img src={articleImageSrc(article.thumbnailUrl)} className="h-16 w-20 shrink-0 rounded-lg object-cover" alt="" /> : <span className="flex h-16 w-20 shrink-0 items-center justify-center rounded-lg bg-[#f5e4db]"><ImagePlus size={22} aria-hidden="true" /></span>}<div className="min-w-0"><h3 className="font-semibold text-[#233a42] [overflow-wrap:anywhere]">{article.title}</h3><p className="mt-1 text-xs text-slate-600">{article.category} · {article.isPublished ? "Đã xuất bản" : "Bản nháp"}</p></div></div>
        <div className="flex flex-wrap items-center gap-3 text-sm"><button type="button" className="inline-flex items-center gap-1 underline" disabled={editorBusy || remove.isPending} onClick={() => { setEditing(article); setVersion(value => value + 1); setSuccess(""); }}><PencilLine size={14} aria-hidden="true" />Sửa</button>{article.isPublished && <Link to={`/wiki/${article.slug}`} target="_blank" rel="noreferrer">Xem bài</Link>}<button type="button" className="inline-flex items-center gap-1 text-red-700" disabled={editorBusy || remove.isPending} onClick={() => void deleteArticle(article)}><Trash2 size={14} aria-hidden="true" />Xóa</button></div>
      </article>) : <p className="rounded-xl border border-dashed border-[#d5b9ab] p-5 text-sm text-slate-600">Chưa có bài viết. Bắt đầu bằng một bài mới bên dưới.</p>}
    </div>}
    {editing && detail.isPending ? <p role="status">Đang mở nội dung bài viết...</p> : editing && (detail.isError || !detail.data) ? <div><ErrorText>{detail.isError ? errorMessage(detail.error) : "Chưa tải được nội dung bài viết."}</ErrorText><button type="button" className="btn btn-outline mt-3" onClick={() => void detail.refetch()}>Thử mở lại</button><button type="button" className="btn btn-outline mt-3 ml-2" onClick={newArticle}>Bài mới</button></div> : <ArticleEditorForm key={`${editing?.id ?? "new"}:${version}`} article={editing ? detail.data! : null} actor={actor} onBusyChange={setEditorBusy} onNew={newArticle} onSaved={(title, published) => {
      if (!current()) return; refresh(); setEditing(null); setVersion(value => value + 1); setEditorBusy(false); setSuccess(`Đã ${published ? "xuất bản" : "lưu bản nháp"} bài “${title}”.`);
    }} />}
  </Panel></div>;
}
