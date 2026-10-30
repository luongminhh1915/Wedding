import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { Download, Plus, Upload, Users } from "lucide-react";
import { apiError } from "../api";
import { useGuests, usePlannerAction, type GuestInput, type Guest } from "./plannerApi";

const blank: GuestInput = { name: "", phone: "", group: "Bạn bè", side: "Shared", partySize: 1, status: "Pending", dietary: "", notes: "", tableId: null };
const statusNames: Record<string, string> = { Pending: "Chưa phản hồi", Attending: "Tham dự", NotAttending: "Không dự", Maybe: "Chưa chắc" };
function csvRows(text: string): string[][] {
  const rows: string[][] = []; let row: string[] = []; let cell = ""; let quoted = false; let closedQuote = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (c === '"') {
      if (quoted) { if (text[i + 1] === '"') { cell += '"'; i++; } else { quoted = false; closedQuote = true; } }
      else if (!cell && !closedQuote) quoted = true;
      else throw new Error("File CSV có dấu ngoặc kép không hợp lệ.");
    }
    else if (c === "," && !quoted) { row.push(cell); cell = ""; closedQuote = false; }
    else if ((c === "\n" || c === "\r") && !quoted) { if (c === "\r" && text[i + 1] === "\n") i++; row.push(cell); if (row.some(Boolean)) rows.push(row); row = []; cell = ""; closedQuote = false; }
    else { if (closedQuote && ![" ", "\t"].includes(c)) throw new Error("File CSV có ký tự thừa sau dấu ngoặc kép."); cell += c; }
  }
  if (quoted) throw new Error("File CSV có dấu ngoặc kép chưa đóng.");
  row.push(cell); if (row.some(Boolean)) rows.push(row); return rows;
}
function guestsFromCsv(text: string): GuestInput[] {
  const rows = csvRows(text.replace(/^\uFEFF/, ""));
  const headers = rows.shift()?.map(header => header.trim()) ?? [];
  const columns = ["name", "phone", "group", "side", "partySize", "status", "dietary", "notes"];
  if (headers[0] !== "name" || headers.some(header => !columns.includes(header)) || new Set(headers).size !== headers.length)
    throw new Error("Dùng CSV có cột name,phone,group,side,partySize,status,dietary,notes; không lặp tên cột. Xuất CSV để lấy mẫu.");
  if (rows.length < 1 || rows.length > 1000) throw new Error("Mỗi lần nhập từ 1 đến 1000 nhóm khách.");
  return rows.map((row, index) => {
    if (row.length > headers.length) throw new Error(`Dòng ${index + 2} có nhiều ô hơn số cột tiêu đề.`);
    const values = Object.fromEntries(headers.map((header, i) => [header, row[i] ?? ""]));
    const partySize = values.partySize?.trim() ? Number(values.partySize) : 1;
    const side = values.side?.trim() || "Shared"; const status = values.status?.trim() || "Pending";
    if (!values.name?.trim() || values.name.length > 160) throw new Error(`Dòng ${index + 2}: tên nhóm khách cần từ 1 đến 160 ký tự.`);
    if (!Number.isInteger(partySize) || partySize < 1 || partySize > 20) throw new Error(`Dòng ${index + 2}: số người phải là số nguyên từ 1 đến 20.`);
    if (!["Shared", "Bride", "Groom"].includes(side) || !Object.hasOwn(statusNames, status)) throw new Error(`Dòng ${index + 2}: phía gia đình hoặc trạng thái phản hồi không hợp lệ.`);
    if ((values.phone?.length ?? 0) > 30 || (values.group?.length ?? 0) > 100 || (values.dietary?.length ?? 0) > 500 || (values.notes?.length ?? 0) > 2000)
      throw new Error(`Dòng ${index + 2}: thông tin khách mời vượt giới hạn cho phép.`);
    return { ...blank, name: values.name.trim(), phone: values.phone, group: values.group?.trim() || "Khác", side, partySize, status, dietary: values.dietary, notes: values.notes };
  });
}
const csvCell = (v: unknown) => `"${String(v ?? "").replace(/^[=+@-]/, "'$&").replaceAll('"', '""')}"`;
type ImportPreview = { generation: number; fileName: string; rows: GuestInput[] };
type ImportLifecycle = { generation: number; activePreview: ImportPreview | null; sending: boolean; mounted: boolean };
export function GuestsPlanner() {
  const query = useGuests(); const action = usePlannerAction();
  const [form, setForm] = useState<GuestInput>(blank);
  const [filter, setFilter] = useState(""); const [error, setError] = useState("");
  const [tableName, setTableName] = useState(""); const [capacity, setCapacity] = useState(10); const [tableId, setTableId] = useState<string | undefined>();
  const [importPreview, setImportPreview] = useState<ImportPreview | null>(null);
  const [readingImport, setReadingImport] = useState(false); const [importFileName, setImportFileName] = useState(""); const [importNotice, setImportNotice] = useState("");
  const importLifecycle = useRef<ImportLifecycle>({ generation: 0, activePreview: null, sending: false, mounted: true });
  useEffect(() => {
    const lifecycle = importLifecycle.current;
    lifecycle.mounted = true;
    return () => { lifecycle.mounted = false; lifecycle.generation++; lifecycle.activePreview = null; };
  }, []);
  const data = query.data; const canEdit = data?.myRole === "Owner" || data?.myRole === "Editor"; const tables = data?.tables ?? []; const guests = data?.guests ?? [];
  const latestRead = useRef({ canEdit, isLoading: query.isLoading, isError: query.isError, isFetching: query.isFetching });
  useLayoutEffect(() => { latestRead.current = { canEdit, isLoading: query.isLoading, isError: query.isError, isFetching: query.isFetching }; }, [canEdit, query.isLoading, query.isError, query.isFetching]);
  const set = <K extends keyof GuestInput>(key: K, value: GuestInput[K]) => setForm(f => ({ ...f, [key]: value }));
  const saveGuest = async (g: GuestInput) => action.mutateAsync({ path: "guests", data: { guests: [g] } });
  const exportCsv = () => {
    const rows = [["name", "phone", "group", "side", "partySize", "status", "dietary", "notes"], ...guests.map(g => [g.name, g.phone, g.group, g.side, g.partySize, g.status, g.dietary, g.notes])];
    const url = URL.createObjectURL(new Blob(["\uFEFF" + rows.map(r => r.map(csvCell).join(",")).join("\r\n")], { type: "text/csv;charset=utf-8" }));
    const a = document.createElement("a"); a.href = url; a.download = "wedding-match-guests.csv"; a.click(); URL.revokeObjectURL(url);
  };
  const importCsv = async (file?: File) => {
    const lifecycle = importLifecycle.current;
    if (lifecycle.sending || action.isPending || !latestRead.current.canEdit) return;
    const generation = ++lifecycle.generation;
    lifecycle.activePreview = null; setImportPreview(null); setImportFileName(file?.name ?? ""); setError(""); setImportNotice(""); action.reset();
    setReadingImport(!!file);
    if (!file) return;
    const isCurrent = () => lifecycle.mounted && generation === lifecycle.generation;
    try {
      if (file.size > 2_000_000) throw new Error("File tối đa 2 MB.");
      const content = await file.text(); if (!isCurrent()) return;
      const preview = { generation, fileName: file.name, rows: guestsFromCsv(content) };
      lifecycle.activePreview = preview; setImportPreview(preview); setError("");
    } catch (e) { if (isCurrent()) { lifecycle.activePreview = null; setImportPreview(null); setError(e instanceof Error ? e.message : "Không thể đọc tệp CSV. Hãy kiểm tra file rồi chọn lại."); } }
    finally { if (isCurrent()) setReadingImport(false); }
  };
  const cancelImport = () => {
    const lifecycle = importLifecycle.current;
    if (lifecycle.sending || action.isPending) return;
    lifecycle.generation++; lifecycle.activePreview = null; setImportPreview(null); setReadingImport(false); setImportFileName(""); setError(""); setImportNotice("");
  };
  const submitImport = (preview: ImportPreview) => {
    const lifecycle = importLifecycle.current;
    if (lifecycle.sending || action.isPending) return;
    const latest = latestRead.current;
    if (lifecycle.activePreview !== preview || preview.generation !== lifecycle.generation || !latest.canEdit || latest.isError || latest.isLoading || latest.isFetching) {
      setError("Bản xem trước không còn sẵn sàng để nhập. Hãy kiểm tra lại tệp CSV và tải lại danh sách khách."); return;
    }
    lifecycle.sending = true; setError("");
    try {
      action.mutate({ path: "guests/import", data: { guests: preview.rows } }, {
        onSuccess: () => { if (lifecycle.mounted && lifecycle.activePreview === preview) { lifecycle.generation++; lifecycle.activePreview = null; setImportPreview(null); setImportFileName(""); setImportNotice(`Đã nhập ${preview.rows.length} nhóm khách vào danh sách.`); } },
        onSettled: () => { lifecycle.sending = false; },
      });
    } catch (e) { lifecycle.sending = false; setError(apiError(e)); }
  };
  if (query.isLoading) return <div className="wd card p-6">Đang tải khách mời...</div>;
  if (query.isError) return <div className="wd card p-6 text-red-700" role="alert"><p>{apiError(query.error)}</p><button type="button" className="btn btn-outline mt-3" disabled={query.isFetching} onClick={() => void query.refetch()}>Thử tải lại khách mời</button></div>;
  return <section className="wd space-y-5">
    <div className="card p-5"><div className="flex flex-wrap justify-between gap-3"><div><h2 className="serif text-2xl font-bold flex items-center gap-2"><Users size={22} />Khách mời & sơ đồ bàn</h2><p className="muted text-sm mt-1">{guests.length} dòng nhóm khách · {guests.filter(g => g.status === "Attending").reduce((s, g) => s + g.partySize, 0)} người xác nhận · {tables.length} bàn. Phản hồi RSVP online tự xuất hiện ở đây.</p><p className="text-xs muted mt-2">Nhóm được chia sang nhiều bàn sẽ hiện thành nhiều dòng cùng tên; tổng số người vẫn giữ nguyên.</p></div><button className="btn btn-ghost" onClick={exportCsv}><Download size={14} />Xuất CSV</button></div></div>
    {(error || action.isError) && <p role="alert" className="p-3 rounded-xl bg-red-50 text-red-700">{error || apiError(action.error)}</p>}
    {importNotice && <p role="status" className="p-3 rounded-xl bg-green-50 text-green-800">{importNotice}</p>}
    {canEdit && <form className="card p-5 grid sm:grid-cols-2 lg:grid-cols-4 gap-3" onSubmit={e => { e.preventDefault(); void saveGuest(form).then(() => setForm(blank)).catch(() => {}); }}>
      <label><span className="label">Tên nhóm khách *</span><input required maxLength={160} className="input" value={form.name} onChange={e => set("name", e.target.value)} /></label>
      <label><span className="label">Điện thoại</span><input maxLength={30} className="input" value={form.phone} onChange={e => set("phone", e.target.value)} /></label>
      <label><span className="label">Nhóm</span><input maxLength={100} className="input" value={form.group} onChange={e => set("group", e.target.value)} /></label>
      <label><span className="label">Phía gia đình</span><select className="input" value={form.side} onChange={e => set("side", e.target.value)}><option value="Shared">Chung</option><option value="Bride">Cô dâu</option><option value="Groom">Chú rể</option></select></label>
      <label><span className="label">Số người</span><input required type="number" min={1} max={20} className="input" value={form.partySize} onChange={e => set("partySize", +e.target.value)} /></label>
      <label><span className="label">Phản hồi</span><select className="input" value={form.status} onChange={e => set("status", e.target.value)}>{Object.entries(statusNames).map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></label>
      <label><span className="label">Ăn uống</span><input maxLength={500} className="input" value={form.dietary} onChange={e => set("dietary", e.target.value)} placeholder="Ăn chay, dị ứng..." /></label>
      <label><span className="label">Ghi chú</span><input maxLength={2000} className="input" value={form.notes} onChange={e => set("notes", e.target.value)} /></label>
      <div className="sm:col-span-2 lg:col-span-4 flex flex-wrap gap-2"><button disabled={action.isPending} className="btn btn-primary"><Plus size={14} />{form.id ? "Lưu thay đổi" : "Thêm khách"}</button>{form.id && <button type="button" className="btn btn-ghost" onClick={() => setForm(blank)}>Hủy sửa</button>}<label className="btn btn-ghost cursor-pointer"><Upload size={14} />Nhập CSV<input type="file" accept=".csv,text/csv" aria-label="Chọn tệp khách mời CSV" disabled={action.isPending} className="sr-only" onChange={e => { const file = e.target.files?.[0]; e.target.value = ""; void importCsv(file); }} /></label></div>
    </form>}
    {readingImport && canEdit && <div className="card p-5"><p role="status">Đang đọc {importFileName}...</p><button type="button" className="btn btn-ghost mt-2" onClick={cancelImport}>Hủy đọc CSV</button></div>}
    {importPreview && canEdit && <div className="card p-5"><h3 className="font-bold">Xác nhận nhập {importPreview.rows.length} nhóm khách</h3><p className="text-xs muted mt-2">Tệp: {importPreview.fileName}</p><p className="muted text-sm my-2">Nhập bổ sung, không xóa danh sách hiện có. Kiểm tra tên: {importPreview.rows.slice(0, 5).map(r => r.name).join(", ")}{importPreview.rows.length > 5 ? "..." : ""}</p><button type="button" className="btn btn-primary" disabled={action.isPending || readingImport || query.isFetching} onClick={() => submitImport(importPreview)}>Nhập vào danh sách</button><button type="button" className="btn btn-ghost" disabled={action.isPending} onClick={cancelImport}>Hủy nhập CSV</button></div>}
    <div className="card p-5"><label><span className="label">Tìm tên, điện thoại hoặc nhóm</span><input className="input" value={filter} onChange={e => setFilter(e.target.value)} /></label><div className="overflow-x-auto mt-4"><table className="w-full text-sm text-left"><thead><tr className="border-b border-[var(--line)]"><th className="p-2">Khách / nhóm</th><th className="p-2">Số người</th><th className="p-2">Phản hồi</th><th className="p-2">Bàn</th><th /></tr></thead><tbody>{guests.filter(g => `${g.name} ${g.phone} ${g.group}`.toLowerCase().includes(filter.toLowerCase())).map(g => <tr key={g.id} className="border-b border-[var(--line)]"><td className="p-2"><strong>{g.name}</strong><small className="block muted">{g.group} · {g.phone || "—"}</small></td><td className="p-2">{g.partySize}</td><td className="p-2">{statusNames[g.status]}</td><td className="p-2"><select className="input min-w-[110px]" aria-label={`Bàn của ${g.name}`} disabled={!canEdit || action.isPending || g.status === "NotAttending"} value={g.tableId ?? ""} onChange={e => { void saveGuest({ ...g, tableId: e.target.value || null }).catch(() => {}); }}><option value="">Chưa xếp</option>{tables.map(t => <option key={t.id} value={t.id}>{t.name} ({t.assigned}/{t.capacity})</option>)}</select></td><td className="p-2">{canEdit && <div className="flex gap-2"><button className="btn btn-ghost" onClick={() => setForm(g)}>Sửa</button><button className="btn btn-ghost" disabled={action.isPending} onClick={() => { if (window.confirm(`Xóa nhóm ${g.name}?`)) action.mutate({ path: `guests/${g.id}`, method: "delete" }); }}>Xóa</button></div>}</td></tr>)}</tbody></table>{!guests.length && <p className="muted text-sm py-5">Chưa có khách mời. Thêm thủ công, nhập CSV hoặc chia sẻ thiệp để nhận RSVP.</p>}</div></div>
    <div className="card p-5"><h3 className="serif text-xl font-bold mb-3">Sơ đồ bàn</h3>{canEdit && <form className="flex flex-wrap gap-3 items-end mb-5" onSubmit={e => { e.preventDefault(); action.mutate({ path: "tables", data: { id: tableId, name: tableName, capacity } }, { onSuccess: () => { setTableName(""); setTableId(undefined); } }); }}><label className="grow"><span className="label">Tên bàn</span><input required maxLength={100} className="input" value={tableName} onChange={e => setTableName(e.target.value)} /></label><label><span className="label">Số ghế</span><input className="input max-w-[100px]" required type="number" min={1} max={100} value={capacity} onChange={e => setCapacity(+e.target.value)} /></label><button className="btn btn-primary" disabled={action.isPending}>{tableId ? "Lưu bàn" : "Thêm bàn"}</button>{tableId && <button type="button" className="btn btn-ghost" onClick={() => { setTableId(undefined); setTableName(""); }}>Hủy</button>}<button type="button" className="btn btn-ghost" disabled={action.isPending || !tables.length} onClick={() => action.mutate({ path: "tables/auto-assign" })}>Tự xếp khách xác nhận</button></form>}
      <p className="text-xs muted mb-4">Giữ nguyên khách đã có bàn, tự động lấp các ghế còn trống. Nhóm lớn có thể tách thành nhiều dòng cùng tên ở các bàn khác nhau; tổng số người không thay đổi. Khi hết ghế, phần còn lại vẫn chưa xếp bàn.</p><div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">{tables.map(t => <article key={t.id} className="border border-[var(--line)] rounded-2xl p-4"><div className="flex justify-between items-center"><strong>{t.name}</strong><span className="chip">{t.assigned}/{t.capacity}</span></div><ul className="text-sm my-3 space-y-1">{guests.filter((g: Guest) => g.tableId === t.id).map(g => <li key={g.id}>{g.name} · {g.partySize} người</li>)}</ul>{canEdit && <div className="flex gap-2"><button className="btn btn-ghost" onClick={() => { setTableId(t.id); setTableName(t.name); setCapacity(t.capacity); }}>Sửa bàn</button><button className="btn btn-ghost" disabled={action.isPending} onClick={() => { if (window.confirm(`Xóa ${t.name} và đưa khách về chưa xếp?`)) action.mutate({ path: `tables/${t.id}`, method: "delete" }); }}>Xóa</button></div>}</article>)}</div>
    </div>
  </section>;
}
