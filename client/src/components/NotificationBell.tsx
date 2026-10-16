import { Bell } from "lucide-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axiosClient from "../services/axiosClient";
export default function NotificationBell() {
  const qc = useQueryClient();
  const query = useQuery({ queryKey: ["notifications"], queryFn: async () => (await axiosClient.get<{ id: string; message: string; createdAt: string; readAt?: string }[]>("/api/notifications")).data, refetchInterval: 60000 });
  const mark = useMutation({ mutationFn: () => axiosClient.post("/api/notifications/read-all"), onSuccess: () => qc.invalidateQueries({ queryKey: ["notifications"] }) });
  const unread = query.data?.filter((n) => !n.readAt).length ?? 0;
  return <details className="wm-notification"><summary aria-label={`Thông báo${unread ? `, ${unread} chưa đọc` : ""}`}><Bell size={19} />{unread > 0 && <span className="wm-notification-count">{unread}</span>}</summary><section aria-label="Thông báo của bạn"><div className="flex justify-between gap-3"><strong>Thông báo</strong><button className="underline text-xs" disabled={!unread || mark.isPending} onClick={() => mark.mutate()}>Đọc tất cả</button></div>{query.isError ? <p>Chưa tải được thông báo.</p> : !query.data?.length ? <p>Bạn chưa có thông báo.</p> : <ul>{query.data.map((n) => <li key={n.id} className={!n.readAt ? "unread" : ""}><p>{n.message}</p><time>{new Date(n.createdAt).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" })}</time></li>)}</ul>}</section></details>;
}
