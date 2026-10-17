import { useEffect, useId, useState } from "react";
import {
  Archive,
  CalendarDays,
  Check,
  ChevronLeft,
  ChevronRight,
  FileText,
  Pencil,
  RefreshCw,
  RotateCcw,
  Square,
} from "lucide-react";
import type { Contract } from "../../../types/contract.types";
import { useCustomerContracts } from "../hooks/useCustomerContracts";
import { apiError } from "../../journey/api";
import { vnd, vnDate, vnDateTime } from "../../journey/format";
import { ComboDialog } from "../../journey/combo/ComboDialog";
import { comboMediaSources } from "../../journey/combo/comboMedia";
import {
  OrderActionDialog,
  OrderCard,
  OrderFilterBar,
  OrderPhoto,
  OrderServiceRow,
  OrderStatus,
} from "../../journey/customerOrders/OrderUi";
import {
  OrderGallery,
  type OrderGallerySelection,
} from "../../journey/customerOrders/OrderGallery";
import { orderMediaPhotos } from "../../journey/customerOrders/orderMedia";
import {
  useCustomerOrderJourney,
  type OrderJourneyItem,
} from "../../journey/customerOrders/customerOrderApi";
import {
  groupCustomerOrders,
  matchesOrderFilter,
  orderCapability,
} from "../../journey/customerOrders/orderPresentation";
import {
  CustomerOrderActionDialog,
  CustomerOrderRequestHistory,
  type CustomerOrderIntent,
} from "./CustomerOrderActionDialog";

