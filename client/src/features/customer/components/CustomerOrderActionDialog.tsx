import { useId, useState } from "react";
import type { Lead } from "../../../types/lead.types";
import { apiError } from "../../journey/api";
import { vnDateTime } from "../../journey/format";
import { OrderActionDialog } from "../../journey/customerOrders/OrderUi";
import {
  useCustomerOrderAction,
  type OrderJourneyItem,
} from "../../journey/customerOrders/customerOrderApi";
import { orderCapability } from "../../journey/customerOrders/orderPresentation";
import { orderReadBlockedMessage } from "../../journey/customerOrders/OrderReadError";

export type CustomerOrderIntent =
  | "edit"
  | "withdraw"
  | "stop-pending"
  | "archive"
  | "restore"
  | "change"
  | "cancellation";
const intentCopy: Record<
  CustomerOrderIntent,
  { title: string; confirm: string; capability: string; description: string }
> = {
  edit: {
    title: "Điều chỉnh yêu cầu tư vấn",
    confirm: "Gửi bản thay thế",
    capability: "canEdit",
    description:
      "Giữ yêu cầu cũ để đối chiếu; bản thay thế sẽ được NCC tiếp nhận lại. Đổi nhà cung cấp nên tạo yêu cầu mới từ trang dịch vụ.",
  },
  withdraw: {
    title: "Dừng yêu cầu tư vấn?",
    confirm: "Dừng tư vấn",
    capability: "canWithdraw",
    description:
      "NCC sẽ được thông báo bạn không tiếp tục. Lịch sử vẫn được giữ; thao tác này không xóa hợp đồng và không tự hoàn khoản tiền đã trả.",
  },
  "stop-pending": {
    title: "Dừng hợp đồng chờ xác nhận?",
    confirm: "Dừng chờ xác nhận",
    capability: "canStopPending",
    description:
      "Chỉ dừng quy trình đang chờ bạn xác nhận. Không phải lệnh hoàn cọc; khoản đã chuyển cần được hai bên đối chiếu riêng theo chính sách đã thống nhất.",
  },
  archive: {
    title: "Cất hồ sơ vào lưu trữ?",
    confirm: "Lưu trữ",
    capability: "canArchive",
    description:
      "Chỉ ẩn khỏi danh sách chính của bạn, không hủy dịch vụ hoặc xóa lịch sử. Có thể tìm lại và khôi phục tại mục Lưu trữ.",
  },
  restore: {
    title: "Khôi phục hồ sơ?",
    confirm: "Đưa về danh sách chính",
    capability: "canRestore",
    description:
      "Hồ sơ sẽ xuất hiện lại trong danh sách. Trạng thái hợp đồng, thanh toán và trao đổi giữ nguyên.",
  },
  change: {
    title: "Đề nghị điều chỉnh",
    confirm: "Gửi đề nghị thay đổi",
    capability: "canRequestChange",
    description:
      "Đây là đề nghị để NCC xem xét, chưa thay đổi giá, ngày cưới hoặc điều khoản hiện tại. Chỉ có hiệu lực sau khi các bên thống nhất và xác nhận lại.",
  },
  cancellation: {
    title: "Đề nghị hủy dịch vụ",
    confirm: "Gửi đề nghị hủy",
    capability: "canRequestCancellation",
    description:
      "Hợp đồng chưa tự hủy sau khi gửi. NCC và bộ phận hỗ trợ cần đối chiếu điều khoản, cọc thực tế và nghĩa vụ còn lại; không tự thu hoặc hoàn tiền.",
  },
};

