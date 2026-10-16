import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ChevronLeft,
  ChevronRight,
  Gift,
  MessageCircle,
  RefreshCw,
  Unlock,
} from "lucide-react";
import axiosClient from "../../../services/axiosClient";
import type { Lead } from "../../../types/lead.types";
import { ChatRoomModal } from "../../chat/components/ChatRoomModal";

const statusLabels: Record<Lead["status"], string> = {
  New: "Chờ nhà cung cấp tiếp nhận",
  Accepted: "Đã tiếp nhận",
  Contacted: "Đang trao đổi",
  Contracted: "Đã tạo hợp đồng",
  Cancelled: "Đã hủy",
  Expired: "Đã hết hạn",
};

export function CustomerLeads() {
  const [page, setPage] = useState(1);
  const [chatLead, setChatLead] = useState<Lead | null>(null);
  const queryClient = useQueryClient();
  const {
    data: leads = [],
    isPending,
    isError,
    refetch,
  } = useQuery<Lead[]>({
    queryKey: ["customer-leads", page],
    queryFn: async () =>
      (
        await axiosClient.get(
          `/api/leads/my-leads?pageNumber=${page}&pageSize=20`,
        )
      ).data,
  });
  const unlock = useMutation({
    mutationFn: async (id: string) =>
      axiosClient.post(`/api/leads/${id}/unlock-phone`),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["customer-leads"] }),
  });

  return (
    <section className="overview-panel mb-7">
      <div className="overview-panel-title">
        <h2>Tư vấn & mã ưu đãi</h2>
        <button onClick={() => refetch()} aria-label="Làm mới yêu cầu tư vấn">
          <RefreshCw size={14} />
          Làm mới
        </button>
      </div>
      <p>
        Theo dõi yêu cầu đã gửi và trò chuyện với nhà cung cấp khi được tiếp
        nhận.
      </p>
      {isPending ? (
        <p className="py-8 text-xs text-slate-400">
          Đang tải yêu cầu tư vấn...
        </p>
      ) : isError ? (
        <p role="alert" className="py-6 text-xs text-red-600">
          Chưa thể tải yêu cầu tư vấn. Hãy thử làm mới.
        </p>
      ) : leads.length === 0 ? (
        <p className="py-8 text-xs text-slate-400">
          Chưa có yêu cầu tư vấn. Khám phá dịch vụ và gửi yêu cầu đầu tiên nhé.
        </p>
      ) : (
        <div className="divide-y divide-slate-100 mt-5">
          {leads.map((lead) => (
            <div
              key={lead.id}
              className="py-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4"
            >
              <div className="min-w-0">
                <h3 className="text-sm font-semibold text-slate-800">
                  {lead.vendorBrandName}
                </h3>
                <p className="text-xs text-slate-400 mt-1">
                  {lead.listingTitle || "Tư vấn dịch vụ cưới"}
                </p>
                <span className="text-[10px] text-rose-600 mt-2 inline-block">
                  {statusLabels[lead.status]}
                </span>
                {lead.voucher && (
                  <span className="flex items-center gap-2 mt-2 text-[10px] text-amber-700">
                    <Gift size={13} />
                    <b className="font-mono">{lead.voucher.code}</b> ·{" "}
                    {new Date(lead.voucher.expiresAt).toLocaleDateString(
                      "vi-VN",
                    )}
                  </span>
                )}
              </div>
              <div className="flex flex-wrap gap-2">
                <button
                  disabled={
                    !["Accepted", "Contacted", "Contracted"].includes(
                      lead.status,
                    )
                  }
                  onClick={() => setChatLead(lead)}
                  className="inline-flex items-center gap-2 text-xs px-4 py-2.5 rounded-xl bg-rose-50 text-rose-700 disabled:opacity-40"
                >
                  <MessageCircle size={15} />
                  Trò chuyện
                </button>
                {!lead.isPhoneUnlocked && (
                  <button
                    disabled={unlock.isPending}
                    onClick={() => unlock.mutate(lead.id)}
                    className="inline-flex items-center gap-2 text-xs px-4 py-2.5 rounded-xl border border-slate-200 text-slate-500 disabled:opacity-40"
                  >
                    <Unlock size={14} />
                    Cho phép xem SĐT
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
      {unlock.isError && (
        <p role="alert" className="text-xs text-red-600 mt-3">
          Không thể mở quyền xem số điện thoại. Vui lòng thử lại.
        </p>
      )}
      {(page > 1 || leads.length === 20) && (
        <div className="flex justify-end items-center gap-3 text-xs mt-4">
          <button
            aria-label="Trang yêu cầu trước"
            disabled={page === 1}
            onClick={() => setPage((p) => p - 1)}
            className="p-2 disabled:opacity-30"
          >
            <ChevronLeft size={16} />
          </button>
          <span>Trang {page}</span>
          <button
            aria-label="Trang yêu cầu sau"
            disabled={leads.length < 20}
            onClick={() => setPage((p) => p + 1)}
            className="p-2 disabled:opacity-30"
          >
            <ChevronRight size={16} />
          </button>
        </div>
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
