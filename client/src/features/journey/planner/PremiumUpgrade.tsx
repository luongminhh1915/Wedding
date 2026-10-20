import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Crown, QrCode, RefreshCw } from "lucide-react";
import { apiError } from "../api";
import { billingApi, type PremiumOrderInfo } from "../financeApi";
import { ErrorText } from "../ui";
import { vnd } from "../format";

/** Premium is activated only by the verified payment ledger; clients cannot mark an order paid. */
export function PremiumUpgrade({ isPremium }: { isPremium?: boolean }) {
  const [order, setOrder] = useState<PremiumOrderInfo | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const status = useQuery({
    queryKey: ["billing-me"],
    queryFn: billingApi.me,
    refetchInterval: order && !order.isPremium ? 10000 : false,
  });
  const info = status.data ?? order;
  const done = isPremium || info?.isPremium;
  if (done)
    return (
      <div className="wd card p-5 mb-6 flex items-center gap-3">
        <span className="badge badge-premium">
          <Crown size={12} /> PREMIUM
        </span>
        <p className="text-sm">
          Thiệp đã kích hoạt Premium sau khi đối soát thanh toán.
        </p>
      </div>
    );
  const create = async () => {
    setBusy(true);
    setError("");
    try {
      setOrder(await billingApi.order());
      await status.refetch();
    } catch (e) {
      setError(apiError(e));
    } finally {
      setBusy(false);
    }
  };
  return (
    <div className="wd card p-6 mb-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="max-w-[520px]">
          <span className="badge badge-premium">
            <Crown size={12} /> PREMIUM
          </span>
          <h3 className="serif text-xl font-bold mt-2">
            Thiệp cưới Premium · {vnd(130000)} / một lần
          </h3>
          <p className="text-sm muted mt-2">
            Mở mẫu cao cấp và các tính năng nâng cấp của trình chỉnh sửa. Chỉ
            chuyển khoản khi đúng tên tài khoản nhận và nội dung trên QR.
          </p>
          <p className="text-xs muted mt-2">
            Ngân hàng xác nhận tự động hoặc Finance đối soát giao dịch thật. Bấm
            “Kiểm tra” không tự kích hoạt.
          </p>
        </div>
        {!info ? (
          <button className="btn btn-primary" disabled={busy} onClick={create}>
            <QrCode size={14} /> Tạo đơn thanh toán
          </button>
        ) : (
          <div className="text-center max-w-[300px]">
            {info.qrUrl ? (
              <img
                src={info.qrUrl}
                alt={`VietQR thanh toán ${vnd(info.price)}`}
                width={180}
                height={180}
                className="rounded-xl border mx-auto"
              />
            ) : (
              <p className="text-sm">
                Thanh toán chưa được cấu hình. Không chuyển tiền vào tài khoản
                thử nghiệm.
              </p>
            )}
            <p className="text-xs muted mt-2 break-all">
              Nội dung: <b>{info.paymentRef}</b>
            </p>
            <p className="text-xs muted">
              Trạng thái:{" "}
              {info.status === "Paid" ? "Đã trả" : "Chờ ngân hàng xác nhận"}
            </p>
            <button
              className="btn btn-outline mt-3"
              disabled={status.isFetching}
              onClick={() => status.refetch()}
            >
              <RefreshCw size={14} /> Kiểm tra thanh toán
            </button>
          </div>
        )}
      </div>
      <ErrorText>
        {error || (status.isError ? apiError(status.error) : "")}
      </ErrorText>
    </div>
  );
}
