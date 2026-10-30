import { useId, useState } from "react";
import { Archive, Check, FileSignature, Pencil, RotateCcw } from "lucide-react";
import { apiError, useRespondQuote } from "../api";
import { type FinancialQuote } from "../financeApi";
import { vnd, vnDate } from "../format";
import {
  useCustomerOrderPage,
  type OrderJourneyItem,
} from "../customerOrders/customerOrderApi";
import {
  OrderActionDialog,
  OrderCard,
  OrderFilterBar,
  OrderServiceRow,
  OrderStatus,
} from "../customerOrders/OrderUi";
import {
  OrderGallery,
  type OrderGallerySelection,
} from "../customerOrders/OrderGallery";
import { orderMediaPhotos } from "../customerOrders/orderMedia";
import {
  groupCustomerOrders,
  orderCapability,
} from "../customerOrders/orderPresentation";
import {
  CustomerOrderActionDialog,
  type CustomerOrderIntent,
} from "../../customer/components/CustomerOrderActionDialog";
import { Pagination } from "../../../components/Pagination";
import { useOrderPage } from "../customerOrders/useOrderPage";
import { useNotificationFocus } from "../../notifications/useNotificationFocus";
import { OrderReadError, orderReadBlockedMessage } from "../customerOrders/OrderReadError";

