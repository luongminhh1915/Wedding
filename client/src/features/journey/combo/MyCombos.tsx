import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  Archive,
  ArrowRight,
  CalendarDays,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CircleHelp,
  Eye,
  ImageIcon,
  MapPin,
  Pencil,
  Plus,
  RotateCcw,
  ShoppingBag,
  Users,
  XCircle,
} from "lucide-react";
import { useSavedCombosPage, type SavedCombo } from "../comboApi";
import { Pagination } from "../../../components/Pagination";
import { useOrderPage } from "../customerOrders/useOrderPage";
import { useNotificationFocus } from "../../notifications/useNotificationFocus";
import { PrivateServiceAgreementPanel } from "../../private-agreements/PrivateServiceAgreementPanel";
import { apiError } from "../api";
import { OrderReadError, orderReadBlockedMessage } from "../customerOrders/OrderReadError";
import { vnd } from "../format";
import { ComboDialog } from "./ComboDialog";
import { unitName } from "./cartModel";
import { comboEditIntent } from "./comboEditing";
import {
  useCustomerOrderAction,
  useCustomerOrderJourney,
  type OrderJourneyItem,
} from "../customerOrders/customerOrderApi";
import {
  comboCardModel,
  type ComboListFilter,
  type ComboCardAction,
} from "../customerOrders/comboOrderModel";
import {
  OrderActionDialog,
  OrderCard,
  OrderFilterBar,
  OrderPhoto,
  OrderServiceRow,
  OrderStatus,
} from "../customerOrders/OrderUi";

const date = (value?: string | null) =>
  value && Number.isFinite(new Date(value).getTime())
    ? new Date(value).toLocaleDateString("vi-VN", {
        timeZone: "Asia/Ho_Chi_Minh",
      })
    : "Chưa chọn";
const filters: { id: ComboListFilter; label: string }[] = [
  { id: "all", label: "Tất cả" },
  { id: "draft", label: "Bản nháp" },
  { id: "active", label: "Đang theo dõi" },
  { id: "closed", label: "Đã kết thúc" },
  { id: "archived", label: "Đã lưu trữ" },
];
type ActionSelection = {
  order: SavedCombo;
  type: Exclude<ComboCardAction, "edit">;
};