export function MyContracts() {
  const [page, setPage] = useState(1);
  const {
    contracts,
    isLoading,
    isError,
    error,
    refetch,
    confirmContract,
    isConfirming,
    rejectContract,
    isRejecting,
  } = useCustomerContracts(page, 50);
  const journey = useCustomerOrderJourney();
  const [filter, setFilter] = useState("all");
  const [now, setNow] = useState(() => Date.now());
  const [dialog, setDialog] = useState<{
    item: OrderJourneyItem;
    intent: CustomerOrderIntent;
  } | null>(null);
  const [verification, setVerification] = useState<{
    contract: Contract;
    type: "confirm" | "reject";
  } | null>(null);
  const [reason, setReason] = useState("");
  const [actionError, setActionError] = useState("");
  const [notice, setNotice] = useState("");
  const [gallery, setGallery] = useState<OrderGallerySelection | null>(null);
  const [preview, setPreview] = useState<Contract | null>(null);
  const reasonId = useId();
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 30000);
    return () => clearInterval(timer);
  }, []);
  const metadata = new Map(
    (journey.data ?? [])
      .filter((item) => item.kind === "contract")
      .map((item) => [item.id, item]),
  );
  const rows = contracts.map((contract) => ({
    ...contract,
    isArchived: metadata.get(contract.id)?.isArchived,
    comboOrderId: metadata.get(contract.id)?.comboOrderId,
  }));
  const groups = groupCustomerOrders(
    rows.filter((row) => matchesOrderFilter(row, filter)),
  );
  function showVerification(contract: Contract, type: "confirm" | "reject") {
    setReason("");
    setActionError("");
    setNotice("");
    setVerification({ contract, type });
  }
  async function submitVerification() {
    if (!verification) return;
    if (verification.type === "reject" && !reason.trim()) {
      setActionError("Vui lòng chỉ rõ thông tin cần NCC điều chỉnh.");
      return;
    }
    setActionError("");
    try {
      if (verification.type === "confirm") {
        await confirmContract(verification.contract.id);
        setNotice(
          "Đã xác nhận hợp đồng. Ngân sách và công việc liên quan được cập nhật theo hệ thống.",
        );
      } else {
        await rejectContract({
          contractId: verification.contract.id,
          reason: reason.trim(),
        });
        setNotice(
          "Đã trả hợp đồng về bản nháp để NCC sửa và gửi bạn xác nhận lại.",
        );
      }
      setVerification(null);
    } catch (e) {
      setActionError(apiError(e));
    }
  }
  const previewUrl = comboMediaSources([preview?.contractImageUrl])[0];
  return (
    <section className="co-section">
      <header className="co-section-heading">
        <div>
          <p className="co-eyebrow">THỎA THUẬN RÕ RÀNG, NGÀY VUI AN TÂM</p>
          <h2>Hợp đồng dịch vụ</h2>
          <p>
            Kiểm tra giá trị, cọc và chính sách hủy trước khi xác nhận. Sau khi
            chốt, hãy gửi đề nghị thay đổi thay vì sửa trực tiếp thỏa thuận đã
            ký.
          </p>
        </div>
        <button
          className="co-button secondary"
          onClick={() => {
            void refetch();
            void journey.refetch();
          }}
        >
          <RefreshCw size={14} /> Làm mới
        </button>
      </header>
      <OrderFilterBar
        label="Lọc hợp đồng"
        value={filter}
        onChange={setFilter}
        options={[
          { id: "all", label: "Tất cả" },
          {
            id: "pending",
            label: "Cần kiểm tra",
            count: rows.filter((r) => matchesOrderFilter(r, "pending")).length,
          },
          { id: "confirmed", label: "Đang hiệu lực" },
          { id: "closed", label: "Đã kết thúc" },
          {
            id: "archived",
            label: "Lưu trữ",
            count: rows.filter((r) => matchesOrderFilter(r, "archived")).length,
          },
        ]}
      />
      {notice && (
        <p className="co-summary-note" role="status">
          {notice}
        </p>
      )}
      {(isError || journey.isError) && (
        <p className="co-error" role="alert">
          {apiError(error || journey.error)}{" "}
          {journey.isError &&
            "Chưa thể xác định quyền thao tác. Vui lòng làm mới."}
        </p>
      )}
      {isLoading ? (
        <div className="co-loading">
          <p>Đang tải hợp đồng...</p>
        </div>
      ) : !groups.length ? (
        <div className="co-empty">
          <FileText size={28} />
          <h3>Chưa có hợp đồng trong mục này</h3>
          <p>
            Khi hai bên thống nhất dịch vụ, NCC sẽ gửi hợp đồng để bạn kiểm tra.
            Hồ sơ đã lưu trữ vẫn có thể khôi phục.
          </p>
        </div>
      ) : (
        <div className="co-card-list">
          {groups.map((group) => {
            const first = group.items[0];
            const firstMeta = metadata.get(first.id);
            return (
              <OrderCard
                key={group.key}
                title={first.vendorBrandName}
                eyebrow={
                  firstMeta?.comboOrderCode
                    ? `COMBO ${firstMeta.comboOrderCode}`
                    : "HỢP ĐỒNG DỊCH VỤ"
                }
                imageSources={[
                  firstMeta?.media.vendorCoverImageUrl,
                  firstMeta?.media.imageUrl,
                  firstMeta?.media.vendorLogoUrl,
                ]}
                subtitle={`${group.items.length} hợp đồng · Mỗi thỏa thuận được xác nhận riêng`}
                status={
                  group.items.length === 1 ? (
                    first.status
                  ) : (
                    <OrderStatus
                      status="InProgress"
                      label="Theo dõi từng dịch vụ"
                    />
                  )
                }
              >
                {group.items.map((contract) => {
                  const item = metadata.get(contract.id);
                  const caps = journey.isError ? undefined : item?.capabilities;
                  const pending = contract.status === "PendingVerification";
                  const expired =
                    new Date(contract.verificationDeadline).getTime() <= now;
                  const title =
                    item?.media.items?.[0]?.title ||
                    item?.media.title ||
                    contract.contractCode;
                  const photos = orderMediaPhotos(item?.media);
                  return (
                    <OrderServiceRow
                      key={contract.id}
                      title={title}
                      photoSources={photos}
                      onPhotoClick={
                        photos.length
                          ? () => setGallery({ title, sources: photos })
                          : undefined
                      }
                      subtitle={
                        <>
                          {contract.contractCode} ·{" "}
                          <OrderStatus status={contract.status} />
                        </>
                      }
                      amount={
                        <>
                          {vnd(contract.contractValue)}
                          <br />
                          <small className="muted">Giá trị HĐ</small>
                        </>
                      }
                    >
                      {pending && (
                        <p className="co-summary-note">
                          {expired
                            ? "Đã quá hạn xác nhận; chờ hệ thống cập nhật trạng thái."
                            : `Vui lòng kiểm tra trước ${vnDateTime(contract.verificationDeadline)}.`}
                        </p>
                      )}
                      <div className="customer-order-actions">
                        {orderCapability(caps, "canConfirm") && (
                          <button
                            className="co-button primary"
                            disabled={expired || isConfirming}
                            onClick={() =>
                              showVerification(contract, "confirm")
                            }
                          >
                            <Check size={14} /> Xác nhận hợp đồng
                          </button>
                        )}
                        {orderCapability(caps, "canRequestChanges") && (
                          <button
                            className="co-button secondary"
                            disabled={isRejecting}
                            onClick={() => showVerification(contract, "reject")}
                          >
                            <Pencil size={14} /> Báo sai lệch / cần sửa
                          </button>
                        )}
                        {orderCapability(caps, "canStopPending") && item && (
                          <button
                            className="co-button danger"
                            onClick={() =>
                              setDialog({ item, intent: "stop-pending" })
                            }
                          >
                            <Square size={13} /> Dừng chờ xác nhận
                          </button>
                        )}
                        {orderCapability(caps, "canRequestChange") &&
                          !orderCapability(caps, "canRequestChanges") &&
                          item && (
                            <button
                              className="co-button secondary"
                              onClick={() =>
                                setDialog({ item, intent: "change" })
                              }
                            >
                              <Pencil size={14} /> Đề nghị điều chỉnh
                            </button>
                          )}
                        {orderCapability(caps, "canRequestCancellation") &&
                          item && (
                            <button
                              className="co-button danger"
                              onClick={() =>
                                setDialog({ item, intent: "cancellation" })
                              }
                            >
                              Đề nghị hủy
                            </button>
                          )}
                        {orderCapability(caps, "canArchive") && item && (
                          <button
                            className="co-button secondary"
                            onClick={() =>
                              setDialog({ item, intent: "archive" })
                            }
                          >
                            <Archive size={14} /> Lưu trữ
                          </button>
                        )}
                        {orderCapability(caps, "canRestore") && item && (
                          <button
                            className="co-button secondary"
                            onClick={() =>
                              setDialog({ item, intent: "restore" })
                            }
                          >
                            <RotateCcw size={14} /> Khôi phục
                          </button>
                        )}
                      </div>
                      <details className="customer-order-details">
                        <summary>Điều khoản, cọc & chứng từ</summary>
                        <div className="customer-order-facts">
                          <span>
                            <CalendarDays size={13} /> Ngày cưới{" "}
                            {vnDate(contract.weddingDate)}
                          </span>
                          <span>Tạo {vnDate(contract.createdAt)}</span>
                          <span>Bản sửa {contract.revisionCount ?? 0}</span>
                        </div>
                        <dl className="co-detail-summary">
                          <div>
                            <dt>Giá trị hợp đồng</dt>
                            <dd>{vnd(contract.contractValue)}</dd>
                          </div>
                          <div>
                            <dt>Cọc ghi trong hợp đồng</dt>
                            <dd>{vnd(contract.depositAmount)}</dd>
                          </div>
                          <div>
                            <dt>Giá trị còn lại theo HĐ</dt>
                            <dd>
                              {vnd(
                                Math.max(
                                  0,
                                  contract.contractValue -
                                    contract.depositAmount,
                                ),
                              )}
                            </dd>
                          </div>
                        </dl>
                        <p className="co-summary-note">
                          Số cọc trên hợp đồng không thay thế xác minh tiền thực
                          nhận. Đối chiếu phiếu thu với NCC trước khi chuyển
                          thêm.
                        </p>
                        {!!contract.appointmentDepositCredit && (
                          <p>
                            Đã tính cọc lịch hẹn{" "}
                            {vnd(contract.appointmentDepositCredit)}; cọc HĐ bổ
                            sung theo số ghi tối đa{" "}
                            {vnd(
                              Math.max(
                                0,
                                contract.depositAmount -
                                  contract.appointmentDepositCredit,
                              ),
                            )}
                            . Không chuyển lại khoản đã cọc lịch.
                          </p>
                        )}
                        <h4 className="font-semibold mt-3">
                          Chính sách hủy / hoàn cọc đã chốt
                        </h4>
                        <p className="whitespace-pre-wrap mt-2">
                          {contract.cancellationPolicySnapshot ||
                            "Vui lòng đối chiếu văn bản hợp đồng đã thống nhất với NCC."}
                        </p>
                        {contract.rejectReason && (
                          <p className="co-summary-note">
                            Thông tin cần sửa: {contract.rejectReason}
                          </p>
                        )}
                        {contract.cancellationReason && (
                          <p className="co-summary-note">
                            Lý do dừng / hủy: {contract.cancellationReason}
                          </p>
                        )}
                        {caps?.blockedReason && (
                          <p className="co-summary-note">
                            {caps.blockedReason}
                          </p>
                        )}
                        {contract.voucherCode && (
                          <p className="co-summary-note">
                            Mã ưu đãi liên quan: {contract.voucherCode}. Giá trị
                            cuối cùng theo thỏa thuận đã xác nhận, không tự trừ
                            thêm lần nữa.
                          </p>
                        )}
                        {contract.contractImageUrl ? (
                          <button
                            className="co-button secondary"
                            onClick={() => setPreview(contract)}
                          >
                            Xem chứng từ đính kèm
                          </button>
                        ) : (
                          <p className="co-summary-note">
                            NCC chưa đính kèm ảnh chứng từ.
                          </p>
                        )}
                      </details>
                      <CustomerOrderRequestHistory item={item} />
                    </OrderServiceRow>
                  );
                })}
              </OrderCard>
            );
          })}
        </div>
      )}
      {(page > 1 || contracts.length === 50) && (
        <nav className="customer-order-pagination" aria-label="Trang hợp đồng">
          <button
            className="co-button secondary"
            aria-label="Trang trước"
            disabled={page === 1}
            onClick={() => setPage((p) => p - 1)}
          >
            <ChevronLeft size={15} />
          </button>
          <span>Trang {page} · Lọc trong trang hiện tại</span>
          <button
            className="co-button secondary"
            aria-label="Trang tiếp"
            disabled={contracts.length < 50}
            onClick={() => setPage((p) => p + 1)}
          >
            <ChevronRight size={15} />
          </button>
        </nav>
      )}
      {gallery && (
        <OrderGallery {...gallery} onClose={() => setGallery(null)} />
      )}
      {dialog && (
        <CustomerOrderActionDialog
          key={`${dialog.item.id}:${dialog.intent}`}
          {...dialog}
          onClose={() => setDialog(null)}
          onSuccess={() =>
            setNotice(
              "Đã ghi nhận thao tác. Đề nghị thay đổi / hủy chưa tự điều chỉnh hợp đồng hoặc thu, hoàn tiền.",
            )
          }
        />
      )}
      {verification && (
        <OrderActionDialog
          title={
            verification.type === "confirm"
              ? "Xác nhận thỏa thuận với NCC"
              : "Yêu cầu NCC sửa hợp đồng"
          }
          confirmLabel={
            verification.type === "confirm"
              ? "Tôi đồng ý & xác nhận"
              : "Trả về để NCC sửa"
          }
          onConfirm={() => void submitVerification()}
          onClose={() => setVerification(null)}
          busy={isConfirming || isRejecting}
          confirmDisabled={verification.type === "reject" && !reason.trim()}
          error={actionError}
          description={
            verification.type === "confirm"
              ? "Chỉ xác nhận sau khi đã kiểm tra giá, cọc, ngày cưới và chính sách hủy. Đây không phải lệnh thanh toán."
              : "NCC sẽ nhận lại bản nháp để sửa và gửi bạn xác nhận lại. Không dùng thao tác này thay cho đề nghị hủy."
          }
        >
          <div className="customer-order-form">
            <p>
              <strong>{verification.contract.contractCode}</strong> ·{" "}
              {verification.contract.vendorBrandName}
            </p>
            <p>
              Giá trị {vnd(verification.contract.contractValue)} · Cọc ghi{" "}
              {vnd(verification.contract.depositAmount)}
            </p>
            <p className="whitespace-pre-wrap">
              {verification.contract.cancellationPolicySnapshot ||
                "Hãy đối chiếu chính sách hủy trong hợp đồng với NCC trước khi xác nhận."}
            </p>
            {verification.type === "reject" && (
              <label htmlFor={reasonId}>
                <span className="label">Thông tin cần điều chỉnh</span>
                <textarea
                  id={reasonId}
                  className="input"
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  rows={4}
                  maxLength={500}
                  required
                />
              </label>
            )}
          </div>
        </OrderActionDialog>
      )}
      {preview && (
        <ComboDialog
          title={`Chứng từ · ${preview.contractCode}`}
          wide
          onClose={() => setPreview(null)}
        >
          <div className="co-dialog-photo">
            <OrderPhoto
              sources={[preview.contractImageUrl]}
              alt={`Chứng từ của hợp đồng ${preview.contractCode}`}
            />
          </div>
          {previewUrl && (
            <a
              className="co-button secondary mt-3"
              href={previewUrl}
              target="_blank"
              rel="noopener noreferrer"
            >
              Mở ảnh gốc
            </a>
          )}
        </ComboDialog>
      )}
    </section>
  );
}
export default MyContracts;
