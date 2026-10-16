import { useEffect, useMemo, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { ArrowLeft, ArrowRight, BadgeCheck, Check, CheckCheck, ChevronRight, LayoutGrid, PackageOpen, Search, ShoppingBag, SlidersHorizontal, Sparkles, Store, X } from "lucide-react";
import axiosClient from "../../../services/axiosClient";
import { useAuthStore } from "../../../store/authStore";
import { useComboCartStore } from "../../../store/comboCartStore";
import { apiError, type ComboOrder, type PlannerOverview } from "../api";
import { useCatalogBuilder, useServerPrice, useSubmitCatalogCombo, type CatalogOption, type CatalogTemplate, type SavedCombo } from "../comboApi";
import { ErrorText, StatusPill } from "../ui";
import { vnd } from "../format";
import { EMPTY_CART, removeService, selectService, type CartSelection } from "./cartModel";
import { ComboCartPanel, unitName } from "./ComboCartPanel";
import { ComboDialog } from "./ComboDialog";
import { ServiceConfigurator } from "./ServiceConfigurator";
import "../../../styles/combo-builder.css";

const normalize = (s: string) => s.normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/g, "d").toLowerCase();
const tierLabel: Record<string, string> = { Saving: "Tiết kiệm", Popular: "Phổ biến", Luxury: "Cao cấp" };