export function MyCombos() {
  const { page, pageSize, filter, setFilter, focusId, changePage, listRef } = useOrderPage("combo");
  const orders = useSavedCombosPage(page, pageSize, filter, focusId);
  const journey = useCustomerOrderJourney(orders.data?.items.map(o => o.id) ?? []);
  const action = useCustomerOrderAction();
  const [selected, setSelected] = useState<ActionSelection | null>(null);
  const [reason, setReason] = useState("");
  const [detail, setDetail] = useState<SavedCombo | null>(null);
  const [photo, setPhoto] = useState<{
    title: string;
    sources: string[];
    index: number;
  } | null>(null);
  const navigate = useNavigate();
  const data = orders.data?.items ?? [];
  const metadata = new Map(
    journey.data
      ?.filter((item) => item.kind === "combo")
      .map((item) => [item.id, item] as const) ?? [],
  );
  const visible = data;
  const notificationFocus = useNotificationFocus("combo", visible.map(row => row.id), orders.isSuccess && !orders.isFetching && !journey.isLoading);
  const openAction = (
    order: SavedCombo,
    type: Exclude<ComboCardAction, "edit">,
  ) => {
    setSelected({ order, type });
    setReason("");
    action.reset();
  };
  const run = () => {
    if (
      !selected ||
      orders.isError || journey.isError ||
      !comboCardModel(
        selected.order,
        metadata.get(selected.order.id),
      ).actions.includes(selected.type)
    )
      return;
    if (selected.type === "withdraw" && reason.trim().length < 10) return;
    action.mutate(
      {
        kind: "combo",
        id: selected.order.id,
        action: selected.type,
        body: selected.type === "withdraw" ? { reason: reason.trim() } : {},
      },
      { onSuccess: () => setSelected(null) },
    );
  };
  return (
    <section className="co-section" aria-label="Combo của bạn" ref={listRef} style={{ scrollMarginTop: "calc(var(--wm-header-offset, 88px) + 18px)" }}>
      <div className="co-section-heading">
        <div>
          <p className="co-eyebrow">DỊCH VỤ CHO NGÀY VUI</p>
          <h2>Combo của hai bạn</h2>
          <p>
            Từng combo, từng nhà cung cấp, gọn trong một nơi. Xem lại ảnh, dịch
            vụ và tiến độ trước khi quyết định bước tiếp theo.
          </p>
        </div>
        <Link className="co-button primary" to="/combo">
          <Plus size={14} />
          Tạo combo mới
        </Link>
      </div>
      <OrderFilterBar
        value={orders.data?.filter ?? filter}
        onChange={(value) => setFilter(value as ComboListFilter)}
        options={filters}
        label="Lọc combo theo tiến độ"
      />
      {!orders.isError && journey.isError && (
        <div className="co-error" role="alert">
          Chưa tải được quyền thao tác và hình ảnh của đơn. Không thể sửa, rút
          hay lưu trữ cho tới khi kiểm tra được trạng thái mới nhất.
          <button
            className="co-button secondary ml-3"
            onClick={() => void journey.refetch()}
          >
            Tải lại trạng thái
          </button>
        </div>
      )}
      {notificationFocus.missing && <p className="co-summary-note" role="status">Combo trong thông báo không còn khả dụng trong tài khoản này. <button className="co-button secondary" onClick={notificationFocus.clearFocus}>Xem danh sách</button></p>}
      {orders.isError ? <OrderReadError label="lựa chọn dịch vụ và combo" error={orders.error} retry={orders.refetch} busy={orders.isFetching} /> : orders.isLoading || journey.isLoading ? (
        <div className="co-loading" role="status">
          <ShoppingBag size={30} />
          <p>Đang mở các combo và tiến độ của bạn...</p>
        </div>
      ) : visible.length === 0 ? (
        <div className="co-empty">
          <ShoppingBag size={34} strokeWidth={1.2} />
          <h3>
            {filter === "archived"
              ? "Chưa có combo lưu trữ"
              : data.length
                ? "Chưa có combo trong nhóm này"
                : "Ngày vui bắt đầu từ một lựa chọn"}
          </h3>
          <p>
            {filter === "archived"
              ? "Lưu trữ chỉ ẩn combo khỏi danh sách chính, không xóa lịch sử. Bạn có thể khôi phục khi cần."
              : "Chọn dịch vụ lẻ hoặc combo của một NCC rồi lưu nháp hoặc gửi yêu cầu báo giá. Combo giá cố định giữ nguyên thành phần; muốn đổi hãy trao đổi với NCC. Chưa tự động thanh toán."}
          </p>
          {data.length ? (
            <button
              className="co-button secondary"
              onClick={() => setFilter("all")}
            >
              Xem tất cả combo
            </button>
          ) : (
            <Link className="co-button primary" to="/combo">
              Khám phá combo
              <ArrowRight size={14} />
            </Link>
          )}
        </div>
      ) : (
        <div className="co-card-list">
          {visible.map((order) => (
            <ComboOrderCard
              key={order.id}
              order={order}
              focused={focusId === order.id}
              metadata={journey.isError ? undefined : metadata.get(order.id)}
              onView={() => setDetail(order)}
              onPhoto={(title, sources) =>
                setPhoto({ title, sources, index: 0 })
              }
              onEdit={() =>
                navigate("/combo", {
                  state: comboEditIntent(order),
                })
              }
              onAction={(type) => openAction(order, type)}
            />
          ))}
        </div>
      )}
      {!orders.isError && <Pagination page={orders.data?.page ?? page} pageSize={pageSize} totalCount={orders.data?.totalCount ?? 0} isLoading={orders.isFetching} onPageChange={next => changePage(next, orders.data?.filter)} label="Phân trang combo đã chọn" />}
      {selected && (
        <OrderActionDialog
          title={
            selected.type === "withdraw"
              ? "Rút yêu cầu combo?"
              : selected.type === "archive"
                ? "Lưu trữ combo này?"
                : "Khôi phục combo?"
          }
          description={
            selected.type === "withdraw" ? (
              <>
                <p>
                  Yêu cầu {selected.order.code} sẽ được rút khỏi luồng báo giá
                  nếu chưa có hợp đồng khóa. Các hợp đồng đã xác nhận không bị
                  tự hủy.
                </p>
                <p>
                  Nhà cung cấp sẽ thấy lý do bạn rút yêu cầu. Đây không phải
                  thao tác xóa lịch sử.
                </p>
              </>
            ) : selected.type === "archive" ? (
              <p>
                Combo sẽ được chuyển vào mục “Đã lưu trữ”. Thao tác chỉ ẩn khỏi
                danh sách chính, không hủy dịch vụ, hợp đồng hoặc khoản thanh
                toán.
              </p>
            ) : (
              <p>
                Đưa combo trở lại danh sách chính. Trạng thái giao dịch và lịch
                sử không thay đổi.
              </p>
            )
          }
          confirmLabel={
            selected.type === "withdraw"
              ? "Xác nhận rút yêu cầu"
              : selected.type === "archive"
                ? "Lưu trữ combo"
                : "Khôi phục combo"
          }
          onConfirm={run}
          onClose={() => setSelected(null)}
          busy={action.isPending}
          confirmDisabled={
            orders.isError || journey.isError ||
            !comboCardModel(selected.order, metadata.get(selected.order.id)).actions.includes(selected.type) ||
            (selected.type === "withdraw" && reason.trim().length < 10)
          }
          danger={selected.type === "withdraw"}
          error={orders.isError || journey.isError ? orderReadBlockedMessage : action.isError ? apiError(action.error) : undefined}
        >
          {selected.type === "withdraw" && (
            <label className="customer-order-form mt-4">
              <span>Lý do rút yêu cầu (ít nhất 10 ký tự)</span>
              <textarea
                className="input"
                rows={3}
                maxLength={2000}
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                placeholder="Chia sẻ ngắn gọn để NCC nắm được mong muốn của bạn..."
              />
              <small className="text-[#53637b]">
                {reason.trim().length}/2000 ký tự
              </small>
            </label>
          )}
        </OrderActionDialog>
      )}
      {detail && (
        <ComboOrderDetail
          order={detail}
          metadata={metadata.get(detail.id)}
          onClose={() => setDetail(null)}
          onPhoto={(title, sources) => setPhoto({ title, sources, index: 0 })}
        />
      )}
      {photo && (
        <ComboDialog title={photo.title} wide onClose={() => setPhoto(null)}>
          <div className="co-dialog-photo">
            <OrderPhoto
              sources={[photo.sources[photo.index]]}
              alt={`${photo.title} · ảnh ${photo.index + 1}`}
            />
          </div>
          <div className="co-photo-controls">
            <button
              className="co-button secondary"
              disabled={photo.sources.length < 2}
              onClick={() =>
                setPhoto(
                  (current) =>
                    current && {
                      ...current,
                      index:
                        (current.index - 1 + current.sources.length) %
                        current.sources.length,
                    },
                )
              }
              aria-label="Ảnh trước"
            >
              <ChevronLeft size={16} />
            </button>
            <span>
              {photo.index + 1}/{photo.sources.length}
            </span>
            <button
              className="co-button secondary"
              disabled={photo.sources.length < 2}
              onClick={() =>
                setPhoto(
                  (current) =>
                    current && {
                      ...current,
                      index: (current.index + 1) % current.sources.length,
                    },
                )
              }
              aria-label="Ảnh tiếp"
            >
              <ChevronRight size={16} />
            </button>
          </div>
        </ComboDialog>
      )}
    </section>
  );
}