export function CustomerOrderActionDialog({
  item,
  intent,
  lead,
  onClose,
  onSuccess,
  readBlocked = false,
}: {
  item: OrderJourneyItem;
  intent: CustomerOrderIntent;
  lead?: Lead;
  onClose: () => void;
  onSuccess?: () => void;
  readBlocked?: boolean;
}) {
  const id = useId();
  const action = useCustomerOrderAction();
  const [reason, setReason] = useState("");
  const [proposed, setProposed] = useState("");
  const [validation, setValidation] = useState("");
  const [form, setForm] = useState({
    phoneNumber: lead?.phoneNumber ?? "",
    weddingDate: lead?.weddingDate?.slice(0, 10) ?? "",
    estimatedGuests: lead?.estimatedGuests?.toString() ?? "",
    estimatedBudget: lead?.estimatedBudget?.toString() ?? "",
    notes: lead?.notes ?? "",
  });
  const copy = intentCopy[intent];
  const noReason = intent === "archive" || intent === "restore";
  const validLeadForm =
    /^\+?[\d]{9,20}$/.test(form.phoneNumber.trim()) &&
    (!form.estimatedGuests ||
      (Number.isInteger(Number(form.estimatedGuests)) &&
        Number(form.estimatedGuests) >= 1 &&
        Number(form.estimatedGuests) <= 10000)) &&
    (!form.estimatedBudget ||
      (Number.isFinite(Number(form.estimatedBudget)) &&
        Number(form.estimatedBudget) > 0 &&
        Number(form.estimatedBudget) <= 100000000000));
  async function submit() {
    setValidation("");
    if (readBlocked || !orderCapability(item.capabilities, copy.capability)) {
      setValidation("Thao tác không còn được phép. Hãy đóng và tải lại hồ sơ.");
      return;
    }
    if (!noReason && reason.trim().length < 10) {
      setValidation("Vui lòng nêu lý do ít nhất 10 ký tự.");
      return;
    }
    if (intent === "edit" && !validLeadForm) {
      setValidation(
        "Kiểm tra số liên hệ, số khách (1–10.000) và ngân sách dự kiến.",
      );
      return;
    }
    const body: Record<string, unknown> = noReason
      ? {}
      : { reason: reason.trim() };
    if (intent === "edit")
      Object.assign(body, {
        listingId: lead?.listingId,
        phoneNumber: form.phoneNumber.trim(),
        weddingDate: form.weddingDate || null,
        estimatedGuests: form.estimatedGuests
          ? Number(form.estimatedGuests)
          : null,
        estimatedBudget: form.estimatedBudget
          ? Number(form.estimatedBudget)
          : null,
        notes: form.notes.trim() || null,
      });
    if (intent === "change" || intent === "cancellation")
      Object.assign(body, {
        type: intent === "change" ? "Change" : "Cancellation",
        proposedChanges: proposed.trim() || null,
      });
    try {
      await action.mutateAsync({
        kind: item.kind,
        id: item.id,
        action:
          intent === "edit"
            ? "revise"
            : intent === "change" || intent === "cancellation"
              ? "requests"
              : intent,
        body,
      });
      onSuccess?.();
      onClose();
    } catch {
      /* The durable server response is shown below; keep the user's input. */
    }
  }
  return (
    <OrderActionDialog
      title={copy.title}
      description={copy.description}
      confirmLabel={copy.confirm}
      onConfirm={() => void submit()}
      onClose={onClose}
      busy={action.isPending}
      confirmDisabled={
        readBlocked || !orderCapability(item.capabilities, copy.capability) ||
        (!noReason && reason.trim().length < 10) ||
        (intent === "edit" && !validLeadForm)
      }
      danger={
        intent === "withdraw" ||
        intent === "stop-pending" ||
        intent === "cancellation"
      }
      error={readBlocked ? orderReadBlockedMessage : validation || (action.isError ? apiError(action.error) : "")}
    >
      <div className="customer-order-form">
        {intent === "edit" && (
          <>
            <p className="muted">
              Để trống thông tin tùy chọn sẽ giữ giá trị hiện tại trong bản thay
              thế.
            </p>
            <label htmlFor={`${id}-phone`}>
              <span className="label">Số điện thoại liên hệ</span>
              <input
                id={`${id}-phone`}
                className="input"
                autoComplete="tel"
                type="tel"
                maxLength={20}
                value={form.phoneNumber}
                onChange={(e) =>
                  setForm({ ...form, phoneNumber: e.target.value })
                }
              />
            </label>
            <label htmlFor={`${id}-date`}>
              <span className="label">Ngày cưới dự kiến</span>
              <input
                id={`${id}-date`}
                className="input"
                type="date"
                value={form.weddingDate}
                onChange={(e) =>
                  setForm({ ...form, weddingDate: e.target.value })
                }
              />
            </label>
            <label htmlFor={`${id}-guests`}>
              <span className="label">Số khách dự kiến</span>
              <input
                id={`${id}-guests`}
                className="input"
                type="number"
                min={1}
                max={10000}
                step={1}
                value={form.estimatedGuests}
                onChange={(e) =>
                  setForm({ ...form, estimatedGuests: e.target.value })
                }
              />
            </label>
            <label htmlFor={`${id}-budget`}>
              <span className="label">Ngân sách dự kiến (VNĐ)</span>
              <input
                id={`${id}-budget`}
                className="input"
                type="number"
                min={1}
                max={100000000000}
                value={form.estimatedBudget}
                onChange={(e) =>
                  setForm({ ...form, estimatedBudget: e.target.value })
                }
              />
            </label>
            <label htmlFor={`${id}-notes`}>
              <span className="label">Mong muốn của bạn</span>
              <textarea
                id={`${id}-notes`}
                className="input"
                rows={3}
                maxLength={1000}
                value={form.notes}
                onChange={(e) => setForm({ ...form, notes: e.target.value })}
              />
            </label>
          </>
        )}
        {!noReason && (
          <label htmlFor={`${id}-reason`}>
            <span className="label">
              Lý do <span aria-hidden="true">*</span>
            </span>
            <textarea
              id={`${id}-reason`}
              className="input"
              rows={3}
              minLength={10}
              maxLength={1000}
              required
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Chia sẻ lý do để NCC có thể hỗ trợ phù hợp..."
            />
          </label>
        )}
        {(intent === "change" || intent === "cancellation") && (
          <label htmlFor={`${id}-proposed`}>
            <span className="label">Phương án mong muốn (không bắt buộc)</span>
            <textarea
              id={`${id}-proposed`}
              className="input"
              rows={3}
              maxLength={4000}
              value={proposed}
              onChange={(e) => setProposed(e.target.value)}
              placeholder={
                intent === "change"
                  ? "Ngày mới, số khách, dịch vụ hoặc ngân sách mong muốn..."
                  : "Mong muốn đối chiếu cọc / thời điểm dừng dịch vụ..."
              }
            />
          </label>
        )}
      </div>
    </OrderActionDialog>
  );
}

