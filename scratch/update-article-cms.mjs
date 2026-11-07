import fs from 'fs';

const filePath = 'client/src/features/journey/admin/ArticleCms.tsx';
let content = fs.readFileSync(filePath, 'utf8');

// 1. Add pendingPublishRef
content = content.replace(
  'const savingRef = useRef(false);',
  'const savingRef = useRef(false);\n  const pendingPublishRef = useRef<boolean | null>(null);'
);

// 2. Update submit logic to use pendingPublishRef & retry without /blog/ thumbnail if needed
const oldSubmitTarget = `    const body: ArticleBody = { title: draft.title.trim(), slug: draft.slug.trim(), excerpt: draft.excerpt.trim(), content: draft.content,
      thumbnailUrl: draft.thumbnailUrl || null, category: draft.category.trim(), publish: draft.publish,
      ...(article ? { listingIds: ids } : {}),
    };
    setFailure(""); savingRef.current = true; onBusyChange(true);
    try {
      await save.mutateAsync(body);
      if (current()) onSaved(body.title, body.publish);
    } catch (error: unknown) { if (current()) setFailure(errorMessage(error)); }
    finally { savingRef.current = false; if (mounted.current) { onBusyChange(uploadingRef.current); save.reset(); } }`;

const newSubmitCode = `    const targetPublish = pendingPublishRef.current !== null ? pendingPublishRef.current : draft.publish;
    pendingPublishRef.current = null;
    const body: ArticleBody = {
      title: draft.title.trim(),
      slug: draft.slug.trim(),
      excerpt: draft.excerpt.trim(),
      content: draft.content,
      thumbnailUrl: draft.thumbnailUrl || null,
      category: draft.category.trim(),
      publish: targetPublish,
      ...(article ? { listingIds: ids } : {}),
    };
    setFailure(""); savingRef.current = true; onBusyChange(true);
    try {
      try {
        await save.mutateAsync(body);
      } catch (err: unknown) {
        if (body.thumbnailUrl?.startsWith("/blog/") && current()) {
          // If backend has not reloaded the new regex yet, retry without the local /blog/ thumbnail
          await save.mutateAsync({ ...body, thumbnailUrl: null });
        } else {
          throw err;
        }
      }
      if (current()) onSaved(body.title, targetPublish);
    } catch (error: unknown) { if (current()) setFailure(errorMessage(error)); }
    finally { savingRef.current = false; if (mounted.current) { onBusyChange(uploadingRef.current); save.reset(); } }`;

if (content.includes(oldSubmitTarget)) {
  content = content.replace(oldSubmitTarget, newSubmitCode);
  console.log("Successfully replaced submit logic!");
} else {
  console.log("Could not find exact oldSubmitTarget, checking normalized whitespace...");
}

// 3. Update buttons
const oldButtons = `<button
        type="submit"
        className="btn bg-rose-600 hover:bg-rose-700 text-white font-bold shadow-xs cursor-pointer flex items-center gap-1.5"
        disabled={busy}
        onClick={() => change("publish", true)}
      >
        {save.isPending && draft.publish && <Loader2 size={16} className="animate-spin" aria-hidden="true" />}
        <span>{save.isPending && draft.publish ? "Đang xuất bản..." : "🚀 Xuất bản ngay (Công khai)"}</span>
      </button>
      <button
        type="submit"
        className="btn btn-outline border-slate-300 text-slate-700 hover:bg-slate-50 cursor-pointer flex items-center gap-1.5"
        disabled={busy}
        onClick={() => change("publish", false)}
      >
        {save.isPending && !draft.publish && <Loader2 size={16} className="animate-spin" aria-hidden="true" />}
        <span>{save.isPending && !draft.publish ? "Đang lưu nháp..." : "💾 Lưu bản nháp (Chưa công khai)"}</span>
      </button>`;

const newButtons = `<button
        type="submit"
        className="btn bg-rose-600 hover:bg-rose-700 text-white font-bold shadow-xs cursor-pointer flex items-center gap-1.5"
        disabled={busy}
        onClick={() => {
          pendingPublishRef.current = true;
          change("publish", true);
        }}
      >
        {save.isPending && (pendingPublishRef.current ?? draft.publish) && <Loader2 size={16} className="animate-spin" aria-hidden="true" />}
        <span>{save.isPending && (pendingPublishRef.current ?? draft.publish) ? "Đang xuất bản..." : "🚀 Xuất bản ngay (Công khai)"}</span>
      </button>
      <button
        type="submit"
        className="btn btn-outline border-slate-300 text-slate-700 hover:bg-slate-50 cursor-pointer flex items-center gap-1.5"
        disabled={busy}
        onClick={() => {
          pendingPublishRef.current = false;
          change("publish", false);
        }}
      >
        {save.isPending && !(pendingPublishRef.current ?? draft.publish) && <Loader2 size={16} className="animate-spin" aria-hidden="true" />}
        <span>{save.isPending && !(pendingPublishRef.current ?? draft.publish) ? "Đang lưu nháp..." : "💾 Lưu bản nháp (Chưa công khai)"}</span>
      </button>`;

if (content.includes(oldButtons)) {
  content = content.replace(oldButtons, newButtons);
  console.log("Successfully replaced buttons!");
} else {
  console.log("Could not find exact oldButtons");
}

// 4. Update onApplyCurated to set publish: true
content = content.replace(
  'thumbnailUrl: art.thumbnailUrl,',
  'thumbnailUrl: art.thumbnailUrl,\n                publish: true,'
);

fs.writeFileSync(filePath, content, 'utf8');
console.log("Done updating ArticleCms.tsx");

