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
  Search,
  ShoppingBag,
  SlidersHorizontal,
  Sparkles,
  Store,
  X,
} from "lucide-react";
import axiosClient from "../../../services/axiosClient";
import { useAuthStore } from "../../../store/authStore";
import { useComboCartStore } from "../../../store/comboCartStore";
import { apiError, type ComboOrder, type PlannerOverview } from "../api";
import {
  useCatalogBuilder,
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
import { useCustomerOrderJourney } from "../customerOrders/customerOrderApi";
import "../../../styles/combo-builder.css";

const normalize = (s: string) =>
  s
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .toLowerCase();
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
  "wedding-planner": "Wedding Planner",
};

export default function ComboBuilderPage() {
  const location = useLocation();
  const userId = useAuthStore((s) => s.user?.id ?? "guest");
  // A route draft can initialize a new editing session without losing the durable cart on other pages.
  return <ComboBuilder key={`${userId}:${location.key}`} />;
}
function ComboBuilder() {
  const user = useAuthStore((s) => s.user);
  const scope = user?.id ?? "guest";
  const cart = useComboCartStore((s) => s.carts[scope] ?? EMPTY_CART);
  const updateCart = useComboCartStore((s) => s.update);
  const clearCart = useComboCartStore((s) => s.clear);
  const { data, isPending, isError, refetch } = useCatalogBuilder();
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
  const params = new URLSearchParams(location.search);
  const routeDraft = (location.state as { draft?: SavedCombo } | null)?.draft;
  const routeRevision = (location.state as { revision?: SavedCombo } | null)
    ?.revision;
  const routeOrder = routeRevision ?? routeDraft;
  const intentMetadata = journey.data?.find(
    (item) => item.kind === "combo" && item.id === routeOrder?.id,
  );
  const [replaceApproved, setReplaceApproved] = useState(
    !cart.selections.length,
  );
  const loadedDraft = useRef(false);
  const prefilledProfile = useRef(false);
  useEffect(() => {
    if (
      !routeOrder ||
      loadedDraft.current ||
      !data ||
      !intentMetadata?.capabilities.canEdit
    )
      return;
    if (!replaceApproved) return;
    loadedDraft.current = true;
    const template = data.templates.find(
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

  const [menu, setMenu] = useState(params.get("category") ?? "");
  const [listingFilter, setListingFilter] = useState(
    params.get("listing") ?? "",
  );
  const [query, setQuery] = useState("");
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
      ? "Đã chọn combo. Bạn có thể sửa dịch vụ rồi bấm “Bước tiếp theo” để kiểm tra và xin báo giá."
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
  const options = useMemo(
    () =>
      new Map(
        data?.categories.flatMap((category) =>
          category.options.map((item) => [item.serviceItemId, item] as const),
        ) ?? [],
      ),
    [data],
  );
  const categories = data?.categories ?? [];
  const vendorProfiles = new Map(
    data?.vendors?.map((vendor) => [vendor.id, vendor]) ?? [],
  );
  const fromListing = categories.find((c) =>
    c.options.some((i) => i.listingId === listingFilter),
  );
  const activeId = menu || fromListing?.categoryId || "templates";
  const activeCategory = categories.find((c) => c.categoryId === activeId);
  const lines = cart.selections.map((selection) => ({
    selection,
    item: options.get(selection.serviceItemId),
    categoryName:
      categories.find((c) => c.categoryId === selection.categoryId)?.name ??
      "Dịch vụ đã chọn",
  }));
  const vendors = [
    ...new Map(
      [...options.values()].map((item) => [
        item.vendorId,
        { id: item.vendorId, name: item.vendorName },
      ]),
    ).values(),
  ];
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
    (!listingFilter || listingFilter === item.listingId) &&
    (!query ||
      normalize(`${item.title} ${item.vendorName}`).includes(normalize(query)));
  const openCategory = (id: string) => {
    setMenu(id);
    setListingFilter("");
    setNotice("");
  };
  const categoryNavRef = useRef<HTMLElement>(null);
  useEffect(() => {
    if (!window.matchMedia("(max-width: 960px)").matches) return;
    const nav = categoryNavRef.current;
    const active = nav?.querySelector<HTMLElement>('[aria-current="page"]');
    if (!nav || !active) return;
    const target =
      active.offsetLeft -
      nav.offsetLeft -
      (nav.clientWidth - active.clientWidth) / 2;
    nav.scrollTo({ left: Math.max(0, target), behavior: "auto" });
  }, [activeId]);
  const changeVendor = (vendor: { id: string; name: string }) => {
    updateCart(scope, (previous) => startVendorCart(previous, vendor.id));
    setPendingVendor(null);
    setListingFilter("");
    setQuery("");
    setMenu("templates");
    setNotice(
      `Đang chọn combo của ${vendor.name}. Bản nháp và yêu cầu đã lưu không bị thay đổi.`,
    );
  };
  const editLine = (categoryId: string) => {
    setMenu(categoryId);
    setListingFilter("");
    setQuery("");
    setCartOpen(false);
    const selection = cart.selections.find((s) => s.categoryId === categoryId);
    const item = selection && options.get(selection.serviceItemId);
    if (item) setConfigItem({ item, categoryId });
  };
  const addService = (selection: CartSelection) => {
    const item = options.get(selection.serviceItemId)!;
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
      cart={cart}
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
        updateCart(scope, (previous) => removeService(previous, id))
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
  if (isError || !data)
    return (
      <main className="wd wd-page wm-combo">
        <div className="container combo-empty">
          <PackageOpen size={40} />
          <h1>Chưa mở được thực đơn dịch vụ</h1>
          <p>
            Kiểm tra kết nối với API rồi thử lại. Giỏ của bạn vẫn được giữ
            nguyên.
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
              Chọn combo do một nhà cung cấp tạo, rồi tùy chỉnh các dịch vụ của
              chính NCC đó. Không ghép dịch vụ từ nhiều NCC vào cùng combo.
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
            <span>1</span> Chọn combo NCC
          </li>
          <li className={lines.length ? "active" : ""}>
            <span>2</span> Tùy chỉnh dịch vụ
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
                    {selectedVendorProfile.city || "Chưa cập nhật khu vực"}
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
                Chọn NCC hoặc một combo bên dưới
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
                        category.options.filter(
                          (item) =>
                            !selectedVendorId ||
                            item.vendorId === selectedVendorId,
                        ).length
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
          >
            {activeId === "templates" ? (
              <>
                <div className="combo-section-heading">
                  <div>
                    <p className="combo-eyebrow">CHỌN MỘT ĐIỂM BẮT ĐẦU</p>
                    <h2>Combo riêng của nhà cung cấp</h2>
                    <p>
                      Chọn gói do NCC tạo. Sau đó thêm, bớt hoặc đổi dịch vụ
                      trong danh mục của cùng NCC.
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
                    const vendor = template.ownerVendorId
                      ? vendorProfiles.get(template.ownerVendorId)
                      : undefined;
                    const templateOptions = template.items.flatMap((slot) =>
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
                          className="combo-preset-cover"
                          aria-label={`Xem hình ảnh và chi tiết ${template.name}`}
                        >
                          <ComboPhoto
                            sources={[
                              template.imageUrl,
                              vendor?.coverImageUrl,
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
                              {vendor?.city}
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
                        <ul>
                          {template.items.slice(0, 4).map((item) => (
                            <li key={item.categoryId}>
                              <ComboPhoto
                                sources={[
                                  item.serviceItemId
                                    ? options.get(item.serviceItemId)?.imageUrl
                                    : undefined,
                                ]}
                                alt=""
                                placeholder={<LayoutGrid size={13} />}
                              />
                              <span>
                                {categories
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
                    <h3>NCC chưa có combo được duyệt</h3>
                    <p>
                      Bạn vẫn có thể chọn các dịch vụ của một NCC từ thanh danh
                      mục để xin báo giá.
                    </p>
                    <button
                      className="combo-secondary"
                      onClick={() =>
                        categories[0] && openCategory(categories[0].categoryId)
                      }
                    >
                      Tự chọn dịch vụ
                    </button>
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
                      Mỗi hạng mục có một lựa chọn. Bạn có thể thay gói và tùy
                      chỉnh tiện ích của cùng nhà cung cấp trước khi gửi.
                    </p>
                  </div>
                </div>
                <div className="combo-filters">
                  <label className="combo-search">
                    <Search size={16} />
                    <input
                      className="input"
                      value={query}
                      onChange={(e) => setQuery(e.target.value)}
                      placeholder="Tìm dịch vụ hoặc NCC..."
                      aria-label="Tìm dịch vụ trong danh mục"
                    />
                  </label>
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
                <div className="combo-product-grid">
                  {activeCategory?.options.filter(matches).map((item) => {
                    const selected = cart.selections.find(
                      (s) => s.serviceItemId === item.serviceItemId,
                    );
                    const current = cart.selections.some(
                      (s) => s.categoryId === activeCategory.categoryId,
                    );
                    return (
                      <article
                        key={item.serviceItemId}
                        className={`combo-product-card ${selected ? "is-selected" : ""}`}
                      >
                        <div className="combo-product-image">
                          <ComboPhoto
                            sources={[
                              item.imageUrl,
                              ...(item.galleryUrls ?? []),
                            ]}
                            alt={`${item.title} — ${item.vendorName}`}
                          />
                          {selected && (
                            <span>
                              <Check size={13} /> Trong giỏ
                            </span>
                          )}
                        </div>
                        <div className="combo-product-body">
                          <p className="combo-vendor-name">
                            <BadgeCheck size={13} />
                            {item.vendorName}
                          </p>
                          <h3>{item.title}</h3>
                          <p className="combo-product-capacity">
                            Phục vụ {item.minGuests}–{item.maxGuests} khách
                          </p>
                          <p className="combo-product-price">
                            <strong>{vnd(item.price)}</strong>/
                            {unitName[item.unit]}
                          </p>
                          {item.unit === "Table" && cart.guestCount > 0 && (
                            <p className="combo-product-capacity">
                              {cart.guestCount} khách →{" "}
                              {Math.ceil(cart.guestCount / 10)} bàn
                            </p>
                          )}
                          <button
                            className={
                              selected ? "combo-secondary" : "combo-primary"
                            }
                            onClick={() =>
                              setConfigItem({
                                item,
                                categoryId: activeCategory.categoryId,
                              })
                            }
                          >
                            <SlidersHorizontal size={14} />
                            {selected
                              ? "Tùy chỉnh / đổi"
                              : current
                                ? "Chọn để thay dịch vụ"
                                : "Chọn dịch vụ"}
                          </button>
                          <Link
                            to={`/listings/${item.listingId}`}
                            className="combo-product-link"
                          >
                            Xem nhà cung cấp
                          </Link>
                        </div>
                      </article>
                    );
                  })}
                </div>
                {!activeCategory?.options.some(matches) && (
                  <div className="combo-empty">
                    <PackageOpen size={36} />
                    <h3>Chưa có dịch vụ phù hợp bộ lọc</h3>
                    <p>
                      Chỉ hiển thị dịch vụ đã được duyệt của NCC đã xác minh và
                      đồng ý tham gia combo.
                    </p>
                    {(query || listingFilter) && (
                      <button
                        className="combo-secondary"
                        onClick={() => {
                          setQuery("");
                          setListingFilter("");
                        }}
                      >
                        Bỏ bộ lọc
                      </button>
                    )}
                  </div>
                )}
              </>
            )}
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
      {configItem && (
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
            setMenu(configItem.categoryId);
            setListingFilter("");
            setQuery("");
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
