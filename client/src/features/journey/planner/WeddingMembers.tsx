import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Copy, Users } from "lucide-react";
import { apiError } from "../api";
import { useMembers, usePlannerAction } from "./plannerApi";
import { useAuthStore } from "../../../store/authStore";

export function WeddingMembers() {
  const current = useAuthStore(x => x.user);
  const members = useMembers();
  const action = usePlannerAction();
  const [params, setParams] = useSearchParams();
  const [email, setEmail] = useState("");
  const [role, setRole] = useState("Editor");
  const [link, setLink] = useState("");
  const [copied, setCopied] = useState(false);
  const join = params.get("join");
  const invite = async () => {
    const result = await action.mutateAsync({ path: "members/invite", data: { role, email: email || null } }) as { code: string };
    setLink(`${window.location.origin}/planner?join=${result.code}`);
  };
  return <section className="wd card p-5 mb-6" aria-label="Thành viên hồ sơ cưới">
    <h2 className="font-bold flex items-center gap-2"><Users size={17} />Cùng nhau lên kế hoạch</h2>
    {join && <div className="p-4 my-3 rounded-xl bg-[var(--bg)]"><p className="text-sm mb-2">Bạn nhận được lời mời tham gia một hồ sơ cưới. Chấp nhận sẽ chuyển Planner sang hồ sơ đó; hồ sơ riêng trước đó vẫn được giữ.</p><button className="btn btn-primary" disabled={action.isPending} onClick={() => action.mutate({ path: `join/${encodeURIComponent(join)}` }, { onSuccess: () => { const next = new URLSearchParams(params); next.delete("join"); setParams(next); } })}>Tham gia hồ sơ cưới</button></div>}
    {members.isLoading ? <p className="muted text-sm mt-2">Đang tải thành viên...</p> : members.isError ? <p role="alert" className="text-red-700">{apiError(members.error)}</p> : <>
      <p className="muted text-xs mt-2">Quyền của bạn: {members.data?.myRole === "Owner" ? "Chủ hồ sơ" : members.data?.myRole === "Editor" ? "Chỉnh sửa" : "Chỉ xem"}. Link mời dùng một lần, có hiệu lực 7 ngày.</p>
      <ul className="space-y-2 mt-4">{members.data?.members.map(m => <li key={m.userId} className="flex flex-wrap justify-between gap-2 items-center border-b border-[var(--line)] pb-2 text-sm"><span>{m.name}<small className="block muted">{m.email}</small></span><div className="flex items-center gap-2">{members.data?.myRole === "Owner" && m.role !== "Owner" ? <><select className="input max-w-[130px]" aria-label={`Quyền của ${m.name}`} value={m.role} disabled={action.isPending} onChange={e => action.mutate({ path: `members/${m.id}`, method: "put", data: { role: e.target.value } })}><option value="Editor">Chỉnh sửa</option><option value="Viewer">Chỉ xem</option></select><button className="btn btn-ghost" disabled={action.isPending} onClick={() => { if (window.confirm(`Xóa quyền truy cập của ${m.name}?`)) action.mutate({ path: `members/${m.id}`, method: "delete" }); }}>Gỡ</button></> : <span className="chip">{m.role === "Owner" ? "Chủ hồ sơ" : m.role === "Editor" ? "Chỉnh sửa" : "Chỉ xem"}</span>}{members.data?.myRole !== "Owner" && m.userId === current?.id && <button className="btn btn-ghost" disabled={action.isPending} onClick={() => { if (window.confirm("Rời hồ sơ cưới đang chia sẻ?")) action.mutate({ path: `members/${m.id}`, method: "delete" }); }}>Rời</button>}</div></li>)}</ul>
      {members.data?.myRole === "Owner" && <form className="flex flex-wrap items-end gap-3 mt-4" onSubmit={e => { e.preventDefault(); void invite().catch(() => {}); }}><label className="grow"><span className="label">Email người nhận (tùy chọn)</span><input className="input" type="email" maxLength={256} value={email} onChange={e => setEmail(e.target.value)} placeholder="Giới hạn link theo đúng email này" /></label><label><span className="label">Quyền</span><select className="input" value={role} onChange={e => setRole(e.target.value)}><option value="Editor">Chỉnh sửa</option><option value="Viewer">Chỉ xem</option></select></label><button className="btn btn-primary" disabled={action.isPending}>Tạo link mời</button></form>}
      {link && <div className="flex flex-wrap gap-2 mt-3"><input className="input flex-1 min-w-0" readOnly value={link} aria-label="Link mời dùng một lần" /><button className="btn btn-ghost" onClick={() => void navigator.clipboard.writeText(link).then(() => setCopied(true))}><Copy size={14} />{copied ? "Đã chép" : "Chép link"}</button></div>}
    </>}{action.isError && <p role="alert" className="text-red-700 text-sm mt-3">{apiError(action.error)}</p>}
  </section>;
}