export function CustomerOrderRequestHistory({
  item,
}: {
  item?: OrderJourneyItem;
}) {
  if (!item?.requests.length) return null;
  const statusLabels: Record<string, string> = {
    PendingVendor: "Chờ NCC phản hồi",
    PendingFinance: "Chờ đối chiếu tài chính",
    Acknowledged: "Đã tiếp nhận đề nghị",
    Rejected: "Không được chấp thuận",
  };
  return (
    <details className="customer-order-details">
      <summary>Lịch sử đề nghị ({item.requests.length})</summary>
      <div className="customer-order-history">
        {item.requests.map((request) => (
          <article key={request.id}>
            <p>
              <strong>
                {request.type === "Cancellation"
                  ? "Đề nghị hủy"
                  : "Đề nghị điều chỉnh"}
              </strong>{" "}
              · {statusLabels[request.status] || request.status}
            </p>
            <p className="muted text-xs">{vnDateTime(request.createdAt)}</p>
            <p className="whitespace-pre-wrap">{request.reason}</p>
            {request.proposedChanges && (
              <p className="whitespace-pre-wrap muted">
                Mong muốn: {request.proposedChanges}
              </p>
            )}
            {request.vendorResponse && (
              <p className="whitespace-pre-wrap">
                NCC: {request.vendorResponse}
              </p>
            )}
            {request.financeResponse && (
              <p className="whitespace-pre-wrap">
                Bộ phận tài chính: {request.financeResponse}
              </p>
            )}
            {request.requiresFinance && (
              <p className="muted">
                Cần đối chiếu khoản tiền thực tế; đề nghị này không tự thu hoặc
                hoàn cọc.
              </p>
            )}
          </article>
        ))}
      </div>
    </details>
  );
}
