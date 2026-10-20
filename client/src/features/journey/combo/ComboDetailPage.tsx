import { useState } from "react";
import { Link, useNavigate, useParams, useSearchParams, useLocation } from "react-router-dom";
import {
  ArrowLeft,
  ArrowRight,
  BadgeCheck,
  CalendarDays,
  Check,
  ChevronLeft,
  ChevronRight,
  CircleHelp,
  ImageIcon,
  MapPin,
  PackageOpen,
  Plus,
  ShieldCheck,
  ShoppingBag,
  Star,
  Store,
  Users,
} from "lucide-react";
import { useAuthStore } from "../../../store/authStore";
import { useComboCartStore } from "../../../store/comboCartStore";
import { apiError, usePlannerOverview } from "../api";
import { useCatalogBuilder, useComboTemplatePreview } from "../comboApi";
import { canPreviewCombo, comboDate } from "./comboTemplateEditorModel";
import { vnd } from "../format";
import { ComboDialog } from "./ComboDialog";
import { ComboPhoto } from "./ComboPhoto";
import { comboCatalogReturn } from "./comboFilterModel";
import { fixedComboSummary } from "./fixedComboModel";
import { EMPTY_CART, quantityFor, unitName } from "./cartModel";
import {
  chooseDetailTemplate,
  comboDetailModel,
  detailBasePrice,
  mediaUrls,
  previewDetailCatalog,
  type ComboDetailModel,
  type DetailSlot,
} from "./comboDetailModel";
import "../../../styles/combo-builder.css";
import "../../../styles/combo-detail.css";

