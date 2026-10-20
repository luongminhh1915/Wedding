import { ArrowRight, Pencil, ShoppingBag, Trash2 } from "lucide-react";
import type { CatalogOption, ComboPrice } from "../comboApi";
import { vnd } from "../format";
import {
  estimateLine,
  quantityFor,
  unitName,
  type ComboCart,
  type CartSelection,
} from "./cartModel";

export interface ResolvedCartLine {
  selection: CartSelection;
  item?: CatalogOption;
  categoryName: string;
}
export function ComboCartPanel({
  cart,
  lines,
  price,
  busy,
  priceError,
  errorMessage,
  onEdit,
  onRemove,
  onReview,
  onClear,
}: {
  cart: ComboCart;
  lines: ResolvedCartLine[];
  price?: ComboPrice;
  busy: boolean;
  priceError: boolean;
  errorMessage?: string;
  onEdit: (categoryId: string) => void;
  onRemove: (categoryId: string) => void;
  onReview: () => void;
  onClear: () => void;
}) {
  const vendors = new Set(
    lines.flatMap((l) => (l.item ? [l.item.vendorId] : [])),
  );
  const fixed = cart.pricingMode === "FixedPrice";
  const ready =
    !!cart.region.trim() && !!cart.weddingDate && cart.guestCount > 0;
  return (
    <section className="combo-cart-panel" aria-label="Giỏ combo của bạn">
      <div className="combo-cart-heading">
        <div>
          <p className="combo-eyebrow">COMBO CỦA HAI BẠN</p>
          <h2>
            <ShoppingBag size={20} /> Giỏ dịch vụ <span>{lines.length}</span>
          </h2>
        </div>
        {lines.length > 0 && (
          <button type="button" onClick={onClear} className="combo-text-button">
            Làm lại
          </button>
        )}
      </div>
      <p className="combo-cart-context">
        {!lines.length
          ? "Chọn món đầu tiên cho ngày vui của bạn."
          : vendors.size === 1
            ? `NCC: ${lines.find((line) => line.item)?.item?.vendorName ?? "Nhà cung cấp đã chọn"}`
            : "Giỏ cũ cần chọn lại một NCC trước khi tiếp tục."}
      </p>
      {cart.templateName && (
        <p className="combo-template-source">
          {fixed ? "Gói giá cố định: " : "Tùy chỉnh từ: "}{cart.templateName}
        </p>
      )}
      {!lines.length ? (
        <div className="combo-cart-empty">
          <ShoppingBag size={34} strokeWidth={1.2} />
          <p>Giỏ đang trống</p>
          <span>Chọn từng danh mục hoặc bắt đầu bằng combo gợi ý.</span>
        </div>
      ) : (
        <ol className="combo-cart-lines">
          {lines.map(({ item, selection, categoryName }) => {
            const serverLine = price?.lines.find(
              (l) => l.serviceItemId === selection.serviceItemId,
            );
            return (
              <li key={selection.serviceItemId}>
                <div className="combo-cart-line-top">
                  <span>{categoryName}</span>
                  {!fixed && <button
                    type="button"
                    className="combo-icon-button"
                    aria-label={`Bỏ ${categoryName} khỏi giỏ`}
                    onClick={() => onRemove(selection.categoryId)}
                  >
                    <Trash2 size={15} />
                  </button>}
                </div>
                {item ? (
                  <>
                    <h3>{item.title}</h3>
                    <p>{item.vendorName}</p>
                    <p>
                      {fixed ? 1 : quantityFor(item.unit, cart.guestCount, selection.hours)}{" "}
                      {unitName[item.unit]}
                      {selection.variantIds.length + selection.addOnIds.length >
                        0 &&
                        ` · ${selection.variantIds.length + selection.addOnIds.length} tùy chọn`}
                    </p>
                    <div className="combo-line-bottom">
                      <strong>
                        {serverLine ? (
                          vnd(serverLine.total)
                        ) : ready ? (
                          <>
                            {vnd(
                              estimateLine(item, selection, cart.guestCount),
                            )}
                            <small> trước ưu đãi</small>
                          </>
                        ) : (
                          `${vnd(item.price)}/${unitName[item.unit]}`
                        )}
                      </strong>
                      {!fixed && <button
                        type="button"
                        className="combo-text-button"
                        onClick={() => onEdit(selection.categoryId)}
                      >
                        <Pencil size={12} /> Sửa / đổi
                      </button>}
                    </div>
                  </>
                ) : (
                  <p role="alert">
                    Dịch vụ này không còn khả dụng. Hãy bỏ khỏi giỏ và chọn lại.
                  </p>
                )}
              </li>
            );
          })}
        </ol>
      )}
      {fixed && <p className="combo-notice">Gói giữ nguyên các dịch vụ đã duyệt. Sau khi gửi yêu cầu, bạn có thể chat với NCC để nhận đề xuất thay đổi riêng cho đơn. Nội dung mới chỉ áp dụng sau khi bạn xác nhận, không thay combo công khai.</p>}
      <div className="combo-cart-totals" aria-live="polite">
        {errorMessage && <p role="alert">{errorMessage}</p>}
        {price ? (
          <>
            <div>
              <span>Tổng trước ưu đãi</span>
              <span>{vnd(price.subtotal)}</span>
            </div>
            {price.discountAmount > 0 && (
              <div className="combo-discount">
                <span>
                  Ưu đãi được NCC chấp thuận ·{" "}
                  {Math.round(price.discountPercent * 100)}%
                </span>
                <span>−{vnd(price.discountAmount)}</span>
              </div>
            )}
            <div className="combo-grand-total">
              <span>{fixed ? "Giá nguyên gói" : "Tổng dự kiến"}</span>
              <strong>{vnd(price.total)}</strong>
            </div>
            {price.plannerReservedBeforeCart != null && (
              <div>
                <span>Các khoản khác đã dự trù</span>
                <span>{vnd(price.plannerReservedBeforeCart)}</span>
              </div>
            )}
            {!!price.plannerReplacedEstimate && (
              <small>
                Combo sẽ thay {vnd(price.plannerReplacedEstimate)} dự trù mẫu,
                không cộng chồng khoản này.
              </small>
            )}
            {price.plannerRemaining != null && (
              <p
                className={
                  price.plannerRemaining < 0
                    ? "combo-budget-warning"
                    : "combo-budget-ok"
                }
              >
                {price.plannerRemaining >= 0
                  ? `Còn ${vnd(price.plannerRemaining)} trong ngân sách`
                  : `Vượt ngân sách ${vnd(-price.plannerRemaining)}`}
              </p>
            )}
          </>
        ) : (
          <p>
            {!lines.length
              ? "Chưa có chi phí"
              : !ready
                ? "Nhập thông tin cưới để tính tổng chính xác."
                : busy
                  ? "Đang tính giá theo lựa chọn..."
                  : priceError
                    ? "Cần kiểm tra lại lựa chọn trước khi gửi."
                    : "Đang chờ giá từ máy chủ..."}
          </p>
        )}
        {price && (
          <details className="combo-price-conditions">
            <summary>Điều kiện giá & ưu đãi</summary>
            <ul>
              {price.warnings.map((warning) => (
                <li key={warning}>{warning}</li>
              ))}
            </ul>
          </details>
        )}
        <button
          type="button"
          className="combo-primary"
          disabled={!price || busy || priceError || lines.some((l) => !l.item)}
          onClick={onReview}
        >
          Bước tiếp theo <ArrowRight size={16} />
        </button>
        <small>Kiểm tra combo & thông tin liên hệ. Chưa thanh toán.</small>
      </div>
    </section>
  );
}
