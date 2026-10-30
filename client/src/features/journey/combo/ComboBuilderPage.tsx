import { useEffect, useMemo, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, useLocation, useNavigate } from "react-router-dom";
import {
  ArrowLeft,
  ArrowRight,
  BadgeCheck,
  Check,
  CheckCheck,
  ChevronRight,
  LayoutGrid,
  MapPin,
  PackageOpen,
  ShoppingBag,
  SlidersHorizontal,
  Sparkles,
  Store,
  X,
} from "lucide-react";
import axiosClient from "../../../services/axiosClient";
import { operatingAreaLabel } from "../../auth/components/operatingAreasModel";
import { useAuthStore } from "../../../store/authStore";
import { useComboCartStore } from "../../../store/comboCartStore";
import { apiError, type ComboOrder, type PlannerOverview } from "../api";
import {
  useCatalogPage,
  useServerPrice,
  useSubmitCatalogCombo,
  type CatalogOption,
  type SavedCombo,
} from "../comboApi";
import { ErrorText, StatusPill } from "../ui";
import { vnd } from "../format";
import {
  EMPTY_CART,
  removeService,
  selectService,
  startVendorCart,
  unitName,
  type CartSelection,
} from "./cartModel";
import { ComboCartPanel } from "./ComboCartPanel";
import { ComboDialog } from "./ComboDialog";
import { ServiceConfigurator } from "./ServiceConfigurator";
import { ComboNextSteps } from "./ComboNextSteps";
import { ComboPhoto } from "./ComboPhoto";
import { editableComboCart } from "./comboEditing";
import { fixedComboOptions, fixedComboSummary } from "./fixedComboModel";
import { ComboServiceCards } from "./ComboServiceCards";
import { comboServicePreviewInput } from "./comboServiceDiscoveryModel";
import { catalogErrorMessage } from "./catalogError";
import { useCustomerOrderJourney } from "../customerOrders/customerOrderApi";
import { Pagination, scrollToList, useResponsivePageSize } from "../../../components/Pagination";
import { ComboCatalogFilters } from "./ComboCatalogFilters";
import { clearComboFilterSearch, comboFilterSearch, readComboFilters, type ComboFilters } from "./comboFilterModel";
import "../../../styles/combo-builder.css";
import "../../../styles/combo-discovery.css";
const tierLabel: Record<string, string> = {
  Saving: "Tiết kiệm",
  Popular: "Phổ biến",
  Luxury: "Cao cấp",
};
const categoryLabels: Record<string, string> = {
  "tiem-cuoi-venues": "Tiệc cưới",
  "trang-tri-decor": "Trang trí",
  "quay-chup-photo-video": "Ảnh & video",
  "vay-cuoi-vest": "Trang phục",
  "trang-diem-makeup": "Trang điểm",
  "thiep-cuoi-qua-tang": "Thiệp & quà",
  "wedding-planner": "Tổ chức A–Z",
};