function ComboOrderCard({
  order,
  focused,
  metadata,
  onView,
  onPhoto,
  onEdit,
  onAction,
}: {
  order: SavedCombo;
  focused?: boolean;
  metadata?: OrderJourneyItem;
  onView: () => void;
  onPhoto: (title: string, sources: string[]) => void;
  onEdit: () => void;
  onAction: (type: Exclude<ComboCardAction, "edit">) => void;
}) {
  const model = comboCardModel(order, metadata);
  const [expanded, setExpanded] = useState(false);
  const services = expanded ? model.items : model.items.slice(0, 2);
  return (
    <OrderCard
      id={`notification-combo-${order.id}`}
      tabIndex={-1}
      className={focused ? "notification-focus" : ""}
      imageSources={model.photos}
      onImageClick={onView}
      title={model.title}
      eyebrow={`${order.code} · ${date(order.createdAt)}`}
      subtitle={model.vendorName}
      status={model.displayStatus}
      meta={
        <>
          <span>
            <CalendarDays size={12} />
            {date(order.weddingDate)}
          </span>
          {order.region && (
            <span>
              <MapPin size={12} />
              {order.region}
            </span>
          )}
          {order.guestCount != null && (
            <span>
              <Users size={12} />
              {order.guestCount} khách
            </span>
          )}
        </>
      }
      footer={
        <>
          <div className="co-total">
            Tổng dự kiến sau ưu đãi<strong>{vnd(order.total)}</strong>
          </div>
          <div className="co-actions">
            <button className="co-button secondary" onClick={onView}>
              <Eye size={13} />
              Xem chi tiết
            </button>
            {model.actions.includes("edit") && (
              <button className="co-button primary" onClick={onEdit}>
                <Pencil size={13} />
                {order.status === "Draft" ? "Sửa bản nháp" : "Sửa yêu cầu"}
              </button>
            )}
            {model.actions.includes("withdraw") && (
              <button
                className="co-button secondary"
                onClick={() => onAction("withdraw")}
              >
                <XCircle size={13} />
                {order.status === "Draft" ? "Hủy bản nháp" : "Rút yêu cầu"}
              </button>
            )}
            {model.actions.includes("archive") && (
              <button
                className="co-button secondary"
                onClick={() => onAction("archive")}
              >
                <Archive size={13} />
                Lưu trữ
              </button>
            )}
            {model.actions.includes("restore") && (
              <button
                className="co-button secondary"
                onClick={() => onAction("restore")}
              >
                <RotateCcw size={13} />
                Khôi phục
              </button>
            )}
          </div>
        </>
      }
    >
      {services.map((item) => (
        <OrderServiceRow
          key={item.id}
          photoSources={item.photos}
          title={item.title}
          subtitle={
            <>
              {item.quantity} {unitName[item.unit] || item.unit}
              {order.status === "Draft" ? " · Chưa gửi NCC" : ""}
            </>
          }
          amount={vnd(item.price - item.discountShare)}
          onPhotoClick={
            item.photos.length
              ? () => onPhoto(item.title, item.photos)
              : undefined
          }
        />
      ))}
      {model.items.length > 2 && (
        <button
          className="co-detail-toggle"
          aria-expanded={expanded}
          onClick={() => setExpanded(!expanded)}
        >
          <ChevronDown size={13} />
          {expanded
            ? "Thu gọn dịch vụ"
            : `Xem thêm ${model.items.length - 2} dịch vụ`}
        </button>
      )}
      {metadata?.capabilities.blockedReason && !metadata.isArchived && (
        <p className="co-summary-note">
          <CircleHelp size={13} />
          <span>
            {metadata.capabilities.blockedReason}{" "}
            <Link to="/customer/dashboard?tab=contracts" className="underline">
              Xem hợp đồng & yêu cầu điều chỉnh
            </Link>
          </span>
        </p>
      )}
      {order.status !== "Draft" && <PrivateServiceAgreementPanel kind="combo" id={order.id} partnerName={model.vendorName} title={model.title} />}
      {order.status !== "Draft" &&
        !["Cancelled", "Expired", "Superseded", "Completed"].includes(
          order.status,
        ) &&
        !metadata?.isArchived && (
          <p className="co-summary-note">
            <ShoppingBag size={13} />
            <span>
              Tiếp theo: trao đổi với NCC và duyệt báo giá trong{" "}
              <Link
                className="underline"
                to="/customer/dashboard?tab=contracts"
              >
                Hợp đồng & tư vấn
              </Link>
              . Combo chưa phải hợp đồng hoặc thanh toán.
            </span>
          </p>
        )}
    </OrderCard>
  );
}