export default function ComboBuilderPage() {
  const location = useLocation(); const userId = useAuthStore((s) => s.user?.id ?? "guest");
  // A route draft can initialize a new editing session without losing the durable cart on other pages.
  return <ComboBuilder key={`${userId}:${location.key}`} />;
}
function ComboBuilder() {
  const user = useAuthStore((s) => s.user); const scope = user?.id ?? "guest";
  const cart = useComboCartStore((s) => s.carts[scope] ?? EMPTY_CART);
  const updateCart = useComboCartStore((s) => s.update); const clearCart = useComboCartStore((s) => s.clear);
  const { data, isPending, isError, refetch } = useCatalogBuilder();
  const planner = useQuery({ queryKey: ["planner-overview", user?.id], enabled: user?.role === "Customer", queryFn: async () => (await axiosClient.get<PlannerOverview>("/api/planner/overview")).data });
  const profile = planner.data?.profile;
  const location = useLocation(); const navigate = useNavigate(); const params = new URLSearchParams(location.search);
  const routeDraft = (location.state as { draft?: SavedCombo } | null)?.draft;
  const loadedDraft = useRef(false);
  useEffect(() => {
    if (!routeDraft || loadedDraft.current || !data) return;
    loadedDraft.current = true;
    const template = data.templates.find((t) => t.id === routeDraft.comboTemplateId);
    const selections = routeDraft.items.flatMap((item) => {
      if (!item.serviceItemId) return [];
      try { const stored = JSON.parse(item.selectionJson) as Partial<CartSelection>; return [{ categoryId: item.categoryId, serviceItemId: item.serviceItemId, variantIds: stored.variantIds ?? [], addOnIds: stored.addOnIds ?? [], hours: stored.hours ?? 1 }]; }
      catch { return [{ categoryId: item.categoryId, serviceItemId: item.serviceItemId, variantIds: [], addOnIds: [], hours: 1 }]; }
    });
    updateCart(scope, { selections, region: routeDraft.region, weddingDate: routeDraft.weddingDate?.slice(0, 10) ?? "", guestCount: routeDraft.guestCount ?? 0, draftId: routeDraft.id, templateId: template?.id ?? null, templateName: template?.name ?? "", templateVendorId: template?.ownerVendorId ?? null });
  }, [routeDraft, data, scope, updateCart]);
  useEffect(() => {
    if (!profile || routeDraft) return;
    if ((!cart.region && profile.city) || (!cart.weddingDate && profile.weddingDate) || (!cart.guestCount && profile.guestCount))
      updateCart(scope, { region: cart.region || profile.city, weddingDate: cart.weddingDate || profile.weddingDate?.slice(0, 10) || "", guestCount: cart.guestCount || profile.guestCount });
  }, [profile, cart.region, cart.weddingDate, cart.guestCount, routeDraft, scope, updateCart]);

  const [menu, setMenu] = useState(params.get("category") ?? ""); const [listingFilter, setListingFilter] = useState(params.get("listing") ?? "");
  const [query, setQuery] = useState(""); const [vendorFilter, setVendorFilter] = useState(""); const [templateFilter, setTemplateFilter] = useState("All");
  const [configItem, setConfigItem] = useState<{ item: CatalogOption; categoryId: string } | null>(null);
  const [pendingTemplate, setPendingTemplate] = useState<CatalogTemplate | null>(null);
  const [cartOpen, setCartOpen] = useState(false); const [reviewOpen, setReviewOpen] = useState(false); const [clearOpen, setClearOpen] = useState(false);
  const [consent, setConsent] = useState(false); const [notice, setNotice] = useState(""); const [done, setDone] = useState<ComboOrder | null>(null);
  const [phone, setPhone] = useState(user?.phoneNumber && /^\+?\d{9,12}$/.test(user.phoneNumber) ? user.phoneNumber : ""); const [note, setNote] = useState("");
  const submit = useSubmitCatalogCombo();
  const options = useMemo(() => new Map(data?.categories.flatMap((category) => category.options.map((item) => [item.serviceItemId, item] as const)) ?? []), [data]);
  const categories = data?.categories ?? [];
  const fromListing = categories.find((c) => c.options.some((i) => i.listingId === listingFilter));
  const activeId = menu || fromListing?.categoryId || "templates";
  const activeCategory = categories.find((c) => c.categoryId === activeId);
  const lines = cart.selections.map((selection) => ({ selection, item: options.get(selection.serviceItemId), categoryName: categories.find((c) => c.categoryId === selection.categoryId)?.name ?? "Dịch vụ đã chọn" }));
  const vendors = [...new Map([...options.values()].map((item) => [item.vendorId, { id: item.vendorId, name: item.vendorName }])).values()];
  const vendorCount = new Set(lines.flatMap((l) => l.item ? [l.item.vendorId] : [])).size;
  const ready = !!cart.region.trim() && !!cart.weddingDate && Number.isInteger(cart.guestCount) && cart.guestCount > 0;
  const price = useServerPrice({ templateId: cart.templateId, region: cart.region, weddingDate: cart.weddingDate, guestCount: cart.guestCount, selections: cart.selections.map(({ serviceItemId, variantIds, addOnIds, hours }) => ({ serviceItemId, variantIds, addOnIds, hours })) }, ready && lines.length > 0 && lines.every((l) => l.item));
  const matches = (item: CatalogOption) => (!vendorFilter || vendorFilter === item.vendorId) && (!listingFilter || listingFilter === item.listingId) && (!query || normalize(`${item.title} ${item.vendorName}`).includes(normalize(query)));
  const openCategory = (id: string) => { setMenu(id); setListingFilter(""); setNotice(""); };
  const editLine = (categoryId: string) => {
    setMenu(categoryId); setListingFilter(""); setVendorFilter(""); setQuery(""); setCartOpen(false);
    const selection = cart.selections.find((s) => s.categoryId === categoryId); const item = selection && options.get(selection.serviceItemId);
    if (item) setConfigItem({ item, categoryId });
  };
  const addService = (selection: CartSelection) => {
    const item = options.get(selection.serviceItemId)!;
    const detached = !!cart.templateVendorId && item.vendorId !== cart.templateVendorId;
    updateCart(scope, (previous) => selectService(previous, selection, item.vendorId));
    setConfigItem(null); setNotice(detached ? "Đã đổi NCC. Giỏ trở thành combo tự chọn; ưu đãi sẽ được máy chủ tính lại." : `Đã cập nhật ${item.title} vào giỏ.`);
  };
  const review = () => { setCartOpen(false); setConsent(false); setReviewOpen(true); submit.reset(); };
  const send = (draft: boolean) => {
    if (!user) { navigate("/login", { state: { from: "/combo" } }); return; }
    if (!price.data || price.isFetching || price.isError) return;
    submit.mutate({ templateId: cart.templateId, region: cart.region, weddingDate: cart.weddingDate, guestCount: cart.guestCount, selections: cart.selections, expectedTotal: price.data.total, phoneNumber: phone || undefined, note, saveAsDraft: draft, draftId: cart.draftId }, { onSuccess: (order) => {
      setDone(order); setReviewOpen(false); if (draft) updateCart(scope, { draftId: order.id }); else clearCart(scope);
    } });
  };
  const cartPanel = <ComboCartPanel cart={cart} lines={lines} price={price.data} busy={price.isFetching} priceError={price.isError} onEdit={editLine} onRemove={(id) => updateCart(scope, (previous) => removeService(previous, id))} onReview={review} onClear={() => { setCartOpen(false); setClearOpen(true); }} />;

  if (isPending) return <main className="wd wd-page wm-combo"><div className="container combo-loading" role="status"><ShoppingBag size={32} /><p>Đang mở thực đơn dịch vụ cưới...</p></div></main>;
  if (isError || !data) return <main className="wd wd-page wm-combo"><div className="container combo-empty"><PackageOpen size={40} /><h1>Chưa mở được thực đơn dịch vụ</h1><p>Kiểm tra kết nối với API rồi thử lại. Giỏ của bạn vẫn được giữ nguyên.</p><button className="combo-primary" onClick={() => refetch()}>Thử lại</button></div></main>;
  if (done) return <main className="wd wd-page wm-combo"><div className="container combo-success"><CheckCheck size={44} /><p className="combo-eyebrow">{done.code}</p><h1>{done.status === "Draft" ? "Đã lưu combo nháp" : "Combo đã sẵn sàng để NCC báo giá"}</h1><p>{done.status === "Draft" ? "Lựa chọn đã lưu trong tài khoản. Chưa gửi yêu cầu cho NCC và chưa thêm khoản dự trù." : `Đã gửi ${done.items.length} yêu cầu dịch vụ và thêm chi phí dự kiến vào Planner. Giá tham khảo có hiệu lực 7 ngày; chưa thu tiền.`}</p><ul>{done.items.map((item) => <li key={item.id}><span>{item.title}<small>{item.vendorName}{item.voucherCode && ` · ${item.voucherCode}`}</small></span><strong>{vnd(item.price - item.discountShare)}</strong></li>)}</ul><strong className="combo-success-total">Tổng dự kiến: {vnd(done.total)}</strong><div className="combo-actions"><Link className="combo-primary" to="/customer/dashboard?tab=combos">Theo dõi trong Planner <ArrowRight size={16} /></Link><button className="combo-secondary" onClick={() => { setDone(null); if (done.status !== "Draft") setMenu("templates"); }}>{done.status === "Draft" ? "Sửa tiếp bản nháp" : "Tạo combo khác"}</button></div></div></main>;

  return <main className="wd wd-page wm-combo"><div className="container combo-shell">
    <header className="combo-page-heading"><div><p className="combo-eyebrow">MỘT GIỎ DỊCH VỤ · NGÀY CƯỚI CỦA BẠN</p><h1>Tự chọn một ngày <em>thật trọn vẹn.</em></h1><p>Chọn theo danh mục như gọi món. Một NCC trọn gói hoặc nhiều NCC phù hợp, cùng nằm trong combo của hai bạn.</p></div><Link to="/planner" className="combo-back"><ArrowLeft size={15} /> Về kế hoạch cưới</Link></header>
    <ol className="combo-journey" aria-label="Các bước tạo combo"><li className="active"><span>1</span> Chọn dịch vụ</li><li className={lines.length ? "active" : ""}><span>2</span> Chỉnh giỏ combo</li><li><span>3</span> Gửi nhận báo giá</li></ol>
    <section className="combo-basics" aria-label="Thông tin cưới để tính giá"><div><Sparkles size={18} /><strong>{profile ? "Lấy từ kế hoạch cưới" : "Cho mình biết ngày vui của bạn"}</strong><span>Có thể điều chỉnh bất cứ lúc nào.</span></div><div className="combo-basics-fields"><label>Khu vực<input className="input" value={cart.region} onChange={(e) => updateCart(scope, { region: e.target.value })} placeholder="VD: Hồ Chí Minh" /></label><label>Ngày cưới<input className="input" type="date" value={cart.weddingDate} onChange={(e) => updateCart(scope, { weddingDate: e.target.value })} /></label><label>Số khách<input className="input" type="number" min={1} max={10000} step={1} value={cart.guestCount || ""} onChange={(e) => updateCart(scope, { guestCount: Math.max(0, Math.min(10000, Math.floor(Number(e.target.value) || 0))) })} placeholder="200" /></label></div></section>
    {notice && <div className="combo-notice" role="status"><Check size={16} /><span>{notice}</span><button className="combo-icon-button" aria-label="Ẩn thông báo" onClick={() => setNotice("")}><X size={16} /></button></div>}
    <div className="combo-workspace"><nav className="combo-category-nav" aria-label="Danh mục chọn dịch vụ"><p>THỰC ĐƠN NGÀY VUI</p><button className={activeId === "templates" ? "selected" : ""} aria-current={activeId === "templates" ? "page" : undefined} onClick={() => openCategory("templates")}><Sparkles size={18} /><span>Combo gợi ý<small>Bắt đầu nhanh</small></span></button>{categories.map((category) => { const selected = cart.selections.some((line) => line.categoryId === category.categoryId); return <button key={category.categoryId} className={activeId === category.categoryId ? "selected" : ""} aria-current={activeId === category.categoryId ? "page" : undefined} onClick={() => openCategory(category.categoryId)}><LayoutGrid size={17} /><span>{category.name}<small>{category.options.length} lựa chọn</small></span>{selected && <Check size={15} className="combo-category-check" />}</button>; })}</nav>
      <section className="combo-menu" aria-label={activeCategory?.name ?? "Combo gợi ý"}>
        {activeId === "templates" ? <><div className="combo-section-heading"><div><p className="combo-eyebrow">CHỌN MỘT ĐIỂM BẮT ĐẦU</p><h2>Combo gợi ý, tùy chỉnh theo bạn</h2><p>Nạp dịch vụ vào giỏ, rồi đổi, bỏ hoặc thêm bất kỳ hạng mục nào.</p></div></div><div className="combo-filters"><label>Kiểu phối hợp<select className="input" value={templateFilter} onChange={(e) => setTemplateFilter(e.target.value)}><option value="All">Tất cả combo</option><option value="Single">Một NCC trọn gói</option><option value="Mixed">Phối hợp nhiều NCC</option></select></label><button className="combo-text-button" onClick={() => categories[0] && openCategory(categories[0].categoryId)}>Hoặc tự chọn từ đầu <ArrowRight size={14} /></button></div><div className="combo-preset-grid">{data.templates.filter((t) => templateFilter === "All" || (templateFilter === "Single" ? !!t.ownerVendorId : !t.ownerVendorId)).map((template) => <article key={template.id} className={`combo-preset-card ${template.tier === "Popular" ? "is-popular" : ""}`}><div className="combo-preset-top"><span>{tierLabel[template.tier] ?? template.tier}</span>{template.tier === "Popular" && <small>GỢI Ý CHO BẠN</small>}</div><h3>{template.name}</h3><p>{template.description}</p><div className="combo-preset-tags"><span><Store size={12} />{template.ownerVendorId ? template.ownerVendorName ?? "Một NCC trọn gói" : "Kết hợp nhiều NCC"}</span><span>{template.items.filter((i) => !i.isOptional).length} hạng mục có sẵn</span></div><ul>{template.items.slice(0, 4).map((item) => <li key={item.categoryId}><Check size={12} />{categories.find((c) => c.categoryId === item.categoryId)?.name ?? "Dịch vụ cưới"}{item.isOptional && " (tùy chọn)"}</li>)}</ul><button className="combo-secondary" onClick={() => setPendingTemplate(template)}>Xem và chọn combo <ChevronRight size={15} /></button></article>)}</div>{!data.templates.some((t) => templateFilter === "All" || (templateFilter === "Single" ? !!t.ownerVendorId : !t.ownerVendorId)) && <div className="combo-empty"><PackageOpen size={36} /><h3>Chưa có combo gợi ý ở nhóm này</h3><p>Bạn vẫn có thể tự ghép giỏ từ các danh mục bên trái.</p><button className="combo-secondary" onClick={() => categories[0] && openCategory(categories[0].categoryId)}>Tự chọn dịch vụ</button></div>}</> : <>
          <div className="combo-section-heading"><div><p className="combo-eyebrow">CHỌN DỊCH VỤ CHO COMBO</p><h2>{activeCategory?.name ?? "Danh mục dịch vụ"}</h2><p>Mỗi hạng mục có một lựa chọn. Bạn có thể đổi NCC và tùy chỉnh gói trước khi gửi.</p></div></div>
          <div className="combo-filters"><label className="combo-search"><Search size={16} /><input className="input" value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Tìm dịch vụ hoặc NCC..." aria-label="Tìm dịch vụ trong danh mục" /></label><label>Lọc theo NCC<select className="input" value={vendorFilter} onChange={(e) => setVendorFilter(e.target.value)}><option value="">Tất cả nhà cung cấp</option>{vendors.map((vendor) => <option key={vendor.id} value={vendor.id}>{vendor.name}</option>)}</select></label></div>
          {listingFilter && <p className="combo-notice">Đang xem dịch vụ thuộc bài đăng bạn vừa chọn. <button className="combo-text-button" onClick={() => setListingFilter("")}>Xem cả danh mục</button></p>}
          <div className="combo-product-grid">{activeCategory?.options.filter(matches).map((item) => { const selected = cart.selections.find((s) => s.serviceItemId === item.serviceItemId); const current = cart.selections.some((s) => s.categoryId === activeCategory.categoryId); return <article key={item.serviceItemId} className={`combo-product-card ${selected ? "is-selected" : ""}`}><div className="combo-product-image">{item.imageUrl ? <img src={item.imageUrl} loading="lazy" alt="" /> : <PackageOpen size={38} />}{selected && <span><Check size={13} /> Trong giỏ</span>}</div><div className="combo-product-body"><p className="combo-vendor-name"><BadgeCheck size={13} />{item.vendorName}</p><h3>{item.title}</h3><p className="combo-product-capacity">Phục vụ {item.minGuests}–{item.maxGuests} khách</p><p className="combo-product-price"><strong>{vnd(item.price)}</strong>/{unitName[item.unit]}</p>{item.unit === "Table" && cart.guestCount > 0 && <p className="combo-product-capacity">{cart.guestCount} khách → {Math.ceil(cart.guestCount / 10)} bàn</p>}<button className={selected ? "combo-secondary" : "combo-primary"} onClick={() => setConfigItem({ item, categoryId: activeCategory.categoryId })}><SlidersHorizontal size={14} />{selected ? "Tùy chỉnh / đổi" : current ? "Chọn để thay dịch vụ" : "Chọn dịch vụ"}</button><Link to={`/listings/${item.listingId}`} className="combo-product-link">Xem nhà cung cấp</Link></div></article>; })}</div>
          {!activeCategory?.options.some(matches) && <div className="combo-empty"><PackageOpen size={36} /><h3>Chưa có dịch vụ phù hợp bộ lọc</h3><p>Chỉ hiển thị dịch vụ đã được duyệt của NCC đã xác minh và đồng ý tham gia combo.</p>{(query || vendorFilter || listingFilter) && <button className="combo-secondary" onClick={() => { setQuery(""); setVendorFilter(""); setListingFilter(""); }}>Bỏ bộ lọc</button>}</div>}
        </>}
      </section><aside className="combo-desktop-cart">{cartPanel}</aside>
    </div>
    <div className="combo-mobile-cart"><button onClick={() => setCartOpen(true)} aria-label={`Mở giỏ combo, ${lines.length} dịch vụ`}><ShoppingBag size={19} /><span><strong>{lines.length} dịch vụ</strong><small>{price.data ? vnd(price.data.total) : "Xem giỏ của bạn"}</small></span><ArrowRight size={18} /></button></div>
    {price.isError && <div className="combo-price-error"><ErrorText>{apiError(price.error)}</ErrorText></div>}
  </div>
    {configItem && <ServiceConfigurator key={configItem.item.serviceItemId} item={configItem.item} categoryId={configItem.categoryId} guests={cart.guestCount} current={cart.selections.find((s) => s.serviceItemId === configItem.item.serviceItemId)} replacedTitle={lines.find((l) => l.selection.categoryId === configItem.categoryId && l.selection.serviceItemId !== configItem.item.serviceItemId)?.item?.title} onSave={addService} onClose={() => setConfigItem(null)} />}
    {cartOpen && <ComboDialog title="Giỏ combo của bạn" onClose={() => setCartOpen(false)}>{cartPanel}</ComboDialog>}
    {pendingTemplate && <ComboDialog title="Bắt đầu từ combo gợi ý" onClose={() => setPendingTemplate(null)}><p className="combo-eyebrow">{tierLabel[pendingTemplate.tier] ?? pendingTemplate.tier}</p><h3 className="combo-template-preview-title">{pendingTemplate.name}</h3><p className="combo-preview-copy">{pendingTemplate.description}</p><ul className="combo-template-preview-lines">{pendingTemplate.items.map((slot) => { const item = slot.serviceItemId && options.get(slot.serviceItemId); return <li key={slot.categoryId}><Check size={16} /><span><strong>{categories.find((c) => c.categoryId === slot.categoryId)?.name}</strong><small>{item ? `${item.title} · ${item.vendorName}` : "Chưa có dịch vụ khả dụng"}{slot.isOptional && " · tùy chọn, chưa thêm sẵn"}</small></span></li>; })}</ul>{lines.length > 0 && <p className="combo-notice">Combo này sẽ thay các lựa chọn đang có trong giỏ. Bạn có thể sửa từng dịch vụ sau khi chọn.</p>}<button className="combo-primary" disabled={pendingTemplate.items.filter((s) => !s.isOptional).length === 0 || pendingTemplate.items.some((s) => !s.isOptional && (!s.serviceItemId || !options.has(s.serviceItemId)))} onClick={() => { const template = pendingTemplate; updateCart(scope, { selections: template.items.filter((s) => !s.isOptional && s.serviceItemId).map((s) => ({ serviceItemId: s.serviceItemId!, categoryId: s.categoryId, variantIds: [], addOnIds: [], hours: 1 })), templateId: template.id, templateName: template.name, templateVendorId: template.ownerVendorId ?? null, draftId: undefined }); setPendingTemplate(null); setNotice("Đã nạp combo vào giỏ. Chọn danh mục để đổi hoặc tùy chỉnh từng dịch vụ."); setCartOpen(true); }}>Dùng combo và tùy chỉnh <ArrowRight size={16} /></button></ComboDialog>}
    {clearOpen && <ComboDialog title="Làm lại giỏ combo?" onClose={() => setClearOpen(false)}><p className="combo-preview-copy">Chỉ xóa lựa chọn trong giỏ đang chỉnh. Các đơn đã gửi và bản nháp trong tài khoản vẫn được giữ.</p><div className="combo-actions"><button className="combo-secondary" onClick={() => setClearOpen(false)}>Giữ giỏ hiện tại</button><button className="combo-primary" onClick={() => { updateCart(scope, (previous) => ({ ...EMPTY_CART, region: previous.region, weddingDate: previous.weddingDate, guestCount: previous.guestCount })); setClearOpen(false); setMenu("templates"); }}>Chọn lại từ đầu</button></div></ComboDialog>}
    {reviewOpen && <ComboDialog title="Xem lại trước khi gửi" wide onClose={() => { if (!submit.isPending) setReviewOpen(false); }}><p className="combo-preview-copy">{cart.guestCount} khách · {cart.region} · {cart.weddingDate.split("-").reverse().join("/")} · {vendorCount} NCC</p><ul className="combo-review-lines">{price.data?.lines.map((line) => <li key={line.serviceItemId}><div><h3>{line.title} × {line.quantity} {unitName[line.unit]}</h3><p>{line.vendorName} · {line.availability}</p><details><summary>Chính sách hủy</summary><p>{line.cancellationPolicy}</p></details></div><strong>{vnd(line.total)}</strong></li>)}</ul><div className="combo-review-total"><span>Tổng dự kiến sau ưu đãi</span><strong>{price.data ? vnd(price.data.total) : "Đang tính lại..."}</strong></div><p className="combo-notice">Đây là yêu cầu báo giá, chưa phải thanh toán. Giá được kiểm tra lại khi gửi; NCC cần xác nhận còn lịch và gửi báo giá chính thức.</p><div className="combo-review-contact"><label>Số điện thoại nhận tư vấn<input className="input" value={phone} onChange={(e) => setPhone(e.target.value)} type="tel" placeholder="09xxxxxxxx" /></label><label>Ghi chú cho NCC<textarea className="input" value={note} onChange={(e) => setNote(e.target.value)} rows={2} placeholder="Điều hai bạn muốn ưu tiên..." /></label></div><label className="combo-consent"><input type="checkbox" checked={consent} onChange={(e) => setConsent(e.target.checked)} /><span>Tôi đã đọc chính sách hủy và hiểu cần duyệt báo giá trước khi ký hợp đồng.</span></label><ErrorText>{submit.isError ? apiError(submit.error) : price.isError ? apiError(price.error) : ""}</ErrorText><div className="combo-actions"><button className="combo-secondary" disabled={submit.isPending || !price.data || price.isFetching || price.isError} onClick={() => send(true)}>Lưu bản nháp</button><button className="combo-primary" disabled={submit.isPending || !consent || !price.data || price.isFetching || price.isError || !/^\+?\d{9,12}$/.test(phone)} onClick={() => send(false)}>{submit.isPending ? "Đang gửi..." : `Gửi ${lines.length} yêu cầu báo giá`}<ArrowRight size={16} /></button></div></ComboDialog>}
  </main>;
}

export function ComboStatusList({ orders }: { orders: ComboOrder[] }) {
  return <ul className="grid gap-3">{orders.map((order) => <li key={order.id} className="rounded-2xl border border-[var(--line)] p-4 bg-[var(--paper)]"><div className="flex flex-wrap justify-between gap-2"><p className="font-bold">{order.code} <StatusPill status={order.status} /></p><p className="text-sm">{vnd(order.total)}</p></div><ol className="mt-3 grid sm:grid-cols-2 gap-2">{order.items.map((item) => <li key={item.id} className="text-sm flex justify-between gap-2 bg-white rounded-xl border border-[var(--line)] px-3 py-2"><span><b>{item.vendorName}</b> · {item.title}</span><StatusPill status={item.status} /></li>)}</ol></li>)}</ul>;
}
