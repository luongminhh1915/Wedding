import { useState } from "react";
import { MessageSquareText, RefreshCw } from "lucide-react";
import { apiError } from "../api";
import { vnDate } from "../format";
import { OrderActionDialog, OrderCard, OrderFilterBar } from "./OrderUi";
import {
  useOrderRequestInbox,
  useReviewOrderRequest,
  type OrderChangeRequest,
} from "./customerOrderApi";
import { useNotificationFocus } from "../../notifications/useNotificationFocus";

const labels: Record<string, string> = {
  PendingVendor: "Chờ NCC tiếp nhận",
  PendingFinance: "Chờ bộ phận tài chính",
  Acknowledged: "Đã tiếp nhận xử lý",
  Rejected: "Không tiếp nhận",
};
export function OrderRequestInbox({
  audience,
}: {
  audience: "vendor" | "finance";
}) {
  const query = useOrderRequestInbox(audience);
  const review = useReviewOrderRequest(audience);
  const [filter, setFilter] = useState("pending");
  const [selected, setSelected] = useState<{
    request: OrderChangeRequest;
    accept: boolean;
  } | null>(null);
  const [response, setResponse] = useState("");
  const pendingStatus =
    audience === "vendor" ? "PendingVendor" : "PendingFinance";
  const requests = query.data ?? [];
  const { focusId, missing, clearFocus } = useNotificationFocus("order-request", requests.map(request => request.id), !query.isPending && !query.isError);
  const pending = requests.filter(
    (request) => request.status === pendingStatus,
  );
  const visible = filter === "pending" ? requests.filter(request => request.id === focusId || request.status === pendingStatus) : requests;
  const choose = (request: OrderChangeRequest, accept: boolean) => {
    review.reset();
    setResponse("");
    setSelected({ request, accept });
  };
  const send = () => {
    if (!selected || response.trim().length < 10) return;
    review.mutate(
      {
        id: selected.request.id,
        accept: selected.accept,
        response: response.trim(),
      },
      {
        onSuccess: () => {
          setSelected(null);
          setResponse("");
        },
      },
    );
  };
  return (
    <section className="co-section">
      <header className="co-section-heading">
        <div>
          <p className="co-eyebrow">PHỐI HỢP VỚI CẶP ĐÔI</p>
          <h2>Yêu cầu thay đổi & hủy</h2>
          <p>
            Tiếp nhận đề nghị của khách, trao đổi phạm vi và cập nhật chứng từ
            theo quy trình. Thao tác ở đây không sửa hợp đồng, hủy dịch vụ hoặc
            chuyển / hoàn tiền.
          </p>
        </div>
        <button
          className="co-button secondary"
          onClick={() => void query.refetch()}
        >
          <RefreshCw size={14} />
          Làm mới
        </button>
      </header>
      <OrderFilterBar
        value={filter}
        onChange={value => { setFilter(value); clearFocus(); }}
        options={[
          { id: "pending", label: "Cần phản hồi", count: pending.length },
          { id: "all", label: "Lịch sử", count: requests.length },
        ]}
      />
      {missing && <p role="status" className="co-summary-note">Đề nghị trong thông báo không còn khả dụng trong tài khoản của bạn.</p>}
      {query.isPending ? (
        <p role="status" className="co-loading">
          Đang tải yêu cầu...
        </p>
      ) : query.isError ? (
        <p role="alert" className="co-error">
          {apiError(query.error)}
        </p>
      ) : !visible.length ? (
        <div className="co-empty">
          <MessageSquareText size={30} />
          <h3>Chưa có yêu cầu trong mục này</h3>
          <p>
            Đề nghị thay đổi hoặc hủy của khách sẽ hiển thị tại đây để bạn phản
            hồi.
          </p>
        </div>
      ) : (
        <div className="co-card-list">
          {visible.map((request) => (
            <div key={request.id} id={`notification-order-request-${request.id}`} tabIndex={-1} data-notification-focus={focusId === request.id}>
            <OrderCard
              title={
                request.type === "Cancellation"
                  ? "Đề nghị hủy dịch vụ"
                  : "Đề nghị điều chỉnh dịch vụ"
              }
              eyebrow={`Mã tham chiếu ${request.entityId.slice(0, 8)}`}
              status={
                <span className="co-status">
                  {labels[request.status] ?? request.status}
                </span>
              }
              meta={<span>{vnDate(request.createdAt)}</span>}
              footer={
                request.status === pendingStatus && (
                  <div className="co-actions">
                    <button
                      className="co-button secondary"
                      onClick={() => choose(request, false)}
                    >
                      Không tiếp nhận
                    </button>
                    <button
                      className="co-button primary"
                      onClick={() => choose(request, true)}
                    >
                      Tiếp nhận xử lý
                    </button>
                  </div>
                )
              }
            >
              <div className="customer-order-details">
                <strong>Nội dung khách đề nghị</strong>
                <p className="whitespace-pre-wrap break-words">
                  {request.reason}
                </p>
                {request.proposedChanges && (
                  <p className="whitespace-pre-wrap break-words">
                    Mong muốn: {request.proposedChanges}
                  </p>
                )}
              </div>
              {(request.vendorResponse || request.financeResponse) && (
                <div className="customer-order-history">
                  {request.vendorResponse && (
                    <p>NCC: {request.vendorResponse}</p>
                  )}
                  {request.financeResponse && (
                    <p>Tài chính: {request.financeResponse}</p>
                  )}
                </div>
              )}
              {request.requiresFinance && (
                <p className="co-summary-note">
                  Đề nghị có ràng buộc hợp đồng / cọc. Sau khi NCC tiếp nhận, bộ
                  phận tài chính cần đối chiếu; không tự động hoàn tiền.
                </p>
              )}
            </OrderCard>
            </div>
          ))}
        </div>
      )}
      {selected && (
        <OrderActionDialog
          title={
            selected.accept
              ? "Tiếp nhận đề nghị của khách"
              : "Phản hồi không tiếp nhận"
          }
          description={
            <p>
              {selected.accept
                ? "Chỉ xác nhận bạn đã tiếp nhận để phối hợp xử lý. Nếu đổi điều khoản, hãy gửi bản sửa để khách duyệt. Nếu hủy, cần xác minh riêng hợp đồng và cọc theo quy trình tài chính."
                : "Khách sẽ xem được lý do phản hồi. Hợp đồng và các khoản đã ghi nhận không thay đổi."}
            </p>
          }
          confirmLabel={
            selected.accept ? "Gửi phản hồi tiếp nhận" : "Gửi lý do"
          }
          onClose={() => setSelected(null)}
          onConfirm={send}
          busy={review.isPending}
          confirmDisabled={response.trim().length < 10}
          error={review.isError ? apiError(review.error) : undefined}
        >
          <div className="customer-order-form">
            <label>
              Phản hồi cho khách (ít nhất 10 ký tự)
              <textarea
                value={response}
                onChange={(event) => setResponse(event.target.value)}
                maxLength={2000}
                rows={4}
              />
            </label>
          </div>
        </OrderActionDialog>
      )}
    </section>
  );
}