function ComboOrderDetail({
  order,
  metadata,
  onClose,
  onPhoto,
}: {
  order: SavedCombo;
  metadata?: OrderJourneyItem;
  onClose: () => void;
  onPhoto: (title: string, sources: string[]) => void;
}) {
  const model = comboCardModel(order, metadata);
  return (
    <ComboDialog title={model.title} wide onClose={onClose}>
      <div className="co-detail-content">
        <p className="co-eyebrow">
          {order.code} · {model.vendorName}
        </p>
        <OrderStatus status={model.displayStatus} />
        <dl className="co-detail-summary">
          <div>
            <dt>Ngày cưới</dt>
            <dd>{date(order.weddingDate)}</dd>
          </div>
          <div>
            <dt>Khu vực</dt>
            <dd>{order.region || "Chưa chọn"}</dd>
          </div>
          <div>
            <dt>Quy mô</dt>
            <dd>{order.guestCount ?? "—"} khách</dd>
          </div>
        </dl>
        {model.photos.length > 0 ? (
          <div className="co-detail-gallery">
            {model.photos.slice(0, 6).map((source, index) => (
              <button
                key={source}
                onClick={() =>
                  onPhoto(model.title, [
                    ...model.photos.slice(index),
                    ...model.photos.slice(0, index),
                  ])
                }
                aria-label={`Mở bộ ảnh combo, ảnh ${index + 1}`}
              >
                <OrderPhoto sources={[source]} alt={`Ảnh combo ${index + 1}`} />
              </button>
            ))}
          </div>
        ) : (
          <p className="co-summary-note">
            <ImageIcon size={14} />
            Chưa có ảnh được công bố cho các dịch vụ trong combo này.
          </p>
        )}
        <h4>Các dịch vụ đã chọn</h4>
        {model.items.map((item) => (
          <OrderServiceRow
            key={item.id}
            photoSources={item.photos}
            title={item.title}
            subtitle={`${item.quantity} ${unitName[item.unit] || item.unit} · ${item.vendorName}`}
            amount={vnd(item.price - item.discountShare)}
            onPhotoClick={
              item.photos.length
                ? () => onPhoto(item.title, item.photos)
                : undefined
            }
          >
            <details>
              <summary>Trạng thái & điều kiện dịch vụ</summary>
              <div className="mt-2">
                <OrderStatus
                  status={order.status === "Draft" ? "Draft" : item.status}
                />
                <p className="mt-2">
                  {item.cancellationPolicy ||
                    "Chính sách cụ thể cần được xác nhận trong báo giá và hợp đồng."}
                </p>
                {item.voucherCode && <p>Mã voucher: {item.voucherCode}</p>}
                <Link
                  to={`/listings/${item.listingId}`}
                  className="underline inline-flex items-center gap-1 mt-2"
                >
                  Xem bài dịch vụ
                  <ArrowRight size={12} />
                </Link>
              </div>
            </details>
          </OrderServiceRow>
        ))}
        <dl className="co-detail-summary">
          <div>
            <dt>Trước ưu đãi</dt>
            <dd>{vnd(order.subtotal)}</dd>
          </div>
          <div>
            <dt>Ưu đãi dự kiến</dt>
            <dd>−{vnd(order.discountAmount)}</dd>
          </div>
          <div>
            <dt>Tổng sau ưu đãi</dt>
            <dd>{vnd(order.total)}</dd>
          </div>
        </dl>
        <p>
          Tổng trên là dữ liệu đã lưu của yêu cầu, không tự trừ tiền. Giá cuối
          cùng theo báo giá và hợp đồng được bạn xác nhận.
        </p>
        {metadata?.supersededById && (
          <p className="co-summary-note">
            Yêu cầu này đã được thay bằng phiên bản mới. Lịch sử cũ được giữ để
            đối chiếu.
          </p>
        )}
        {!!metadata?.requests.length && (
          <>
            <h4>Lịch sử yêu cầu hỗ trợ</h4>
            <div className="customer-order-history">
              {metadata.requests.map((request) => (
                <div key={request.id}>
                  <OrderStatus status={request.status} />
                  <p>{request.reason}</p>
                  <small>{date(request.createdAt)}</small>
                </div>
              ))}
            </div>
          </>
        )}
        <div className="co-dialog-actions">
          <button className="co-button secondary" onClick={onClose}>
            Đóng chi tiết
          </button>
          {order.status !== "Draft" && (
            <Link
              className="co-button primary"
              to="/customer/dashboard?tab=contracts"
              onClick={onClose}
            >
              Trao đổi & xem báo giá
              <ArrowRight size={13} />
            </Link>
          )}
        </div>
      </div>
    </ComboDialog>
  );
}