export default function ComboBuilderPage() {
  const userId = useAuthStore((s) => s.user?.id ?? "guest");
  // URL filter changes must not remount the cart/editor or replay a consumed draft intent.
  return <ComboBuilder key={userId} />;
}
function ComboBuilder() {
  const user = useAuthStore((s) => s.user);
  const scope = user?.id ?? "guest";
  const cart = useComboCartStore((s) => s.carts[scope] ?? EMPTY_CART);
  const updateCart = useComboCartStore((s) => s.update);
  const clearCart = useComboCartStore((s) => s.clear);
  const journey = useCustomerOrderJourney();
  const planner = useQuery({
    queryKey: ["planner-overview", user?.id],
    enabled: user?.role === "Customer",
    queryFn: async () =>
      (await axiosClient.get<PlannerOverview>("/api/planner/overview")).data,
  });
  const profile = planner.data?.profile;
  const location = useLocation();
  const navigate = useNavigate();
  const filters = readComboFilters(location.search);
  const changeFilters = (changes: Partial<ComboFilters>) => navigate({ pathname: location.pathname,
    search: comboFilterSearch({ ...filters, page: 1, ...changes }, location.search) }, { state: location.state });
  const resetFilters = () => navigate({ pathname: location.pathname, search: clearComboFilterSearch(location.search) }, { state: location.state });
  const routeDraft = (location.state as { draft?: SavedCombo } | null)?.draft;
  const routeRevision = (location.state as { revision?: SavedCombo } | null)
    ?.revision;
  const routeOrder = routeRevision ?? routeDraft;
  const menu = filters.category ?? "";
  const listingFilter = filters.listing ?? "";
  const query = filters.search ?? "";
  const setMenu = (category: string) => changeFilters({ category, listing: undefined,
    ...(category !== "templates" ? { minServices: undefined, maxServices: undefined, minSavings: undefined, templateCategoryId: undefined } : {}) });
  const setListingFilter = (listing: string) => changeFilters({ listing: listing || undefined });
  const pageSize = useResponsivePageSize();
  const page = filters.page;
  const catalog = useCatalogPage({
    categoryId: menu && menu !== "templates" ? menu : undefined,
    vendorId: cart.vendorId ?? cart.templateVendorId ?? undefined,
    listingId: listingFilter || undefined,
    search: query, sort: filters.sort, page, pageSize,
    minPrice: filters.minPrice, maxPrice: filters.maxPrice, city: filters.city, region: filters.region,
    minServices: filters.minServices, maxServices: filters.maxServices, minSavings: filters.minSavings,
    minRating: filters.minRating, templateCategoryId: filters.templateCategoryId, filterVendorId: filters.filterVendorId,
    selectedItemIds: [...new Set([...cart.selections.map(i => i.serviceItemId), ...(routeOrder?.items.flatMap(i => i.serviceItemId ? [i.serviceItemId] : []) ?? [])])].sort(),
    templateId: routeOrder?.comboTemplateId ?? cart.templateId,
    guests: cart.guestCount || 200,
  });
  const { isPending, isError, refetch } = catalog;
  const data = catalog.data?.builder;
  const servicePreviewInput = comboServicePreviewInput(catalog.data, filters,
    cart.vendorId ?? cart.templateVendorId, cart.guestCount || 200);
  const servicePreview = useCatalogPage(servicePreviewInput ?? {
    page: 1, pageSize: 4, selectedItemIds: [],
  }, !!servicePreviewInput);
  const previewCategory = servicePreviewInput ? servicePreview.data?.builder.categories.find(
    category => category.categoryId === servicePreviewInput.categoryId,
  ) : undefined;
  const listRef = useRef<HTMLElement>(null);
  const changePage = (next: number) => {
    changeFilters({ page: next });
    scrollToList(listRef.current);
  };
  const intentMetadata = journey.data?.find(
    (item) => item.kind === "combo" && item.id === routeOrder?.id,
  );
  const [replaceApproved, setReplaceApproved] = useState(
    !cart.selections.length,
  );
  const loadedDraft = useRef<string | null>(null);
  const prefilledProfile = useRef(false);
  useEffect(() => {
    if (
      !routeOrder ||
      loadedDraft.current === routeOrder.id ||
      !data ||
      !intentMetadata?.capabilities.canEdit
    )
      return;
    if (!replaceApproved) return;
    loadedDraft.current = routeOrder.id;
    const template = [...data.templates, ...(catalog.data?.selectedTemplate ? [catalog.data.selectedTemplate] : [])].find(
      (t) => t.id === routeOrder.comboTemplateId,
    );
    updateCart(
      scope,
      editableComboCart(
        routeOrder,
        template,
        routeRevision?.id ?? intentMetadata.revisionOfId,
      ),
    );
    // Consume this one-time intent. Browser Back must not replay an old draft over
    // later edits (or restore an obsolete draft ID after it has been re-saved).
    const nextState = { ...(location.state as Record<string, unknown>) };
    delete nextState.draft;
    delete nextState.revision;
    navigate(
      {
        pathname: location.pathname,
        search: location.search,
        hash: location.hash,
      },
      {
        replace: true,
        state: Object.keys(nextState).length ? nextState : null,
      },
    );
  }, [
    routeOrder,
    routeRevision,
    intentMetadata,
    replaceApproved,
    data,
    catalog.data?.selectedTemplate,
    scope,
    updateCart,
    location.state,
    location.pathname,
    location.search,
    location.hash,
    navigate,
  ]);
  useEffect(() => {
    if (!profile || routeOrder || prefilledProfile.current) return;
    prefilledProfile.current = true;
    if (
      (!cart.region && profile.city) ||
      (!cart.weddingDate && profile.weddingDate) ||
      (!cart.guestCount && profile.guestCount)
    )
      updateCart(scope, {
        region: cart.region || profile.city,
        weddingDate:
          cart.weddingDate || profile.weddingDate?.slice(0, 10) || "",
        guestCount: cart.guestCount || profile.guestCount,
      });
  }, [
    profile,
    cart.region,
    cart.weddingDate,
    cart.guestCount,
    routeOrder,
    scope,
    updateCart,
  ]);

  const [pendingVendor, setPendingVendor] = useState<{
    id: string;
    name: string;
  } | null>(null);
  const [configItem, setConfigItem] = useState<{
    item: CatalogOption;
    categoryId: string;
  } | null>(null);
  const selectedFromDetail =
    (location.state as { comboSelected?: string } | null)?.comboSelected ===
    cart.templateId;
  const [cartOpen, setCartOpen] = useState(selectedFromDetail);
  const [reviewOpen, setReviewOpen] = useState(false);
  const [clearOpen, setClearOpen] = useState(false);
  const [consent, setConsent] = useState(false);
  const [notice, setNotice] = useState(
    selectedFromDetail
      ? "Đã chọn combo. Bấm “Bước tiếp theo” để kiểm tra và gửi yêu cầu. Gói cố định giữ nguyên thành phần; thay đổi riêng sẽ trao đổi với NCC trong chat."
      : "",
  );
  const [done, setDone] = useState<ComboOrder | null>(null);
  const [phone, setPhone] = useState(
    user?.phoneNumber && /^\+?\d{9,12}$/.test(user.phoneNumber)
      ? user.phoneNumber
      : "",
  );
  const note = cart.note ?? "";
  const revisionMetadata = journey.data?.find(
    (item) => item.kind === "combo" && item.id === cart.revisionOfId,
  );
  const revisionAllowed =
    !cart.revisionOfId || revisionMetadata?.capabilities.canEdit === true;
  const submit = useSubmitCatalogCombo();
  const currentTemplate = [...(data?.templates ?? []), ...(catalog.data?.selectedTemplate ? [catalog.data.selectedTemplate] : [])].find(template => template.id === cart.templateId);
  const fixedCart = cart.pricingMode === "FixedPrice" || currentTemplate?.pricingMode === "FixedPrice";
  const options = useMemo(
    () =>
      new Map(
        [...(data?.categories.flatMap((category) =>
          category.options.map((item) => [item.serviceItemId, item] as const),
        ) ?? []), ...(catalog.data?.selectedOptions.map(item => [item.serviceItemId, item] as const) ?? []),
        ...(previewCategory?.options.map(item => [item.serviceItemId, item] as const) ?? []),
        ...(currentTemplate ? fixedComboOptions(currentTemplate).map(item => [item.serviceItemId, item] as const) : [])],
      ),
    [data, catalog.data?.selectedOptions, previewCategory, currentTemplate],
  );
  const categories = data?.categories ?? [];
  const vendorProfiles = new Map(
    data?.vendors?.map((vendor) => [vendor.id, vendor]) ?? [],
  );
  const fromListing = categories.find((c) =>
    c.options.some((i) => i.listingId === listingFilter),
  );
  const activeId = menu || catalog.data?.categoryId || fromListing?.categoryId || "templates";
  const activeCategory = categories.find((c) => c.categoryId === activeId);
  const lines = cart.selections.map((selection) => ({
    selection,
    item: options.get(selection.serviceItemId),
    categoryName:
      categories.find((c) => c.categoryId === selection.categoryId)?.name ??
      "Dịch vụ đã chọn",
  }));
  const vendors = data?.vendors?.map(v => ({ id: v.id, name: v.name })) ?? [];
  const cartVendorIds = [
    ...new Set(lines.flatMap((l) => (l.item ? [l.item.vendorId] : []))),
  ];
  const vendorCount = cartVendorIds.length;
  const mixedCart = vendorCount > 1;
  const selectedVendorId =
    vendorCount === 1
      ? cartVendorIds[0]
      : (cart.vendorId ?? cart.templateVendorId);
  const selectedVendor = vendors.find(
    (vendor) => vendor.id === selectedVendorId,
  );
  const selectedVendorProfile = selectedVendorId
    ? vendorProfiles.get(selectedVendorId)
    : undefined;
  const templates =
    data?.templates.filter(
      (template) =>
        !!template.ownerVendorId &&
        (!selectedVendorId || template.ownerVendorId === selectedVendorId),
    ) ?? [];
  const ready =
    !!cart.region.trim() &&
    !!cart.weddingDate &&
    Number.isInteger(cart.guestCount) &&
    cart.guestCount > 0;
  const missingInfo = !cart.region.trim()
    ? "Nhập khu vực tổ chức ở phần Thông tin cưới để tiếp tục."
    : !cart.weddingDate
      ? "Chọn ngày cưới ở phần Thông tin cưới để NCC kiểm tra còn lịch."
      : cart.guestCount <= 0
        ? "Nhập số khách ở phần Thông tin cưới để tính giá."
        : "";
  const price = useServerPrice(
    {
      templateId: cart.templateId,
      revisionOfId: cart.revisionOfId,
      region: cart.region,
      weddingDate: cart.weddingDate,
      guestCount: cart.guestCount,
      selections: cart.selections.map(
        ({ serviceItemId, variantIds, addOnIds, hours }) => ({
          serviceItemId,
          variantIds,
          addOnIds,
          hours,
        }),
      ),
    },
    ready && lines.length > 0 && lines.every((l) => l.item) && !mixedCart,
  );
  const matches = (item: CatalogOption) =>
    (!selectedVendorId || selectedVendorId === item.vendorId) &&
    (!listingFilter || listingFilter === item.listingId);
  const openCategory = (id: string) => {
    setMenu(id);
    setNotice("");
  };
  const categoryNavRef = useRef<HTMLElement>(null);
  useEffect(() => {
    const revealActiveCategory = () => {
      if (!window.matchMedia("(max-width: 960px)").matches) return;
      const nav = categoryNavRef.current;
      const active = nav?.querySelector<HTMLElement>('[aria-current="page"]');
      if (!nav || !active) return;
      const target = active.offsetLeft - nav.offsetLeft - (nav.clientWidth - active.clientWidth) / 2;
      nav.scrollTo({ left: Math.max(0, target), behavior: "auto" });
    };
    revealActiveCategory();
    // Resizing/rotating a device also changes the vertical sidebar into a rail.
    // Keep the already-selected category visible without changing the cart/filter.
    window.addEventListener("resize", revealActiveCategory);
    return () => window.removeEventListener("resize", revealActiveCategory);
  }, [activeId]);
  const changeVendor = (vendor: { id: string; name: string }) => {
    updateCart(scope, (previous) => startVendorCart(previous, vendor.id));
    setPendingVendor(null);
    changeFilters({ category: "templates", listing: undefined, search: undefined, filterVendorId: undefined });
    setNotice(
      `Đang chọn combo của ${vendor.name}. Bản nháp và yêu cầu đã lưu không bị thay đổi.`,
    );
  };
  const editLine = (categoryId: string) => {
    if (fixedCart) { setNotice("Gói cố định giữ nguyên thành phần. Gửi yêu cầu trước, rồi chat với NCC để nhận đề xuất thay đổi riêng cho đơn."); return; }
    changeFilters({ category: categoryId, listing: undefined, search: undefined,
      minServices: undefined, maxServices: undefined, minSavings: undefined, templateCategoryId: undefined });
    setCartOpen(false);
    const selection = cart.selections.find((s) => s.categoryId === categoryId);
    const item = selection && options.get(selection.serviceItemId);
    if (item) setConfigItem({ item, categoryId });
  };
  const addService = (selection: CartSelection) => {
    if (fixedCart) { setConfigItem(null); setNotice("Không thể thêm/bớt riêng trong gói cố định. Bạn có thể trao đổi và xác nhận đề xuất riêng với NCC sau khi gửi yêu cầu."); return; }
    const item = options.get(selection.serviceItemId) ??
      (configItem?.item.serviceItemId === selection.serviceItemId ? configItem.item : undefined);
    if (!item) { setConfigItem(null); setNotice("Dịch vụ vừa chọn không còn trong danh sách. Hãy tải lại trước khi chọn tiếp."); return; }
    if (mixedCart || (selectedVendorId && item.vendorId !== selectedVendorId)) {
      setConfigItem(null);
      setNotice(
        "Mỗi combo chỉ có một NCC. Hãy chọn lại NCC và bắt đầu giỏ mới.",
      );
      return;
    }
    updateCart(scope, (previous) =>
      selectService(
        { ...previous, vendorId: selectedVendorId ?? null },
        selection,
        item.vendorId,
      ),
    );
    setConfigItem(null);
    setNotice(`Đã cập nhật ${item.title} vào combo của ${item.vendorName}.`);
  };
  const review = () => {
    if (
      mixedCart ||
      !price.data ||
      price.isFetching ||
      price.isError ||
      !revisionAllowed
    )
      return;
    setCartOpen(false);
    setConsent(false);
    setReviewOpen(true);
    submit.reset();
  };
  const send = (draft: boolean) => {
    if (!user) {
      navigate("/login", { state: { from: "/combo" } });
      return;
    }
    if (
      mixedCart ||
      !price.data ||
      price.isFetching ||
      price.isError ||
      !revisionAllowed
    )
      return;
    if (
      cart.revisionOfId &&
      (cart.revisionReason?.trim().length ?? 0) < 10
    )
      return;
    submit.mutate(
      {
        templateId: cart.templateId,
        region: cart.region,
        weddingDate: cart.weddingDate,
        guestCount: cart.guestCount,
        selections: cart.selections,
        expectedTotal: price.data.total,
        phoneNumber: phone || undefined,
        note,
        saveAsDraft: draft,
        draftId: cart.draftId,
        revisionOfId: cart.revisionOfId,
        revisionReason: cart.revisionReason?.trim(),
      },
      {
        onSuccess: (order) => {
          setDone(order);
          setReviewOpen(false);
          if (draft) updateCart(scope, { draftId: order.id });
          else clearCart(scope);
        },
      },
    );
  };
  const cartPanel = (
    <ComboCartPanel
      cart={fixedCart ? { ...cart, pricingMode: "FixedPrice" } : cart}
      lines={lines}
      price={mixedCart ? undefined : price.data}
      busy={price.isFetching}
      priceError={price.isError}
      errorMessage={
        mixedCart
          ? "Giỏ cũ chứa nhiều NCC. Chọn một NCC bên dưới để tạo combo hợp lệ; bản đã lưu vẫn được giữ nguyên."
          : price.isError
            ? apiError(price.error)
            : missingInfo || undefined
      }
      onEdit={editLine}
      onRemove={(id) =>
        fixedCart ? setNotice("Gói cố định giữ nguyên thành phần; hãy trao đổi thay đổi riêng với NCC.") : updateCart(scope, (previous) => removeService(previous, id))
      }
      onReview={review}
      onClear={() => {
        setCartOpen(false);
        setClearOpen(true);
      }}
    />
  );

  if (isPending)
    return (
      <main className="wd wd-page wm-combo">
        <div className="container combo-loading" role="status">
          <ShoppingBag size={32} />
          <p>Đang mở thực đơn dịch vụ cưới...</p>
        </div>
      </main>
    );
  if ((isError && !data) || !data)
    return (
      <main className="wd wd-page wm-combo">
        <div className="container combo-empty">
          <PackageOpen size={40} />
          <h1>Chưa mở được thực đơn dịch vụ</h1>
          <p>
            {catalogErrorMessage(catalog.error, import.meta.env.DEV)} Giỏ của bạn
            vẫn được giữ nguyên.
          </p>
          <button className="combo-primary" onClick={() => refetch()}>
            Thử lại
          </button>
        </div>
      </main>
    );
  if (routeOrder) {
    const unavailable =
      !journey.isPending &&
      (journey.isError || !intentMetadata?.capabilities.canEdit);
    return (
      <main className="wd wd-page wm-combo">
        <div className="container combo-empty">
          <PackageOpen size={36} />
          <h1>
            {unavailable
              ? "Chưa thể chỉnh combo này"
              : journey.isPending
                ? "Đang kiểm tra trạng thái combo..."
                : "Mở combo để chỉnh sửa"}
          </h1>
          <p>
            {unavailable
              ? intentMetadata?.capabilities.blockedReason ||
                "Không tải được quyền chỉnh sửa hoặc yêu cầu đã thay đổi trạng thái. Giỏ hiện tại được giữ nguyên."
              : "Bản đã gửi chưa thay đổi. Chỉ khi gửi bản chỉnh sửa thành công, hệ thống mới thay thế yêu cầu cũ."}
          </p>
          {unavailable && (
            <button
              className="combo-secondary"
              onClick={() => void journey.refetch()}
            >
              Kiểm tra lại
            </button>
          )}
          <Link
            className="combo-secondary"
            to="/customer/dashboard?tab=combos"
            replace
          >
            Quay lại combo của bạn
          </Link>
        </div>
        {!unavailable && !journey.isPending && !replaceApproved && (
          <ComboDialog
            title="Mở combo để chỉnh sửa?"
            onClose={() =>
              navigate("/customer/dashboard?tab=combos", { replace: true })
            }
          >
            <p className="combo-preview-copy">
              Giỏ đang chỉnh có {cart.selections.length} dịch vụ. Mở{" "}
              {routeOrder.code} sẽ thay các lựa chọn trong giỏ này; bản đã gửi
              và bản nháp đã lưu không bị xóa.
            </p>
            <div className="combo-actions">
              <button
                className="combo-secondary"
                onClick={() =>
                  navigate("/customer/dashboard?tab=combos", { replace: true })
                }
              >
                Giữ giỏ hiện tại
              </button>
              <button
                className="combo-primary"
                onClick={() => setReplaceApproved(true)}
              >
                Mở để chỉnh sửa
              </button>
            </div>
          </ComboDialog>
        )}
      </main>
    );
  }
  if (done)
    return (
      <main className="wd wd-page wm-combo">
        <div className="container combo-success">
          <CheckCheck size={44} />
          <p className="combo-eyebrow">{done.code}</p>
          <h1>
            {done.status === "Draft"
              ? "Đã lưu combo nháp"
              : submit.variables?.revisionOfId
                ? "Đã gửi bản chỉnh sửa cho NCC"
                : "Combo đã sẵn sàng để NCC báo giá"}
          </h1>
          <p>
            {done.status === "Draft"
              ? "Lựa chọn đã lưu trong tài khoản. Chưa gửi yêu cầu cho NCC và chưa thêm khoản dự trù."
              : `Đã gửi ${done.items.length} yêu cầu dịch vụ và thêm chi phí dự kiến vào Planner. Giá tham khảo có hiệu lực 7 ngày; chưa thu tiền.`}
          </p>
          <ul>
            {done.items.map((item) => (
              <li key={item.id}>
                <span>
                  {item.title}
                  <small>
                    {item.vendorName}
                    {item.voucherCode && ` · ${item.voucherCode}`}
                  </small>
                </span>
                <strong>{vnd(item.price - item.discountShare)}</strong>
              </li>
            ))}
          </ul>
          <strong className="combo-success-total">
            Tổng dự kiến: {vnd(done.total)}
          </strong>
          {done.status !== "Draft" && <ComboNextSteps sent />}
          <div className="combo-actions">
            {done.status !== "Draft" && (
              <Link
                className="combo-primary"
                to="/customer/dashboard?tab=contracts"
              >
                Trao đổi & nhận báo giá <ArrowRight size={16} />
              </Link>
            )}
            <Link className="combo-primary" to="/customer/dashboard?tab=combos">
              Theo dõi trong Planner <ArrowRight size={16} />
            </Link>
            <button
              className="combo-secondary"
              onClick={() => {
                setDone(null);
                if (done.status !== "Draft") setMenu("templates");
              }}
            >
              {done.status === "Draft" ? "Sửa tiếp bản nháp" : "Tạo combo khác"}
            </button>
          </div>
        </div>
      </main>
    );

  return (
    <main className="wd wd-page wm-combo">
      <div className="container combo-shell">
        <header className="combo-page-heading">
          <div>
            <p className="combo-eyebrow">MỘT GIỎ DỊCH VỤ · NGÀY CƯỚI CỦA BẠN</p>
            <h1>
              Tự chọn một ngày <em>thật trọn vẹn.</em>
            </h1>
            <p>
              Chọn gói combo có sẵn hoặc tự chọn dịch vụ theo từng danh mục.
              Một giỏ chỉ gồm dịch vụ của cùng NCC; gói cố định giữ nguyên thành phần đã duyệt.
            </p>
          </div>
          <Link to="/planner" className="combo-back">
            <ArrowLeft size={15} /> Về kế hoạch cưới
          </Link>
        </header>
        {cart.revisionOfId && (
          <div className="combo-notice" role="status">
            <SlidersHorizontal size={18} />
            <span>
              Đang chỉnh lại yêu cầu đã gửi. Bản cũ được giữ nguyên cho tới khi
              bạn gửi bản mới thành công; các báo giá cũ sẽ không còn dùng để
              chốt bản mới.{" "}
              {revisionAllowed
                ? "Bạn có thể lưu nháp để sửa tiếp."
                : revisionMetadata?.capabilities.blockedReason ||
                  "Đang kiểm tra lại quyền chỉnh sửa. Nếu đã phát sinh hợp đồng/cọc, cần gửi đề nghị điều chỉnh trong Hợp đồng & tư vấn."}
            </span>
          </div>
        )}
        <ol className="combo-journey" aria-label="Các bước tạo combo">
          <li className="active">
            <span>1</span> Chọn combo hoặc dịch vụ
          </li>
          <li className={lines.length ? "active" : ""}>
            <span>2</span> Xem lại lựa chọn
          </li>
          <li>
            <span>3</span> Kiểm tra & gửi yêu cầu
          </li>
        </ol>
        <section
          className="combo-basics"
          id="combo-event-info"
          aria-label="Thông tin cưới để tính giá"
        >
          <div>
            <Sparkles size={18} />
            <strong>
              {profile
                ? "Lấy từ kế hoạch cưới"
                : "Cho mình biết ngày vui của bạn"}
            </strong>
            <span>Có thể điều chỉnh bất cứ lúc nào.</span>
          </div>
          <div className="combo-basics-fields">
            <label>
              Khu vực
              <input
                className="input"
                value={cart.region}
                onChange={(e) => updateCart(scope, { region: e.target.value })}
                placeholder="VD: Hồ Chí Minh"
              />
            </label>
            <label>
              Ngày cưới
              <input
                className="input"
                type="date"
                value={cart.weddingDate}
                onChange={(e) =>
                  updateCart(scope, { weddingDate: e.target.value })
                }
              />
            </label>
            <label>
              Số khách
              <input
                className="input"
                type="number"
                min={1}
                max={10000}
                step={1}
                value={cart.guestCount || ""}
                onChange={(e) =>
                  updateCart(scope, {
                    guestCount: Math.max(
                      0,
                      Math.min(10000, Math.floor(Number(e.target.value) || 0)),
                    ),
                  })
                }
                placeholder="200"
              />
            </label>
          </div>
        </section>
        <section
          className="combo-vendor-picker"
          aria-label="Nhà cung cấp của combo"
        >
          <div>
            <p className="combo-eyebrow">MỖI COMBO · MỘT NHÀ CUNG CẤP</p>
            <h2>{selectedVendor?.name ?? "Chọn nhà cung cấp đồng hành"}</h2>
            {selectedVendorProfile && (
              <div className="combo-selected-shop">
                <ComboPhoto
                  sources={[selectedVendorProfile.logoUrl]}
                  alt={`Ảnh đại diện ${selectedVendorProfile.name}`}
                  placeholder={<Store size={22} />}
                />
                <div>
                  <span>
                    <MapPin size={12} />{" "}
                    {operatingAreaLabel(selectedVendorProfile.operatingRegions, selectedVendorProfile.nationwide, selectedVendorProfile.city)}
                  </span>
                  {selectedVendorProfile.isVerified && (
                    <span>
                      <BadgeCheck size={13} /> NCC đã xác minh
                    </span>
                  )}
                  {selectedVendorProfile.ratingAvg != null && (
                    <span>
                      {Number(selectedVendorProfile.ratingAvg).toFixed(1)}/5 ·{" "}
                      {selectedVendorProfile.totalReviews} đánh giá
                    </span>
                  )}
                </div>
              </div>
            )}
            <p>
              Chỉ thêm, đổi dịch vụ trong cùng NCC. Muốn chọn NCC khác, hãy bắt
              đầu giỏ mới.
            </p>
            {missingInfo && (
              <p className="combo-budget-warning" role="status">
                {missingInfo}{" "}
                <a href="#combo-event-info">Bổ sung thông tin ↑</a>
              </p>
            )}
          </div>
          <label>
            Nhà cung cấp
            <select
              className="input"
              value={selectedVendorId ?? ""}
              onChange={(e) => {
                const vendor = vendors.find((v) => v.id === e.target.value);
                if (!vendor || vendor.id === selectedVendorId) return;
                if (lines.length) setPendingVendor(vendor);
                else changeVendor(vendor);
              }}
            >
              <option value="" disabled>
                Chọn NCC, combo hoặc dịch vụ bên dưới
              </option>
              {vendors.map((vendor) => (
                <option key={vendor.id} value={vendor.id}>
                  {vendor.name}
                </option>
              ))}
            </select>
          </label>
        </section>
        {mixedCart && (
          <section className="combo-legacy-warning" role="alert">
            <h2>Giỏ cũ đang có dịch vụ từ nhiều NCC</h2>
            <p>
              Quy tắc mới: một combo chỉ có một NCC. Chọn NCC muốn giữ để bắt
              đầu giỏ mới; đơn và bản nháp cũ trong tài khoản không bị xóa.
            </p>
            <div className="combo-actions">
              {cartVendorIds.map((id) => (
                <button
                  key={id}
                  className="combo-secondary"
                  onClick={() => {
                    updateCart(scope, (previous) => ({
                      ...previous,
                      selections: previous.selections.filter(
                        (selection) =>
                          options.get(selection.serviceItemId)?.vendorId === id,
                      ),
                      vendorId: id,
                      templateId: null,
                      templateName: "",
                      templateVendorId: null,
                      draftId: undefined,
                    }));
                    setNotice(
                      "Đã tạo giỏ mới của một NCC. Bản nháp cũ vẫn còn trong tài khoản.",
                    );
                  }}
                >
                  Chỉ giữ{" "}
                  {vendors.find((vendor) => vendor.id === id)?.name ??
                    "NCC này"}
                </button>
              ))}
            </div>
          </section>
        )}
        {notice && (
          <div className="combo-notice" role="status">
            <Check size={16} />
            <span>{notice}</span>
            <button
              className="combo-icon-button"
              aria-label="Ẩn thông báo"
              onClick={() => setNotice("")}
            >
              <X size={16} />
            </button>
          </div>
        )}
        <div className="combo-workspace">
          <nav
            className="combo-category-nav"
            aria-label="Danh mục chọn dịch vụ"
            ref={categoryNavRef}
          >
            <p>DANH MỤC DỊCH VỤ</p>
            <button className="combo-service-entry" onClick={() => {
              const category = categories.find(item => catalog.data?.categoryCounts.some(count => count.categoryId === item.categoryId && count.count > 0));
              if (category) openCategory(category.categoryId);
            }} disabled={!categories.some(item => catalog.data?.categoryCounts.some(count => count.categoryId === item.categoryId && count.count > 0))}>
              <LayoutGrid size={18} /><span>Tự chọn dịch vụ<small>Theo danh mục</small></span>
            </button>
            <button
              className={activeId === "templates" ? "selected" : ""}
              aria-current={activeId === "templates" ? "page" : undefined}
              onClick={() => openCategory("templates")}
            >
              <Sparkles size={18} />
              <span>
                Combo của NCC<small>Bắt đầu nhanh</small>
              </span>
            </button>
            {categories.map((category) => {
              const selected = cart.selections.some(
                (line) => line.categoryId === category.categoryId,
              );
              return (
                <button
                  key={category.categoryId}
                  className={activeId === category.categoryId ? "selected" : ""}
                  aria-current={
                    activeId === category.categoryId ? "page" : undefined
                  }
                  onClick={() => openCategory(category.categoryId)}
                >
                  <LayoutGrid size={17} />
                  <span>
                    <span className="combo-category-full">{category.name}</span>
                    <span className="combo-category-short">
                      {categoryLabels[category.slug] ??
                        category.name.split("(")[0].trim()}
                    </span>
                    <small>
                      {
                        catalog.data?.categoryCounts.find(c => c.categoryId === category.categoryId)?.count ?? 0
                      }{" "}
                      lựa chọn
                    </small>
                  </span>
                  {selected && (
                    <Check size={15} className="combo-category-check" />
                  )}
                </button>
              );
            })}
          </nav>
          <section
            className="combo-menu"
            aria-label={activeCategory?.name ?? "Combo gợi ý"}
            ref={listRef}
            aria-busy={catalog.isFetching}
          >
            {catalog.isFetching && <p className="combo-catalog-status" role="status">Đang cập nhật danh sách...</p>}
            {catalog.isError && <p className="co-error" role="alert">Không tải được danh sách mới. Giỏ vẫn được giữ. <button className="combo-text-button" onClick={() => void refetch()}>Thử lại</button></p>}
            <ComboCatalogFilters key={location.search} filters={filters} categories={categories} vendors={data?.vendors ?? []}
              guests={data.previewGuestCount ?? 200} templateMode={activeId === "templates"}
              lockedVendorName={selectedVendor?.name} onChange={changeFilters} onReset={resetFilters} />
            <p className="combo-discovery-price-note" role="status">{catalog.data?.totalCount ?? 0} {activeId === "templates" ? "combo" : "dịch vụ"} phù hợp</p>
            {activeId === "templates" && <section className="combo-service-discovery" aria-label="Tự chọn dịch vụ lẻ">
              <div className="combo-section-heading">
                <div><p className="combo-eyebrow">KHÔNG CẦN CHỌN GÓI CÓ SẴN</p><h2>Tự chọn dịch vụ</h2>
                  <p>Xem ảnh, giá và chọn dịch vụ vào giỏ ngay tại đây. Các dịch vụ trong một giỏ thuộc cùng một nhà cung cấp.</p></div>
              </div>
              <div className="combo-service-category-links" aria-label="Chọn nhóm dịch vụ lẻ">
                {categories.map(category => <button key={category.categoryId} type="button" className="combo-secondary"
                  onClick={() => openCategory(category.categoryId)}>{categoryLabels[category.slug] ?? category.name.split("(")[0].trim()}
                  <small>{catalog.data?.categoryCounts.find(count => count.categoryId === category.categoryId)?.count ?? 0}</small></button>)}
              </div>
              {servicePreviewInput && servicePreview.isPending && <p role="status" className="combo-catalog-status">Đang tải dịch vụ lẻ...</p>}
              {servicePreviewInput && servicePreview.isError && <div role="alert" className="co-error">Không tải được dịch vụ lẻ. Giỏ vẫn được giữ.
                <button className="combo-text-button" onClick={() => void servicePreview.refetch()}>Thử tải dịch vụ</button></div>}
              {servicePreviewInput && servicePreview.isSuccess && <>
                <p className="combo-discovery-price-note">{previewCategory?.name} · Giá dịch vụ chưa áp ưu đãi combo. Các bộ lọc số thành phần và tiết kiệm chỉ áp dụng cho gói combo.</p>
                <ComboServiceCards items={previewCategory?.options.filter(matches) ?? []} categoryId={servicePreviewInput.categoryId!}
                  selections={cart.selections} guestCount={cart.guestCount} fixedCart={fixedCart}
                  onConfigure={(item, categoryId) => setConfigItem({ item, categoryId })} />
                {!previewCategory?.options.some(matches) && <p className="combo-catalog-status">Chưa có dịch vụ lẻ phù hợp bộ lọc. Bạn có thể chọn nhóm khác ở trên.</p>}
                <button className="combo-text-button" onClick={() => openCategory(servicePreviewInput.categoryId!)}>
                  Xem tất cả {servicePreview.data?.totalCount ?? 0} dịch vụ trong nhóm <ChevronRight size={15} />
                </button>
              </>}
              {!servicePreviewInput && <p className="combo-catalog-status">Chưa có dịch vụ lẻ khả dụng cho nhà cung cấp hoặc bộ lọc hiện tại. Bạn vẫn có thể xem các nhóm dịch vụ ở trên.</p>}
            </section>}
            {activeId === "templates" ? (
              <>
                <div className="combo-section-heading">
                  <div>
                    <p className="combo-eyebrow">CHỌN MỘT ĐIỂM BẮT ĐẦU</p>
                    <h2>Combo riêng của nhà cung cấp</h2>
                    <p>
                      Xem thành phần và giá gói do NCC công bố. Chọn nguyên gói
                      để gửi yêu cầu; thay đổi riêng cần được hai bên xác nhận.
                    </p>
                  </div>
                </div>
                <div className="combo-filters">
                  <button
                    className="combo-text-button"
                    onClick={() =>
                      categories[0] && openCategory(categories[0].categoryId)
                    }
                  >
                    Tự chọn dịch vụ của một NCC <ArrowRight size={14} />
                  </button>
                </div>
                <div className="combo-preset-grid">
                  {templates.map((template) => {
                    const estimate = catalog.data?.estimates?.find(item => item.templateId === template.id);
                    const fixedSummary = fixedComboSummary(template);
                    const isFixed = template.pricingMode === "FixedPrice";
                    const vendor = template.ownerVendorId
                      ? vendorProfiles.get(template.ownerVendorId)
                      : undefined;
                    const templateOptions = isFixed ? fixedComboOptions(template) : template.items.flatMap((slot) =>
                      slot.serviceItemId && options.get(slot.serviceItemId)
                        ? [options.get(slot.serviceItemId)!]
                        : [],
                    );
                    return (
                      <article
                        key={template.id}
                        className={`combo-preset-card ${template.tier === "Popular" ? "is-popular" : ""}`}
                      >
                        <Link
                          to={`/combo/${template.id}`}
                          state={{ comboCatalogReturn: `/combo${comboFilterSearch(filters)}` }}
                          className="combo-preset-cover"
                          aria-label={`Xem hình ảnh và chi tiết ${template.name}`}
                        >
                          <ComboPhoto
                            sources={[
                              template.imageUrl,
                              ...(template.content?.galleryUrls ?? []),
                              ...(!isFixed ? [vendor?.coverImageUrl] : []),
                              ...templateOptions.flatMap((item) => [
                                item.imageUrl,
                                ...(item.galleryUrls ?? []),
                              ]),
                            ]}
                            alt={`Hình ảnh ${template.name} — ${template.ownerVendorName ?? "NCC"}`}
                          />
                        </Link>
                        <div className="combo-preset-top">
                          <span>
                            {tierLabel[template.tier] ?? template.tier}
                          </span>
                          {template.tier === "Popular" && (
                            <small>GỢI Ý CHO BẠN</small>
                          )}
                        </div>
                        <h3>{template.name}</h3>
                        <p>{template.description}</p>
                        <div className="combo-preset-shop">
                          <ComboPhoto
                            sources={[vendor?.logoUrl]}
                            alt={`Ảnh đại diện ${template.ownerVendorName ?? "nhà cung cấp"}`}
                            placeholder={<Store size={17} />}
                          />
                          <div>
                            <strong>
                              {template.ownerVendorName ??
                                "Nhà cung cấp của combo"}
                            </strong>
                            <small>
                              {vendor && operatingAreaLabel(vendor.operatingRegions, vendor.nationwide, vendor.city)}
                              {vendor?.isVerified && (
                                <>
                                  <BadgeCheck size={12} /> Đã xác minh
                                </>
                              )}
                            </small>
                          </div>
                        </div>
                        <div className="combo-preset-tags">
                          <span>
                            {template.items.filter((i) => !i.isOptional).length}{" "}
                            hạng mục có sẵn
                          </span>
                        </div>
                        {(fixedSummary || !isFixed && estimate) && <div className="combo-preset-estimate">
                          <small>{fixedSummary ? "Giá nguyên gói đã duyệt" : `Dự kiến cho ${estimate!.guests.toLocaleString("vi-VN")} khách`}</small>
                          <strong>{vnd(fixedSummary?.price ?? estimate!.total)}</strong>
                          {fixedSummary ? <>
                            <small>Tổng giá bán lẻ: {vnd(fixedSummary.original)}</small>
                            {fixedSummary.saving > 0 && <p>Tiết kiệm {vnd(fixedSummary.saving)} · {fixedSummary.percentage.toLocaleString("vi-VN", { maximumFractionDigits: 2 })}%</p>}
                          </> : estimate!.savings > 0 && <p>Tiết kiệm {vnd(estimate!.savings)} so với dịch vụ có sẵn</p>}
                          {estimate?.shopRating != null && <span>Đánh giá shop: {estimate.shopRating.toLocaleString("vi-VN")} / 5</span>}
                        </div>}
                        <ul>
                          {template.items.slice(0, 4).map((item) => (
                            <li key={item.serviceItemId ?? item.categoryId}>
                              <ComboPhoto
                                sources={[
                                  item.serviceItemId
                                    ? templateOptions.find(option => option.serviceItemId === item.serviceItemId)?.imageUrl
                                    : undefined,
                                ]}
                                alt=""
                                placeholder={<LayoutGrid size={13} />}
                              />
                              <span>
                                {templateOptions.find(option => option.serviceItemId === item.serviceItemId)?.title ?? categories
                                  .find((c) => c.categoryId === item.categoryId)
                                  ?.name.split("(")[0]
                                  .trim() ?? "Dịch vụ cưới"}
                                {item.isOptional && " (tùy chọn)"}
                              </span>
                            </li>
                          ))}
                        </ul>
                        <Link
                          className="combo-secondary"
                          to={`/combo/${template.id}`}
                          state={{ comboCatalogReturn: `/combo${comboFilterSearch(filters)}` }}
                        >
                          Xem chi tiết combo <ChevronRight size={15} />
                        </Link>
                      </article>
                    );
                  })}
                </div>
                {templates.length === 0 && (
                  <div className="combo-empty">
                    <PackageOpen size={36} />
                    <h3>Chưa có combo phù hợp</h3>
                    <p>
                      Thử mở rộng bộ lọc hoặc tự chọn các dịch vụ của một NCC để xin báo giá. Giỏ hiện tại vẫn được giữ nguyên.
                    </p>
                    <button
                      className="combo-secondary"
                      onClick={() =>
                        categories[0] && openCategory(categories[0].categoryId)
                      }
                    >
                      Tự chọn dịch vụ
                    </button>
                    <button className="combo-text-button" onClick={resetFilters}>Xóa bộ lọc combo</button>
                  </div>
                )}
              </>
            ) : (
              <>
                <div className="combo-section-heading">
                  <div>
                    <p className="combo-eyebrow">CHỌN DỊCH VỤ CHO COMBO</p>
                    <h2>{activeCategory?.name ?? "Danh mục dịch vụ"}</h2>
                    <p>
                      {fixedCart ? "Giỏ đang dùng nguyên gói đã duyệt. Gửi yêu cầu rồi trao đổi với NCC nếu cần thay đổi riêng." : "Mỗi hạng mục có một lựa chọn. Bạn có thể thay gói và tùy chỉnh tiện ích của cùng nhà cung cấp trước khi gửi."}
                    </p>
                  </div>
                </div>
                {listingFilter && (
                  <p className="combo-notice">
                    Đang xem dịch vụ thuộc bài đăng bạn vừa chọn.{" "}
                    <button
                      className="combo-text-button"
                      onClick={() => setListingFilter("")}
                    >
                      Xem cả danh mục
                    </button>
                  </p>
                )}
                {activeCategory && <ComboServiceCards items={activeCategory.options.filter(matches)} categoryId={activeCategory.categoryId}
                  selections={cart.selections} guestCount={cart.guestCount} fixedCart={fixedCart}
                  onConfigure={(item, categoryId) => setConfigItem({ item, categoryId })} />}
                {!activeCategory?.options.some(matches) && (
                  <div className="combo-empty">
                    <PackageOpen size={36} />
                    <h3>Chưa có dịch vụ phù hợp bộ lọc</h3>
                    <p>
                      Chỉ hiển thị dịch vụ đã được duyệt của NCC đã xác minh và
                      đồng ý tham gia combo.
                    </p>
                    {(location.search) && (
                      <button
                        className="combo-secondary"
                        onClick={() => changeFilters({ category: activeId, listing: undefined, search: undefined,
                          minPrice: undefined, maxPrice: undefined, city: undefined, region: undefined, minServices: undefined,
                          maxServices: undefined, minSavings: undefined, minRating: undefined, templateCategoryId: undefined, filterVendorId: undefined })}
                      >
                        Bỏ bộ lọc
                      </button>
                    )}
                  </div>
                )}
              </>
            )}
            <Pagination page={catalog.data?.page ?? page} pageSize={pageSize} totalCount={catalog.data?.totalCount ?? 0} onPageChange={changePage} isLoading={catalog.isFetching} label="Phân trang combo và dịch vụ" />
            {lines.length > 0 && !mixedCart && (
              <div className="combo-next-step">
                <div>
                  <strong>
                    Đã chọn {lines.length} dịch vụ
                    {selectedVendor ? ` của ${selectedVendor.name}` : ""}
                  </strong>
                  <p>
                    Tiếp theo: kiểm tra combo và điền thông tin để xin báo giá.
                    Chưa thanh toán.
                  </p>
                  {missingInfo && (
                    <p role="status">
                      {missingInfo}{" "}
                      <a href="#combo-event-info">Bổ sung thông tin ↑</a>
                    </p>
                  )}
                </div>
                <button
                  className="combo-primary"
                  disabled={
                    !price.data ||
                    price.isFetching ||
                    price.isError ||
                    lines.some((line) => !line.item)
                  }
                  onClick={review}
                >
                  Bước tiếp theo <ArrowRight size={16} />
                </button>
              </div>
            )}
          </section>
          <aside className="combo-desktop-cart">{cartPanel}</aside>
        </div>
        <div className="combo-mobile-cart">
          <button
            onClick={() => setCartOpen(true)}
            aria-label={`Mở giỏ combo, ${lines.length} dịch vụ`}
          >
            <ShoppingBag size={19} />
            <span>
              <strong>{lines.length} dịch vụ</strong>
              <small>
                {price.data ? vnd(price.data.total) : "Xem giỏ của bạn"}
              </small>
            </span>
            <ArrowRight size={18} />
          </button>
        </div>
        {price.isError && (
          <div className="combo-price-error">
            <ErrorText>{apiError(price.error)}</ErrorText>
          </div>
        )}
      </div>
      {configItem && !fixedCart && (
        <ServiceConfigurator
          key={configItem.item.serviceItemId}
          item={configItem.item}
          categoryId={configItem.categoryId}
          guests={cart.guestCount}
          current={cart.selections.find(
            (s) => s.serviceItemId === configItem.item.serviceItemId,
          )}
          replacedTitle={
            lines.find(
              (l) =>
                l.selection.categoryId === configItem.categoryId &&
                l.selection.serviceItemId !== configItem.item.serviceItemId,
            )?.item?.title
          }
          onSave={addService}
          onClose={() => setConfigItem(null)}
          onBrowseAlternatives={() => {
            changeFilters({ category: configItem.categoryId, listing: undefined, search: undefined,
              minServices: undefined, maxServices: undefined, minSavings: undefined, templateCategoryId: undefined });
            setConfigItem(null);
            setCartOpen(false);
            setNotice(
              "Chọn dịch vụ khác trong danh mục này để thay lựa chọn cũ trong giỏ.",
            );
          }}
        />
      )}
      {cartOpen && (
        <ComboDialog
          title="Giỏ combo của bạn"
          onClose={() => setCartOpen(false)}
        >
          {cartPanel}
        </ComboDialog>
      )}
      {pendingVendor && (
        <ComboDialog
          title="Đổi sang nhà cung cấp khác?"
          onClose={() => setPendingVendor(null)}
        >
          <p className="combo-preview-copy">
            Mỗi combo chỉ có một NCC. Chọn {pendingVendor.name} sẽ bắt đầu giỏ
            mới và bỏ các lựa chọn đang chỉnh. Đơn đã gửi và bản nháp đã lưu
            không bị xóa.
          </p>
          <div className="combo-actions">
            <button
              className="combo-secondary"
              onClick={() => setPendingVendor(null)}
            >
              Giữ NCC hiện tại
            </button>
            <button
              className="combo-primary"
              onClick={() => changeVendor(pendingVendor)}
            >
              Bắt đầu giỏ của NCC mới
            </button>
          </div>
        </ComboDialog>
      )}
      {clearOpen && (
        <ComboDialog
          title="Làm lại giỏ combo?"
          onClose={() => setClearOpen(false)}
        >
          <p className="combo-preview-copy">
            Chỉ xóa lựa chọn trong giỏ đang chỉnh. Các đơn đã gửi và bản nháp
            trong tài khoản vẫn được giữ.
          </p>
          <div className="combo-actions">
            <button
              className="combo-secondary"
              onClick={() => setClearOpen(false)}
            >
              Giữ giỏ hiện tại
            </button>
            <button
              className="combo-primary"
              onClick={() => {
                updateCart(scope, (previous) => ({
                  ...EMPTY_CART,
                  region: previous.region,
                  weddingDate: previous.weddingDate,
                  guestCount: previous.guestCount,
                }));
                setClearOpen(false);
                setMenu("templates");
              }}
            >
              Chọn lại từ đầu
            </button>
          </div>
        </ComboDialog>
      )}
      {reviewOpen && (
        <ComboDialog
          title="Bước tiếp theo: Kiểm tra & liên hệ"
          wide
          onClose={() => {
            if (!submit.isPending) setReviewOpen(false);
          }}
        >
          <p className="combo-preview-copy">
            {cart.guestCount} khách · {cart.region} ·{" "}
            {cart.weddingDate.split("-").reverse().join("/")}
            <br />
            NCC: {selectedVendor?.name ?? "Nhà cung cấp đã chọn"}
          </p>
          <details className="combo-review-details">
            <summary>
              Xem {lines.length} dịch vụ đã chọn & chính sách hủy
            </summary>
            <ul className="combo-review-lines">
              {price.data?.lines.map((line) => (
                <li key={line.serviceItemId}>
                  <div>
                    <h3>
                      {line.title} × {line.quantity} {unitName[line.unit]}
                    </h3>
                    <p>
                      {line.vendorName} · {line.availability}
                    </p>
                    <details>
                      <summary>Chính sách hủy</summary>
                      <p>{line.cancellationPolicy}</p>
                    </details>
                  </div>
                  <strong>{vnd(line.total)}</strong>
                </li>
              ))}
            </ul>
          </details>
          <div className="combo-review-total">
            <span>Tổng dự kiến sau ưu đãi</span>
            <strong>
              {price.data ? vnd(price.data.total) : "Đang tính lại..."}
            </strong>
          </div>
          <p className="combo-notice">
            Đây là yêu cầu báo giá, chưa phải thanh toán. Giá được kiểm tra lại
            khi gửi; NCC cần xác nhận còn lịch và gửi báo giá chính thức.
          </p>
          <ComboNextSteps />
          <div className="combo-review-contact">
            {cart.revisionOfId && (
              <label>
                Lý do chỉnh sửa (ít nhất 10 ký tự)
                <textarea
                  className="input"
                  value={cart.revisionReason ?? ""}
                  onChange={(event) =>
                    updateCart(scope, { revisionReason: event.target.value })
                  }
                  maxLength={1000}
                  rows={2}
                  placeholder="Ví dụ: đổi gói chụp ảnh và giảm số khách..."
                />
              </label>
            )}
            <label>
              Số điện thoại nhận tư vấn
              <input
                className="input"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                type="tel"
                placeholder="09xxxxxxxx"
              />
            </label>
            <label>
              Ghi chú cho NCC
              <textarea
                className="input"
                value={note}
                onChange={(e) => updateCart(scope, { note: e.target.value })}
                rows={2}
                placeholder="Điều hai bạn muốn ưu tiên..."
              />
            </label>
          </div>
          <label className="combo-consent">
            <input
              type="checkbox"
              checked={consent}
              onChange={(e) => setConsent(e.target.checked)}
            />
            <span>
              Tôi đã đọc chính sách hủy và hiểu cần duyệt báo giá trước khi ký
              hợp đồng.
            </span>
          </label>
          <ErrorText>
            {submit.isError
              ? apiError(submit.error)
              : price.isError
                ? apiError(price.error)
                : ""}
          </ErrorText>
          <div className="combo-actions">
            <button
              className="combo-secondary"
              disabled={submit.isPending}
              onClick={() => setReviewOpen(false)}
            >
              Quay lại chỉnh combo
            </button>
            <button
              className="combo-secondary"
              disabled={
                submit.isPending ||
                !revisionAllowed ||
                (!!cart.revisionOfId &&
                  (cart.revisionReason?.trim().length ?? 0) < 10) ||
                !price.data ||
                price.isFetching ||
                price.isError
              }
              onClick={() => send(true)}
            >
              {submit.isPending && submit.variables?.saveAsDraft
                ? "Đang lưu nháp..."
                : "Lưu bản nháp"}
            </button>
            <button
              className="combo-primary"
              disabled={
                submit.isPending ||
                !revisionAllowed ||
                (!!cart.revisionOfId &&
                  (cart.revisionReason?.trim().length ?? 0) < 10) ||
                !consent ||
                !price.data ||
                price.isFetching ||
                price.isError ||
                !/^\+?\d{9,12}$/.test(phone)
              }
              onClick={() => send(false)}
            >
              {submit.isPending && !submit.variables?.saveAsDraft
                ? "Đang gửi..."
                : cart.revisionOfId
                  ? "Gửi bản chỉnh sửa cho NCC"
                  : "Gửi yêu cầu cho NCC"}
              <ArrowRight size={16} />
            </button>
          </div>
        </ComboDialog>
      )}
    </main>
  );
}

export function ComboStatusList({ orders }: { orders: ComboOrder[] }) {
  return (
    <ul className="grid gap-3">
      {orders.map((order) => (
        <li
          key={order.id}
          className="rounded-2xl border border-[var(--line)] p-4 bg-[var(--paper)]"
        >
          <div className="flex flex-wrap justify-between gap-2">
            <p className="font-bold">
              {order.code} <StatusPill status={order.status} />
            </p>
            <p className="text-sm">{vnd(order.total)}</p>
          </div>
          <ol className="mt-3 grid sm:grid-cols-2 gap-2">
            {order.items.map((item) => (
              <li
                key={item.id}
                className="text-sm flex justify-between gap-2 bg-white rounded-xl border border-[var(--line)] px-3 py-2"
              >
                <span>
                  <b>{item.vendorName}</b> · {item.title}
                </span>
                {order.status === "Draft" ? (
                  <span className="muted">Chưa gửi NCC</span>
                ) : (
                  <StatusPill status={item.status} />
                )}
              </li>
            ))}
          </ol>
        </li>
      ))}
    </ul>
  );
}