export function MyQuotes({
  onOpenContracts,
}: {
  onOpenContracts?: () => void;
}) {
  const { page, pageSize, filter, setFilter, focusId, changePage, listRef } = useOrderPage("quote");
  const quotes = useCustomerOrderPage<FinancialQuote>("quote", page, pageSize, filter, focusId);
  const journey = { data: quotes.data?.metadata, isError: quotes.isError, error: quotes.error };
  const respond = useRespondQuote();
  const [dialog, setDialog] = useState<{
    item: OrderJourneyItem;
    intent: CustomerOrderIntent;
  } | null>(null);
  const [review, setReview] = useState<{
    quote: FinancialQuote;
    accept: boolean;
  } | null>(null);
  const [note, setNote] = useState("");
  const [notice, setNotice] = useState("");
  const [gallery, setGallery] = useState<OrderGallerySelection | null>(null);
  const noteId = useId();
  const metadata = new Map(
    (journey.data ?? [])
      .filter((item) => item.kind === "quote")
      .map((item) => [item.id, item]),
  );
  const rows = (quotes.data?.items ?? [])
    .map((q) => ({
      ...q,
      isArchived: metadata.get(q.id)?.isArchived,
      comboOrderId: metadata.get(q.id)?.comboOrderId,
    }));
  const groups = groupCustomerOrders(rows);
  const notificationFocus = useNotificationFocus("quote", rows.map(row => row.id), quotes.isSuccess && !quotes.isFetching);
  const reviewCaps = review ? metadata.get(review.quote.id)?.capabilities : undefined;
  const reviewBlocked = !!review && (quotes.isError || !orderCapability(reviewCaps, "canRespondQuote") ||
    (review.accept && reviewCaps?.canAcceptQuote === false));
  function startReview(quote: FinancialQuote, accept: boolean) {
    respond.reset();
    setNote("");
    setNotice("");
    setReview({ quote, accept });
  }
  async function submitReview() {
    if (!review || reviewBlocked || (!review.accept && !note.trim())) return;
    try {
      await respond.mutateAsync({
        id: review.quote.id,
        accept: review.accept,
        note: note.trim() || undefined,
      });
      setNotice(
        review.accept
          ? "Đã tạo hợp đồng chờ bạn kiểm tra và xác nhận. Chấp nhận báo giá không tự thực hiện thanh toán."
          : "Đã gửi lý do từ chối / đề nghị NCC lập báo giá mới. Bản báo giá cũ vẫn được giữ.",
      );
      const accepted = review.accept;
      setReview(null);
      if (accepted) onOpenContracts?.();
    } catch {
      /* Keep the dialog and note when the server rejects a stale quote. */
    }
  }
  return (
    <section className="co-section" ref={listRef} style={{ scrollMarginTop: "calc(var(--wm-header-offset, 88px) + 18px)" }}>
      <header className="co-section-heading">
        <div>
          <p className="co-eyebrow">XEM RÕ TRƯỚC KHI CHỐT</p>
          <h2>Báo giá từ nhà cung cấp</h2>
          <p>
            Giữ đầy đủ phiên bản để đối chiếu. Nếu chưa phù hợp, gửi lý do để
            NCC lập báo giá mới; không sửa trực tiếp giá do NCC gửi.
          </p>
        </div>
      </header>
      <OrderFilterBar
        label="Lọc báo giá"
        value={quotes.data?.filter ?? filter}
        onChange={setFilter}
        options={[
          { id: "all", label: "Tất cả" },
          {
            id: "pending",
            label: "Cần phản hồi",
          },
          { id: "closed", label: "Đã hết hiệu lực" },
          {
            id: "archived",
            label: "Lưu trữ",
          },
        ]}
      />
      {notice && (
        <p className="co-summary-note" role="status">
          <Check size={14} /> {notice}
        </p>
      )}
      {notificationFocus.missing && <p className="co-summary-note" role="status">Báo giá trong thông báo không còn khả dụng trong tài khoản này. <button className="co-button secondary" onClick={notificationFocus.clearFocus}>Xem danh sách</button></p>}
      {quotes.isError ? <OrderReadError label="báo giá" error={quotes.error} retry={quotes.refetch} busy={quotes.isFetching} /> : quotes.isPending ? (
        <div className="co-loading">
          <p>Đang tải báo giá...</p>
        </div>
      ) : !groups.length ? (
        <div className="co-empty">
          <FileSignature size={28} />
          <h3>Chưa có báo giá trong mục này</h3>
          <p>
            NCC sẽ gửi báo giá sau khi tiếp nhận yêu cầu tư vấn. Báo giá cũ hoặc
            lưu trữ không bị xóa.
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
                title={first.vendorName}
                eyebrow={
                  firstMeta?.comboOrderCode
                    ? `COMBO ${firstMeta.comboOrderCode}`
                    : "BÁO GIÁ DỊCH VỤ"
                }
                imageSources={[
                  firstMeta?.media.vendorCoverImageUrl,
                  firstMeta?.media.imageUrl,
                  firstMeta?.media.vendorLogoUrl,
                ]}
                subtitle={`${group.items.length} báo giá / phiên bản cùng NCC`}
              >
                {group.items.map((quote) => {
                  const item = metadata.get(quote.id);
                  const caps = journey.isError ? undefined : item?.capabilities;
                  const expired =
                    new Date(quote.validUntil).getTime() <= Date.now();
                  const canRespond =
                    orderCapability(caps, "canRespondQuote") &&
                    quote.status === "Sent" &&
                    !expired;
                  const title =
                    item?.media.items?.[0]?.title ||
                    item?.media.title ||
                    quote.code;
                  const photos = orderMediaPhotos(item?.media);
                  return (
                    <OrderServiceRow
                      key={quote.id}
                      id={`notification-quote-${quote.id}`}
                      tabIndex={-1}
                      className={focusId === quote.id ? "notification-focus" : ""}
                      title={title}
                      photoSources={photos}
                      onPhotoClick={
                        photos.length
                          ? () => setGallery({ title, sources: photos })
                          : undefined
                      }
                      subtitle={
                        <>
                          {quote.code} · Phiên bản {quote.version} ·{" "}
                          <OrderStatus status={quote.status} />
                        </>
                      }
                      amount={
                        <>
                          {vnd(quote.totalAmount)}
                          <br />
                          <small className="muted">Tổng báo giá</small>
                        </>
                      }
                    >
                      <div className="customer-order-facts">
                        <span>Hiệu lực đến {vnDate(quote.validUntil)}</span>
                        <span>Cọc đề nghị {vnd(quote.depositAmount)}</span>
                      </div>
                      <details className="customer-order-details">
                        <summary>Xem hạng mục & điều kiện báo giá</summary>
                        <div className="customer-order-history">
                          {quote.items.map((line, index) => (
                            <div key={index}>
                              <p>
                                <strong>{line.name}</strong>
                              </p>
                              <p>
                                {line.quantity.toLocaleString("vi-VN")} ×{" "}
                                {vnd(line.unitPrice)} ={" "}
                                {vnd(line.quantity * line.unitPrice)}
                              </p>
                            </div>
                          ))}
                          {!!quote.vatAmount && (
                            <p>
                              VAT {Math.round(quote.vatRate * 10000) / 100}%:{" "}
                              {vnd(quote.vatAmount)}
                            </p>
                          )}
                          <p>
                            <strong>Tổng cộng {vnd(quote.totalAmount)}</strong>
                          </p>
                          {quote.terms && <p>{quote.terms}</p>}
                        </div>
                      </details>
                      <div className="customer-order-actions">
                        {canRespond && (
                          <>
                            <button
                              className="co-button primary"
                              disabled={respond.isPending || caps?.canAcceptQuote === false}
                              onClick={() => startReview(quote, true)}
                            >
                              <FileSignature size={14} /> Chấp nhận & tạo hợp
                              đồng
                            </button>
                            <button
                              className="co-button secondary"
                              disabled={respond.isPending}
                              onClick={() => startReview(quote, false)}
                            >
                              <Pencil size={14} /> Chưa phù hợp / cần báo lại
                            </button>
                          </>
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
                        {quote.contractId && onOpenContracts && (
                          <button
                            className="co-button secondary"
                            onClick={onOpenContracts}
                          >
                            Xem hợp đồng liên quan
                          </button>
                        )}
                      </div>
                      {quote.contractId && (
                        <p className="co-summary-note">
                          Báo giá đã liên kết hợp đồng. Việc thay đổi tiếp theo
                          cần xử lý trên hợp đồng, không xóa báo giá cũ.
                        </p>
                      )}
                      {caps?.blockedReason && <p className="co-summary-note">{caps.blockedReason}</p>}
                      {quote.status === "Sent" && expired && (
                        <p className="co-summary-note">
                          Báo giá đã quá hạn. Đề nghị NCC gửi phiên bản còn hiệu
                          lực trước khi chấp nhận.
                        </p>
                      )}
                    </OrderServiceRow>
                  );
                })}
              </OrderCard>
            );
          })}
        </div>
      )}
      {!quotes.isError && <Pagination page={quotes.data?.page ?? page} pageSize={pageSize} totalCount={quotes.data?.totalCount ?? 0} isLoading={quotes.isFetching} onPageChange={next => changePage(next, quotes.data?.filter)} label="Phân trang nhóm báo giá" />}
      {gallery && (
        <OrderGallery {...gallery} onClose={() => setGallery(null)} />
      )}
      {dialog && (
        <CustomerOrderActionDialog
          key={`${dialog.item.id}:${dialog.intent}`}
          {...dialog}
          item={metadata.get(dialog.item.id) ?? dialog.item}
          readBlocked={quotes.isError || !metadata.has(dialog.item.id)}
          onClose={() => setDialog(null)}
          onSuccess={() =>
            setNotice(
              "Đã cập nhật lưu trữ hồ sơ. Thỏa thuận và lịch sử không thay đổi.",
            )
          }
        />
      )}
      {review && (
        <OrderActionDialog
          title={review.accept ? "Chấp nhận báo giá?" : "Đề nghị NCC báo lại"}
          description={
            review.accept
              ? "Hệ thống sẽ tạo hợp đồng chờ bạn kiểm tra. Chưa xác nhận hợp đồng và chưa tự thu tiền."
              : "Báo giá hiện tại sẽ được đánh dấu không phù hợp, vẫn giữ lịch sử. NCC cần gửi phiên bản mới để bạn duyệt lại."
          }
          confirmLabel={
            review.accept ? "Chấp nhận & tạo hợp đồng" : "Gửi phản hồi"
          }
          onClose={() => setReview(null)}
          onConfirm={() => void submitReview()}
          busy={respond.isPending}
          confirmDisabled={reviewBlocked || (!review.accept && !note.trim())}
          error={reviewBlocked ? (review.accept && reviewCaps?.blockedReason) || orderReadBlockedMessage : respond.isError ? apiError(respond.error) : ""}
        >
          <div className="customer-order-form">
            <p>
              <strong>
                {review.quote.vendorName} · {review.quote.code}
              </strong>
            </p>
            <p>
              Tổng {vnd(review.quote.totalAmount)} · Cọc đề nghị{" "}
              {vnd(review.quote.depositAmount)}
            </p>
            {review.quote.terms && (
              <p className="whitespace-pre-wrap">{review.quote.terms}</p>
            )}
            {!review.accept && (
              <label htmlFor={noteId}>
                <span className="label">
                  Thông tin chưa phù hợp / mong muốn điều chỉnh
                </span>
                <textarea
                  id={noteId}
                  className="input"
                  value={note}
                  onChange={(e) => setNote(e.target.value)}
                  maxLength={1000}
                  rows={4}
                  required
                />
              </label>
            )}
          </div>
        </OrderActionDialog>
      )}
    </section>
  );
}
