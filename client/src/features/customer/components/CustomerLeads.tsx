import { useState } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Archive,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  Gift,
  MessageCircle,
  Pencil,
  RefreshCw,
  RotateCcw,
  Square,
  Unlock,
  Users,
} from "lucide-react";
import axiosClient from "../../../services/axiosClient";
import { useAuthStore } from "../../../store/authStore";
import type { Lead } from "../../../types/lead.types";
import { ChatRoomModal } from "../../chat/components/ChatRoomModal";
import { apiError } from "../../journey/api";
import { vnd, vnDate } from "../../journey/format";
import {
  useCustomerOrderJourney,
  type OrderJourneyItem,
} from "../../journey/customerOrders/customerOrderApi";
import {
  OrderCard,
  OrderFilterBar,
  OrderServiceRow,
  OrderStatus,
} from "../../journey/customerOrders/OrderUi";
import {
  OrderGallery,
  type OrderGallerySelection,
} from "../../journey/customerOrders/OrderGallery";
import { orderMediaPhotos } from "../../journey/customerOrders/orderMedia";
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

export function CustomerLeads() {
  const user = useAuthStore((state) => state.user);
  const [page, setPage] = useState(1);
  const [filter, setFilter] = useState("active");
  const [chatLead, setChatLead] = useState<Lead | null>(null);
  const [dialog, setDialog] = useState<{
    item: OrderJourneyItem;
    intent: CustomerOrderIntent;
    lead: Lead;
  } | null>(null);
  const [notice, setNotice] = useState("");
  const [gallery, setGallery] = useState<OrderGallerySelection | null>(null);
  const queryClient = useQueryClient();
  const journey = useCustomerOrderJourney();
  const query = useQuery<Lead[]>({
    queryKey: ["customer-leads", user?.id, page],
    enabled: user?.role === "Customer",
    queryFn: async () =>
      (
        await axiosClient.get("/api/leads/my-leads", {
          params: { pageNumber: page, pageSize: 50 },
        })
      ).data,
  });
  const unlock = useMutation({
    mutationFn: async (id: string) =>
      axiosClient.post(`/api/leads/${id}/unlock-phone`),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["customer-leads"] }),
  });
  const metadata = new Map(
    (journey.data ?? [])
      .filter((item) => item.kind === "lead")
      .map((item) => [item.id, item]),
  );
  const rows = (query.data ?? []).map((lead) => ({
    ...lead,
    isArchived: metadata.get(lead.id)?.isArchived,
    comboOrderId: metadata.get(lead.id)?.comboOrderId,
  }));
  const groups = groupCustomerOrders(
    rows.filter((row) => matchesOrderFilter(row, filter)),
  );
  const show = (lead: Lead, intent: CustomerOrderIntent) => {
    const item = metadata.get(lead.id);
    if (item) {
      setNotice("");
      setDialog({ item, intent, lead });
    }
  };
  return (
    <section className="co-section">
      <header className="co-section-heading">
        <div>
          <p className="co-eyebrow">TỪ MONG MUỐN ĐẾN LỰA CHỌN</p>
          <h2>Yêu cầu tư vấn</h2>
          <p>
            Các dịch vụ cùng NCC và cùng combo được gom lại. Mở chi tiết để xem
            mong muốn, mã ưu đãi và thao tác phù hợp.
          </p>
        </div>
        <button
          className="co-button secondary"
          onClick={() => {
            void query.refetch();
            void journey.refetch();
          }}
        >
          <RefreshCw size={14} /> Làm mới
        </button>
      </header>
      <OrderFilterBar
        label="Lọc yêu cầu tư vấn"
        value={filter}
        onChange={setFilter}
        options={[
          {
            id: "active",
            label: "Đang xử lý",
            count: rows.filter((r) => matchesOrderFilter(r, "active")).length,
          },
          {
            id: "closed",
            label: "Đã dừng / hết hạn",
            count: rows.filter((r) => matchesOrderFilter(r, "closed")).length,
          },
          { id: "all", label: "Tất cả" },
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
      {(query.isError || journey.isError || unlock.isError) && (
        <p className="co-error" role="alert">
          {apiError(query.error || journey.error || unlock.error)}{" "}
          {journey.isError &&
            "Chưa tải được quyền thao tác; các nút thay đổi đang được ẩn an toàn."}
        </p>
      )}
      {query.isPending ? (
        <div className="co-loading">
          <p>Đang tải yêu cầu tư vấn...</p>
        </div>
      ) : !groups.length ? (
        <div className="co-empty">
          <h3>
            {filter === "archived"
              ? "Chưa có hồ sơ lưu trữ"
              : "Chưa có yêu cầu trong mục này"}
          </h3>
          <p>Yêu cầu đã dừng và bản thay thế vẫn được giữ trong lịch sử.</p>
          <Link className="co-button primary" to="/browse">
            Khám phá dịch vụ cưới
          </Link>
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
                    : "TƯ VẤN DỊCH VỤ"
                }
                imageSources={[
                  firstMeta?.media.vendorCoverImageUrl,
                  firstMeta?.media.imageUrl,
                  firstMeta?.media.vendorLogoUrl,
                ]}
                status={
                  group.items.length === 1 ? (
                    <OrderStatus status={first.status} />
                  ) : (
                    <OrderStatus
                      status="InProgress"
                      label={`${group.items.length} yêu cầu cùng NCC`}
                    />
                  )
                }
                subtitle="Mỗi dịch vụ có lịch sử và quyền xử lý riêng."
              >
                {group.items.map((lead) => {
                  const item = metadata.get(lead.id);
                  const caps = journey.isError ? undefined : item?.capabilities;
                  const title =
                    item?.media.items?.[0]?.title ||
                    item?.media.title ||
                    lead.listingTitle ||
                    "Tư vấn dịch vụ cưới";
                  const photos = orderMediaPhotos(item?.media);
                  return (
                    <OrderServiceRow
                      key={lead.id}
                      title={title}
                      photoSources={photos}
                      onPhotoClick={
                        photos.length
                          ? () => setGallery({ title, sources: photos })
                          : undefined
                      }
                      subtitle={
                        <>
                          <OrderStatus status={lead.status} /> · Gửi{" "}
                          {vnDate(lead.createdAt)}
                        </>
                      }
                    >
                      <details className="customer-order-details">
                        <summary>Xem mong muốn & thao tác</summary>
                        <div className="customer-order-facts">
                          {lead.weddingDate && (
                            <span>
                              <CalendarDays size={13} />{" "}
                              {vnDate(lead.weddingDate)}
                            </span>
                          )}
                          {lead.estimatedGuests != null && (
                            <span>
                              <Users size={13} />{" "}
                              {lead.estimatedGuests.toLocaleString("vi-VN")}{" "}
                              khách
                            </span>
                          )}
                          {lead.estimatedBudget != null && (
                            <span>
                              Ngân sách dự kiến {vnd(lead.estimatedBudget)}
                            </span>
                          )}
                        </div>
                        {lead.notes && (
                          <p className="whitespace-pre-wrap my-3">
                            {lead.notes}
                          </p>
                        )}
                        {lead.voucher && (
                          <p className="co-summary-note">
                            <Gift size={14} />{" "}
                            <span>
                              Mã {lead.voucher.code} · Hạn{" "}
                              {vnDate(lead.voucher.expiresAt)} ·{" "}
                              {lead.voucher.status === "Active"
                                ? "Chưa sử dụng"
                                : lead.voucher.status === "Redeemed"
                                  ? "Đã sử dụng"
                                  : "Đã hết hiệu lực"}
                            </span>
                          </p>
                        )}
                        {item?.supersededById && (
                          <p className="co-summary-note">
                            Yêu cầu này đã có bản thay thế. Lịch sử cũ không bị
                            xóa.
                          </p>
                        )}
                        {caps?.blockedReason && (
                          <p className="co-summary-note">
                            {caps.blockedReason}
                          </p>
                        )}
                        <div className="customer-order-actions">
                          <button
                            className="co-button primary"
                            disabled={
                              !["Accepted", "Contacted", "Contracted"].includes(
                                lead.status,
                              )
                            }
                            onClick={() => setChatLead(lead)}
                          >
                            <MessageCircle size={14} /> Trò chuyện
                          </button>
                          {!lead.isPhoneUnlocked &&
                            !["Cancelled", "Expired"].includes(lead.status) && (
                              <button
                                className="co-button secondary"
                                disabled={unlock.isPending}
                                onClick={() => unlock.mutate(lead.id)}
                              >
                                <Unlock size={14} /> Cho phép xem SĐT
                              </button>
                            )}
                          {orderCapability(caps, "canEdit") && (
                            <button
                              className="co-button secondary"
                              onClick={() => show(lead, "edit")}
                            >
                              <Pencil size={14} /> Chỉnh yêu cầu
                            </button>
                          )}
                          {orderCapability(caps, "canRequestChange") && (
                            <button
                              className="co-button secondary"
                              onClick={() => show(lead, "change")}
                            >
                              <Pencil size={14} /> Đề nghị điều chỉnh
                            </button>
                          )}
                          {orderCapability(caps, "canRequestCancellation") && (
                            <button className="co-button danger" onClick={() => show(lead, "cancellation")}>
                              <Square size={13} /> Đề nghị hủy dịch vụ
                            </button>
                          )}
                          {orderCapability(caps, "canWithdraw") && (
                            <button
                              className="co-button danger"
                              onClick={() => show(lead, "withdraw")}
                            >
                              <Square size={13} /> Dừng tư vấn
                            </button>
                          )}
                          {orderCapability(caps, "canArchive") && (
                            <button
                              className="co-button secondary"
                              onClick={() => show(lead, "archive")}
                            >
                              <Archive size={14} /> Lưu trữ
                            </button>
                          )}
                          {orderCapability(caps, "canRestore") && (
                            <button
                              className="co-button secondary"
                              onClick={() => show(lead, "restore")}
                            >
                              <RotateCcw size={14} /> Khôi phục
                            </button>
                          )}
                          {lead.listingId && (
                            <Link
                              className="co-button secondary"
                              to={`/listings/${lead.listingId}`}
                            >
                              Xem dịch vụ
                            </Link>
                          )}
                        </div>
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
      {(page > 1 || rows.length === 50) && (
        <nav
          className="customer-order-pagination"
          aria-label="Trang yêu cầu tư vấn"
        >
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
            disabled={rows.length < 50}
            onClick={() => setPage((p) => p + 1)}
          >
            <ChevronRight size={15} />
          </button>
        </nav>
      )}
      {dialog && (
        <CustomerOrderActionDialog
          key={`${dialog.item.id}:${dialog.intent}`}
          {...dialog}
          onClose={() => setDialog(null)}
          onSuccess={() =>
            setNotice(
              "Đã cập nhật hồ sơ. Lịch sử và các khoản liên quan được xử lý theo kết quả từ hệ thống.",
            )
          }
        />
      )}
      {gallery && (
        <OrderGallery {...gallery} onClose={() => setGallery(null)} />
      )}
      {chatLead && (
        <ChatRoomModal
          isOpen
          leadId={chatLead.id}
          partnerName={chatLead.vendorBrandName}
          listingTitle={chatLead.listingTitle}
          voucherCode={chatLead.voucher?.code}
          isVendorUser={false}
          onClose={() => setChatLead(null)}
        />
      )}
    </section>
  );
}