const tiers: Record<string, string> = {
  Saving: "Tiết kiệm",
  Popular: "Phổ biến",
  Luxury: "Cao cấp",
};
export default function ComboDetailPage() {
  const { templateId = "" } = useParams<{ templateId: string }>();
  const [params] = useSearchParams();
  const location = useLocation();
  const role = useAuthStore(state => state.user?.role);
  const isPreview = canPreviewCombo(role, params.get("preview"));
  const revisionId = params.get("revision");
  const query = useCatalogBuilder();
  const preview = useComboTemplatePreview(templateId, revisionId, isPreview);
  const current = isPreview ? preview : query;
  const returnPath = isPreview ? role === "VendorOwner" ? "/vendor/dashboard?tab=combos" : "/admin/dashboard?tab=combos" : comboCatalogReturn((location.state as { comboCatalogReturn?: unknown } | null)?.comboCatalogReturn);
  if (current.isPending)
    return (
      <main className="wd wd-page wm-combo-detail">
        <div className="container cd-empty" role="status">
          <PackageOpen size={36} />
          <h1>Đang mở chi tiết combo...</h1>
          <p>Ảnh và dịch vụ được lấy từ thông tin NCC đã công bố.</p>
        </div>
      </main>
    );
  if (current.isError)
    return (
      <main className="wd wd-page wm-combo-detail">
        <div className="container cd-empty" role="alert">
          <PackageOpen size={36} />
          <h1>Chưa tải được combo</h1>
          <p>{apiError(current.error)}</p>
          <button
            className="combo-primary"
            onClick={() => void current.refetch()}
          >
            Thử lại
          </button>
          <Link to={returnPath}>Quay lại các combo</Link>
        </div>
      </main>
    );
  const data = isPreview ? (preview.data ? previewDetailCatalog(query.data, preview.data) : undefined) : query.data;
  const model = data && comboDetailModel(data, templateId);
  if (!model)
    return (
      <main className="wd wd-page wm-combo-detail">
        <div className="container cd-empty">
          <PackageOpen size={38} />
          <p className="cd-eyebrow">COMBO KHÔNG KHẢ DỤNG</p>
          <h1>Combo này chưa được chia sẻ</h1>
          <p>
            Combo có thể đang chờ duyệt, tạm ẩn hoặc không còn phục vụ. Giỏ hiện
            tại của bạn vẫn được giữ nguyên.
          </p>
          <Link className="combo-primary" to={returnPath}>
            <ArrowLeft size={15} />
            Xem các combo đang có
          </Link>
        </div>
      </main>
    );
  return (
    <ComboDetail
      key={`${model.template.id}:${isPreview ? revisionId ?? "preview" : "public"}`}
      model={model}
      readOnly={isPreview}
      returnPath={returnPath}
      previewGuests={
        data?.previewGuestCount || 200
      }
    />
  );
}
function ComboDetail({
  model,
  previewGuests,
  readOnly = false,
  returnPath = "/combo",
}: {
  model: ComboDetailModel;
  previewGuests: number;
  readOnly?: boolean;
  returnPath?: string;
}) {
  const { template, vendor, required, optional, photos } = model;
  const user = useAuthStore((state) => state.user);
  const scope = user?.id ?? "guest";
  const cart = useComboCartStore((state) => state.carts[scope] ?? EMPTY_CART);
  const updateCart = useComboCartStore((state) => state.update);
  const profile = usePlannerOverview().data?.profile;
  const navigate = useNavigate();
  const [photoIndex, setPhotoIndex] = useState(0);
  const [galleryOpen, setGalleryOpen] = useState(false);
  const [replaceOpen, setReplaceOpen] = useState(false);
  const [error, setError] = useState("");
  const visiblePhotoIndex = Math.min(
    photoIndex,
    Math.max(0, photos.length - 1),
  );
  const guests = cart.guestCount || profile?.guestCount || previewGuests;
  const basePrice = detailBasePrice(model, guests);
  const fixed = template.pricingMode === "FixedPrice";
  const fixedSummary = fixedComboSummary(template);
  const editable = !readOnly && (!user || user.role === "Customer");
  const hasVariablePricing = required.some(
    (slot) => slot.item && ["Guest", "Table"].includes(slot.item.unit),
  );
  const choose = () => {
    if (readOnly) return;
    try {
      updateCart(scope, (previous) =>
        chooseDetailTemplate(previous, model, profile),
      );
      setReplaceOpen(false);
      navigate(returnPath, { state: { comboSelected: template.id } });
    } catch (e) {
      setError((e as Error).message);
    }
  };
  const requestChoose = () => {
    if (!model.canChoose || !editable) return;
    if (cart.selections.length) setReplaceOpen(true);
    else choose();
  };
  const hasVendorRating =
    vendor?.ratingAvg != null && (vendor.totalReviews ?? 0) >= 3;
  return (
    <main className="wd wd-page wm-combo-detail">
      <div className="container cd-container">
        {readOnly && <section role="note" className="mb-5 rounded-2xl border border-[#d4c894] bg-[#faf8f0] p-4"><h2 className="font-semibold">Xem trước combo · chỉ đọc</h2><p className="mt-2 text-sm">Bạn đang xem nội dung phiên bản được cấp quyền, không phải tự xuất bản. Chọn combo, gửi yêu cầu và thay đổi giỏ đều được tắt.</p><Link className="mt-2 inline-block text-sm underline" to={returnPath}>Về quản lý combo</Link></section>}
        <nav className="cd-breadcrumb" aria-label="Đường dẫn">
          <Link to={returnPath}>
            <ArrowLeft size={14} />
            Combo cưới
          </Link>
          <ChevronRight size={12} />
          <span>{template.name}</span>
        </nav>
        <section className="cd-photo-section" aria-label="Hình ảnh combo">
          <button
            className="cd-cover"
            disabled={!photos.length}
            onClick={() => setGalleryOpen(true)}
            aria-label="Mở ảnh lớn của combo"
          >
            <ComboPhoto
              sources={photos.length ? [photos[visiblePhotoIndex]] : []}
              alt={`Hình ảnh ${template.name}`}
              className="cd-cover-photo"
              placeholder={
                <>
                  <ImageIcon size={48} />
                  <span>NCC chưa cập nhật ảnh combo</span>
                </>
              }
            />
            <span className="cd-photo-label">
              <ImageIcon size={14} />
              {photos.length
                ? `${visiblePhotoIndex + 1} / ${photos.length} ảnh · Xem lớn`
                : "Chưa có hình ảnh"}
            </span>
          </button>
          {photos.length > 1 && (
            <div className="cd-thumbnails" aria-label="Chọn ảnh combo">
              {photos.slice(0, 6).map((url, index) => (
                <button
                  key={url}
                  onClick={() => setPhotoIndex(index)}
                  className={visiblePhotoIndex === index ? "is-active" : ""}
                  aria-label={`Xem ảnh ${index + 1}`}
                  aria-pressed={visiblePhotoIndex === index}
                >
                  <ComboPhoto sources={[url]} alt={`Ảnh combo ${index + 1}`} />
                  {index === 5 && photos.length > 6 && (
                    <span>+{photos.length - 6}</span>
                  )}
                </button>
              ))}
              {photos.length > 6 && (
                <button
                  className="cd-all-photos"
                  onClick={() => setGalleryOpen(true)}
                >
                  Xem tất cả {photos.length} ảnh
                </button>
              )}
            </div>
          )}
        </section>
        <div className="cd-layout">
          <div className="cd-main">
            <header className="cd-heading">
              <div className="cd-tags">
                <span>{tiers[template.tier] || template.tier}</span>
                <span>{template.style}</span>
                <span>
                  <Store size={12} />
                  Một nhà cung cấp
                </span>
              </div>
              <h1>{template.name}</h1>
              <p className="cd-subtitle">
                Combo của <strong>{model.vendorName}</strong>
                {template.region || vendor?.city
                  ? ` · ${template.region || vendor?.city}`
                  : ""}
              </p>
              {template.description && (
                <p className="cd-description">{template.description}</p>
              )}
              <div className="cd-facts">
                <span>
                  <Check size={16} />
                  {required.length} hạng mục có sẵn
                </span>
                {optional.length > 0 && (
                  <span>
                    <Plus size={16} />
                    {optional.length} hạng mục tùy chọn
                  </span>
                )}
                <span>
                  <ShieldCheck size={16} />
                  {fixed ? "Giá cố định cho nguyên gói đã duyệt" : "Giá chốt sau khi NCC báo giá"}
                </span>
              </div>
            </header>
            <nav className="cd-section-nav" aria-label="Nội dung combo">
              <a href="#dich-vu-trong-combo">Dịch vụ bao gồm</a>
              <a href="#nha-cung-cap-combo">Nhà cung cấp</a>
              <a href="#dieu-kien-combo">Điều kiện</a>
            </nav>
            <section id="dich-vu-trong-combo" className="cd-section">
              <div className="cd-section-title">
                <p className="cd-eyebrow">BIẾT RÕ TRƯỚC KHI CHỌN</p>
                <h2>Trong combo có những gì?</h2>
                <p>
                  {fixed ? "Đây là các dịch vụ của nguyên gói đã duyệt. Muốn thay đổi, hãy gửi yêu cầu rồi trao đổi với NCC trong chat; đề xuất riêng chỉ áp dụng khi bạn xác nhận." : "Các hạng mục dưới đây được thêm sẵn. Bạn có thể đổi dịch vụ trong cùng NCC ở bước tùy chỉnh."}
                </p>
              </div>
              <div className="cd-service-list">
                {required.map((slot) => (
                  <IncludedService
                    key={slot.item?.serviceItemId ?? slot.categoryId}
                    slot={slot}
                    guests={guests}
                    optional={false}
                    readOnly={readOnly}
                    fixed={fixed}
                  />
                ))}
              </div>
              {!required.length && (
                <p className="cd-unavailable" role="alert">
                  NCC chưa cấu hình hạng mục mặc định. Chưa thể chọn combo này.
                </p>
              )}
            </section>
            {optional.length > 0 && (
              <section className="cd-section">
                <div className="cd-section-title">
                  <p className="cd-eyebrow">CHỈ THÊM KHI BẠN CẦN</p>
                  <h2>Có thể bổ sung</h2>
                  <p>
                    Chưa nằm trong giỏ mặc định hoặc tổng giá bên cạnh. Thêm
                    riêng khi tùy chỉnh.
                  </p>
                </div>
                <div className="cd-service-list">
                  {optional.map((slot) => (
                    <IncludedService
                      key={slot.item?.serviceItemId ?? slot.categoryId}
                      slot={slot}
                      guests={guests}
                      optional
                      readOnly={readOnly}
                    />
                  ))}
                </div>
              </section>
            )}
            <section id="nha-cung-cap-combo" className="cd-section cd-vendor">
              <div className="cd-vendor-heading">
                <ComboPhoto
                  className="cd-vendor-logo"
                  sources={[vendor?.logoUrl]}
                  alt={`Logo ${model.vendorName}`}
                  placeholder={<Store size={25} />}
                />
                <div>
                  <p className="cd-eyebrow">ĐƠN VỊ ĐỒNG HÀNH</p>
                  <h2>{model.vendorName}</h2>
                  {vendor?.isVerified && (
                    <span className="cd-verified">
                      <BadgeCheck size={14} />
                      Hồ sơ đã xác minh
                    </span>
                  )}
                </div>
              </div>
              {vendor?.bio ? (
                <p className="cd-description">{vendor.bio}</p>
              ) : (
                <p className="cd-muted">
                  NCC chưa cập nhật phần giới thiệu. Xem chi tiết từng dịch vụ
                  hoặc trao đổi trực tiếp sau khi gửi yêu cầu.
                </p>
              )}
              <dl className="cd-vendor-meta">
                {vendor?.city && (
                  <div>
                    <dt>
                      <MapPin size={15} />
                      Khu vực
                    </dt>
                    <dd>{vendor.city}</dd>
                  </div>
                )}
                {vendor?.address && (
                  <div>
                    <dt>Địa chỉ</dt>
                    <dd>{vendor.address}</dd>
                  </div>
                )}
                {hasVendorRating && (
                  <div>
                    <dt>
                      <Star size={15} />
                      Đánh giá công khai
                    </dt>
                    <dd>
                      {vendor!.ratingAvg!.toFixed(1)}/5 · {vendor!.totalReviews}{" "}
                      đánh giá
                    </dd>
                  </div>
                )}
                {vendor && !hasVendorRating && (
                  <div>
                    <dt>Đánh giá</dt>
                    <dd>Chưa đủ đánh giá để công bố điểm</dd>
                  </div>
                )}
              </dl>
              {required.find((slot) => slot.item)?.item && (
                <Link
                  className="combo-secondary"
                  to={`/listings/${required.find((slot) => slot.item)!.item!.listingId}${readOnly ? "?preview=vendor" : ""}`}
                >
                  Xem hồ sơ & bài đăng của NCC <ArrowRight size={14} />
                </Link>
              )}
            </section>
            <section id="dieu-kien-combo" className="cd-section">
              <div className="cd-section-title">
                <p className="cd-eyebrow">RÕ RÀNG ĐỂ YÊN TÂM</p>
                <h2>Trước khi gửi yêu cầu</h2>
              </div>
              <div className="cd-policy-list">
                {template.content?.terms && <details open><summary>Điều khoản do NCC công bố</summary><p className="whitespace-pre-wrap break-words">{template.content.terms}</p></details>}
                {(template.content?.validFromUtc || template.content?.validUntilUtc) && <details open><summary>Thời hạn áp dụng</summary><p>{template.content.validFromUtc ? `Từ ${comboDate(template.content.validFromUtc)}` : "Không giới hạn ngày bắt đầu"}{template.content.validUntilUtc ? ` · đến ${comboDate(template.content.validUntilUtc)}` : " · chưa giới hạn ngày kết thúc"} (giờ Việt Nam).</p></details>}
                <details open>
                  <summary>
                    <CircleHelp size={16} />
                    Giá hiển thị có phải giá cuối cùng?
                  </summary>
                  <p>
                    Đây là giá cơ bản tham khảo của các dịch vụ được thêm sẵn,
                    chưa gồm nâng cấp và dịch vụ đi kèm chưa chọn. Ưu đãi, số
                    lượng và ngày còn trống được hệ thống kiểm tra lại trong
                    giỏ; giá chính thức do NCC gửi bằng báo giá để bạn duyệt.
                  </p>
                </details>
                <details>
                  <summary>
                    <Store size={16} />
                    Có thể đổi sang NCC khác trong combo này không?
                  </summary>
                  <p>
                    Mỗi combo chỉ thuộc một NCC. Bạn có thể đổi, thêm hoặc bỏ
                    dịch vụ của chính NCC đó. Muốn đổi NCC, hãy bắt đầu một giỏ
                    mới; các yêu cầu và bản nháp đã lưu vẫn được giữ.
                  </p>
                </details>
                <details>
                  <summary>
                    <CalendarDays size={16} />
                    Chính sách hủy và lịch phục vụ
                  </summary>
                  <p>
                    Chính sách do NCC khai báo được hiển thị ở từng dịch vụ phía
                    trên. Lịch trống hiện là dự kiến, chỉ được xác nhận chính
                    thức trong báo giá và hợp đồng. Hãy kiểm tra điều kiện cọc,
                    hoàn tiền và thời gian thực hiện trước khi xác nhận.
                  </p>
                </details>
              </div>
            </section>
          </div>
          <aside className="cd-sidebar">
            <section className="cd-choice-card">
              <p className="cd-eyebrow">
                COMBO CỦA {model.vendorName.toUpperCase()}
              </p>
              <span className="cd-price-label">{fixed ? "Giá bán nguyên gói" : "Tổng giá cơ bản tham khảo"}</span>
              <strong className="cd-price">
                {fixedSummary ? vnd(fixedSummary.price) : model.canChoose ? vnd(basePrice) : "Chưa thể tính"}
              </strong>
              <p className="cd-muted">
                {fixed ? "Giá và thành phần giữ theo phiên bản NCC đã công bố. Lịch trống/phạm vi phục vụ cần NCC xác nhận." : hasVariablePricing
                  ? `Theo quy mô ${guests.toLocaleString("vi-VN")} khách, chưa cộng nâng cấp / dịch vụ đi kèm.`
                  : "Các gói mặc định, chưa cộng nâng cấp / dịch vụ đi kèm."}
              </p>
              {fixedSummary && <div className="cd-note"><span>Tổng giá bán lẻ: {vnd(fixedSummary.original)}{fixedSummary.saving > 0 && <><br />Tiết kiệm {vnd(fixedSummary.saving)} · {fixedSummary.percentage.toLocaleString("vi-VN", { maximumFractionDigits: 2 })}%</>}</span></div>}
              <ul className="cd-summary-list">
                {required.map((slot) => (
                  <li key={slot.item?.serviceItemId ?? slot.categoryId}>
                    <Check size={15} />
                    <span>{slot.categoryName}</span>
                  </li>
                ))}
              </ul>
              <p className="cd-note">
                <ShieldCheck size={18} />
                <span>
                  Chọn combo chỉ đưa dịch vụ vào giỏ để tùy chỉnh. Chưa gửi yêu
                  cầu và không tự thu tiền.
                </span>
              </p>
              {!model.canChoose && (
                <p className="cd-unavailable" role="alert">
                  Có dịch vụ không còn khả dụng hoặc không thuộc NCC này. Vui
                  lòng chọn combo khác.
                </p>
              )}
              {!editable && !readOnly && (
                <p className="cd-unavailable">
                  Chức năng đặt combo dành cho tài khoản cặp đôi.
                </p>
              )}
              {!readOnly && <button
                className="combo-primary"
                disabled={!model.canChoose || !editable}
                onClick={requestChoose}
              >
                <ShoppingBag size={16} />
                {fixed ? "Chọn nguyên gói & gửi yêu cầu" : "Chọn combo & tùy chỉnh"}
                <ArrowRight size={15} />
              </button>}
              <p className="cd-small-print">
                Không phải thanh toán · Bạn sẽ duyệt báo giá trước khi ký hợp
                đồng
              </p>
              {error && (
                <p className="cd-unavailable" role="alert">
                  {error}
                </p>
              )}
              <Link to={returnPath} className="cd-back-link">
                <ArrowLeft size={13} />
                Xem combo khác
              </Link>
            </section>
          </aside>
        </div>
        {!readOnly && <div className="cd-mobile-choose">
          <div>
            <small>Giá cơ bản tham khảo</small>
            <strong>
              {model.canChoose ? vnd(basePrice) : "Chưa khả dụng"}
            </strong>
          </div>
          <button
            className="combo-primary"
            disabled={!model.canChoose || !editable}
            onClick={requestChoose}
          >
            Chọn combo
            <ArrowRight size={15} />
          </button>
        </div>}
        {galleryOpen && photos.length > 0 && (
          <ComboDialog
            title={`Ảnh ${template.name}`}
            wide
            onClose={() => setGalleryOpen(false)}
          >
            <div className="cd-gallery-large">
              <ComboPhoto
                sources={[photos[visiblePhotoIndex]]}
                alt={`Ảnh ${visiblePhotoIndex + 1} của ${template.name}`}
              />
            </div>
            <div className="cd-gallery-controls">
              <button
                className="combo-secondary"
                disabled={photos.length < 2}
                onClick={() =>
                  setPhotoIndex(
                    (visiblePhotoIndex - 1 + photos.length) % photos.length,
                  )
                }
                aria-label="Ảnh trước"
              >
                <ChevronLeft size={18} />
              </button>
              <span>
                {visiblePhotoIndex + 1} / {photos.length}
              </span>
              <button
                className="combo-secondary"
                disabled={photos.length < 2}
                onClick={() =>
                  setPhotoIndex((visiblePhotoIndex + 1) % photos.length)
                }
                aria-label="Ảnh tiếp"
              >
                <ChevronRight size={18} />
              </button>
            </div>
            <p className="cd-muted">
              Hình ảnh do NCC đăng trên combo hoặc bài dịch vụ liên quan; phạm
              vi cụ thể theo mô tả và báo giá.
            </p>
          </ComboDialog>
        )}
        {!readOnly && replaceOpen && (
          <ComboDialog
            title="Thay các dịch vụ đang chỉnh?"
            onClose={() => setReplaceOpen(false)}
          >
            <p className="cd-description">
              Giỏ hiện có {cart.selections.length} dịch vụ. Chọn “
              {template.name}” sẽ thay chúng bằng {required.length} hạng mục mặc
              định của {model.vendorName}.
            </p>
            <p className="cd-muted">
              Ngày cưới, khu vực và số khách vẫn giữ nguyên. Đơn đã gửi và bản
              nháp trong tài khoản không bị xóa.
            </p>
            <div className="combo-actions">
              <button
                className="combo-secondary"
                onClick={() => setReplaceOpen(false)}
              >
                Giữ giỏ hiện tại
              </button>
              <button className="combo-primary" onClick={choose}>
                {fixed ? "Chọn nguyên gói này" : "Thay giỏ & tùy chỉnh"}
                <ArrowRight size={15} />
              </button>
            </div>
          </ComboDialog>
        )}
      </div>
    </main>
  );
}
function IncludedService({
  slot,
  guests,
  optional,
  readOnly = false,
  fixed = false,
}: {
  slot: DetailSlot;
  guests: number;
  optional: boolean;
  readOnly?: boolean;
  fixed?: boolean;
}) {
  const item = slot.item;
  const quantity = item ? fixed ? 1 : quantityFor(item.unit, guests, 1) : 0;
  return (
    <article className={`cd-service ${optional ? "is-optional" : ""}`}>
      <div className="cd-service-photo">
        <ComboPhoto
          sources={mediaUrls([item?.imageUrl, ...(item?.galleryUrls ?? [])])}
          alt={item?.title || slot.categoryName}
        />
      </div>
      <div className="cd-service-content">
        <div className="cd-service-meta">
          <span>{slot.categoryName}</span>
          <small>{optional ? "Tùy chọn · chưa gồm" : "Được thêm sẵn"}</small>
        </div>
        <h3>{item?.title || "Dịch vụ chưa khả dụng"}</h3>
        {item && slot.valid ? (
          <>
            <p className="cd-service-price">
              {vnd(item.price)} / {unitName[item.unit] || item.unit}
              <span>
                {quantity} {unitName[item.unit] || item.unit} mặc định
              </span>
            </p>
            <p className="cd-service-description">
              {item.description ||
                "NCC chưa bổ sung mô tả chi tiết. Hãy xem bài dịch vụ hoặc yêu cầu NCC làm rõ phạm vi trong báo giá."}
            </p>
            <div className="cd-service-capacity">
              <Users size={13} />
              Phục vụ {item.minGuests.toLocaleString("vi-VN")}–
              {item.maxGuests.toLocaleString("vi-VN")} khách
              {item.location && ` · ${item.location}`}
            </div>
            {(item.variants.length > 0 || item.addOns.length > 0) && (
              <details className="cd-upgrades">
                <summary>Nâng cấp & dịch vụ đi kèm chưa chọn</summary>
                {item.variants.length > 0 && (
                  <>
                    <h4>Nâng cấp gói</h4>
                    <ul>
                      {item.variants.map((variant, index) => (
                        <li key={variant.id || index}>
                          <span>{variant.name}</span>
                          <strong>
                            {variant.priceDelta >= 0 ? "+" : ""}
                            {vnd(variant.priceDelta)} /{" "}
                            {unitName[item.unit] || item.unit}
                          </strong>
                        </li>
                      ))}
                    </ul>
                  </>
                )}
                {item.addOns.length > 0 && (
                  <>
                    <h4>Dịch vụ đi kèm</h4>
                    <ul>
                      {item.addOns.map((addon, index) => (
                        <li key={addon.id || index}>
                          <span>{addon.name}</span>
                          <strong>
                            +{vnd(addon.price)}
                            {addon.perUnit
                              ? ` / ${unitName[item.unit] || item.unit}`
                              : " / lần"}
                          </strong>
                        </li>
                      ))}
                    </ul>
                  </>
                )}
                <p>Chỉ tính thêm sau khi bạn lựa chọn ở bước tùy chỉnh.</p>
              </details>
            )}
            <details className="cd-service-policy">
              <summary>Chính sách hủy của dịch vụ</summary>
              <p>
                {item.cancellationPolicy ||
                  "NCC chưa công bố chính sách. Cần xác nhận bằng báo giá / hợp đồng trước khi đặt."}
              </p>
            </details>
            <Link
              to={`/listings/${item.listingId}${readOnly ? "?preview=vendor" : ""}`}
              className="cd-listing-link"
            >
              Xem đầy đủ bài dịch vụ
              <ArrowRight size={13} />
            </Link>
          </>
        ) : (
          <p className="cd-unavailable">
            Dịch vụ hiện chưa khả dụng trong combo này.
          </p>
        )}
      </div>
    </article>
  );
}
