import { useRef, useState } from "react";
import { Bell, Check, CheckCheck, ExternalLink, RefreshCw } from "lucide-react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useAuthStore } from "../../store/authStore";
import { Pagination, scrollToList, useResponsivePageSize } from "../../components/Pagination";
import { useNotifications, useReadNotifications } from "./notificationApi";
import { notificationTarget, notificationTime, type NotificationItem } from "./notificationModel";
import "./notifications.css";

export default function NotificationsPage() {
  const [params, setParams] = useSearchParams();
  const rawPage = Number(params.get("page"));
  const page = Number.isSafeInteger(rawPage) && rawPage > 0 ? rawPage : 1;
  const unreadOnly = params.get("filter") === "unread";
  const pageSize = useResponsivePageSize(8, 5);
  const heading = useRef<HTMLHeadingElement>(null);
  const user = useAuthStore(s => s.user);
  const navigate = useNavigate();
  const query = useNotifications(page, pageSize, unreadOnly);
  const mark = useReadNotifications();
  const [openError, setOpenError] = useState("");
  const changePage = (nextPage: number, filter = unreadOnly) => {
    setOpenError("");
    const next = new URLSearchParams();
    if (nextPage > 1) next.set("page", String(nextPage));
    if (filter) next.set("filter", "unread");
    setParams(next);
    scrollToList(heading.current);
  };
  const openNotification = async (notification: NotificationItem) => {
    const target = notificationTarget(notification.targetPath, user?.role);
    if (!target) return;
    const expectedToken = useAuthStore.getState().token;
    setOpenError("");
    try {
      if (!notification.readAt) await mark.mutateAsync(notification.id);
      if (useAuthStore.getState().token === expectedToken) navigate(target);
    } catch {
      setOpenError("Chưa đánh dấu được thông báo. Bạn có thể thử lại; nội dung vẫn được giữ nguyên.");
    }
  };
  const data = query.data;
  return <main className="wm-notifications" aria-busy={query.isFetching}>
    <div className="wm-notifications-heading">
      <div><span className="wm-notifications-eyebrow">KHÔNG BỎ LỠ ĐIỀU QUAN TRỌNG</span><h1 ref={heading}><Bell size={29} aria-hidden="true" />Thông báo</h1>
        <p>Cập nhật về lịch hẹn, yêu cầu dịch vụ và kế hoạch của bạn.</p></div>
      <Link to={user?.role === "Customer" ? "/customer/dashboard" : user?.role === "VendorOwner" ? "/vendor/dashboard" : "/admin/dashboard"}>Về không gian của bạn</Link>
    </div>
    <section className="wm-notifications-panel" aria-label="Danh sách thông báo">
      <div className="wm-notifications-toolbar">
        <div className="wm-notifications-filters" aria-label="Lọc thông báo">
          <button type="button" aria-pressed={!unreadOnly} onClick={() => changePage(1, false)}>Tất cả</button>
          <button type="button" aria-pressed={unreadOnly} onClick={() => changePage(1, true)}>Chưa đọc {data ? `(${data.unreadCount})` : ""}</button>
        </div>
        <button type="button" className="wm-notifications-readall" disabled={!data?.unreadCount || mark.isPending} onClick={() => { setOpenError(""); mark.mutate(null); }}>
          <CheckCheck size={16} aria-hidden="true" /> Đọc tất cả
        </button>
      </div>
      {(mark.isError || openError) && <p className="wm-notifications-error" role="alert">{openError || "Chưa cập nhật được thông báo. Vui lòng thử lại."}</p>}
      {query.isPending ? <div className="wm-notifications-empty" role="status">Đang tải thông báo của bạn...</div>
        : query.isError ? <div className="wm-notifications-empty" role="alert"><p>Chưa tải được thông báo. Vui lòng kiểm tra kết nối.</p><button type="button" onClick={() => query.refetch()}><RefreshCw size={16} />Thử lại</button></div>
        : !data?.items.length ? <div className="wm-notifications-empty"><Bell size={34} aria-hidden="true" /><h2>{unreadOnly ? "Bạn đã đọc hết thông báo" : "Chưa có thông báo nào"}</h2><p>{unreadOnly ? "Các cập nhật mới sẽ xuất hiện ở đây." : "Thông báo sẽ được lưu tại đây khi kế hoạch của bạn có cập nhật."}</p></div>
        : <>
          <p className="wm-notifications-summary" role="status">{data.totalCount} thông báo{unreadOnly ? " chưa đọc" : ""} · Trang {data.page}/{data.totalPages}</p>
          <ul className="wm-notifications-list">{data.items.map(notification => {
            const target = notificationTarget(notification.targetPath, user?.role);
            return <li key={notification.id} className={!notification.readAt ? "is-unread" : ""}>
              <span className="wm-notifications-dot" aria-label={notification.readAt ? "Đã đọc" : "Chưa đọc"} />
              <article>
                <p>{notification.message}</p><time dateTime={notification.createdAt}>{notificationTime(notification.createdAt)}</time>
                <div className="wm-notifications-item-actions">
                  {target && <button type="button" disabled={mark.isPending} onClick={() => void openNotification(notification)}>Xem nội dung <ExternalLink size={14} aria-hidden="true" /></button>}
                  {!notification.readAt && <button type="button" disabled={mark.isPending} onClick={() => { setOpenError(""); mark.mutate(notification.id); }}><Check size={14} aria-hidden="true" /> Đánh dấu đã đọc</button>}
                </div>
              </article>
            </li>;
          })}</ul>
          <Pagination page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} onPageChange={changePage} isLoading={query.isFetching} label="Phân trang thông báo" />
        </>}
    </section>
  </main>;
}
