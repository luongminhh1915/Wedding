import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  ArrowDown, ArrowRight, ArrowUpRight, Building2, CalendarHeart, Camera, Check,
  CheckCheck, ChevronDown, Flower2, Gift, Heart, MapPin, PartyPopper, Search,
  Shirt, Sparkles, Users, type LucideIcon,
} from "lucide-react";
import { dashboardPath } from "../../utils/navigation";
import { useAuthStore } from "../../store/authStore";
import { FeaturePreview, type PreviewKind } from "./BrandPreviews";
import { usePublicCategories, usePublicListings } from "../customer/hooks/useBrowse";
import type { CategoryDto } from "../../types/category.types";
import ListingCard from "../customer/components/ListingCard";
import { serviceCategoryLabel, serviceFilterSearch } from "../customer/serviceFilterModel";
import { marketplaceSearchFilters, type MarketplaceSearchDraft } from "./marketplaceSearchModel";
import "./home-romantic.css";

const features: { title: string; description: string; preview: PreviewKind; to: string; tone: string; icon: LucideIcon; action: string }[] = [
  { title: "Lên plan, không lên stress", description: "Từng công việc và khoản chi đều có nơi. Cùng nhau chuẩn bị, từng chút một.", preview: "checklist", to: "/customer/dashboard?tab=checklist", tone: "peach", icon: CheckCheck, action: "Mở checklist cưới" },
  { title: "Khách mời đông, quản lý vẫn gọn", description: "Danh sách khách, xếp bàn và phản hồi tham dự gọn lại trong một không gian.", preview: "guests", to: "/customer/dashboard?tab=guests", tone: "sage", icon: Users, action: "Quản lý khách mời" },
  { title: "Một lời mời thật riêng", description: "Thêm ảnh, kể câu chuyện của hai bạn và gửi thiệp online đến những người thương.", preview: "invitation", to: "/customer/dashboard?tab=invitations", tone: "rose", icon: Heart, action: "Thiết kế thiệp cưới" },
  { title: "Tìm người đồng hành hợp gu", description: "Xem dịch vụ, hình ảnh và trao đổi với nhà cung cấp trước khi đưa ra lựa chọn.", preview: "vendors", to: "/browse", tone: "butter", icon: Flower2, action: "Khám phá dịch vụ" },
];
const categoryStyles: Record<string, { icon: LucideIcon; tile: number; hint: string }> = {
  "quay-chup-photo-video": { icon: Camera, tile: 0, hint: "Lưu giữ khoảnh khắc" },
  "thiep-cuoi-qua-tang": { icon: Gift, tile: 1, hint: "Gửi trao yêu thương" },
  "tiec-su-kien-khac": { icon: PartyPopper, tile: 2, hint: "Thêm sắc màu ngày vui" },
  "wedding-planner": { icon: CalendarHeart, tile: 3, hint: "Có người cùng mình lo" },
  "trang-diem-makeup": { icon: Sparkles, tile: 4, hint: "Rạng rỡ theo cách của bạn" },
  "trang-tri-decor": { icon: Flower2, tile: 5, hint: "Đẹp từ những điều nhỏ" },
  "tiem-cuoi-venues": { icon: Building2, tile: 6, hint: "Nơi ngày vui bắt đầu" },
  "vay-cuoi-vest": { icon: Shirt, tile: 7, hint: "Dành cho ngày đặc biệt" },
};
const categoryIcons: Record<string, LucideIcon> = { Building2, CalendarHeart, Camera, Flower2, Gift, PartyPopper, Shirt, Sparkles };
const initialSearchDraft: MarketplaceSearchDraft = { categoryId: "", keyword: "", location: "", maxPrice: "" };
const faqs = [
  ["Wedding Match giúp hai bạn chuẩn bị cưới như thế nào?", "Hai bạn có thể lập checklist, quản lý ngân sách cá nhân, tạo thiệp online, theo dõi khách mời và tìm dịch vụ cưới trong cùng một tài khoản."],
  ["Tìm dịch vụ trên đây có cần thanh toán ngay không?", "Bạn có thể xem thông tin và gửi yêu cầu tư vấn trước. Thành phần dịch vụ, báo giá và hợp đồng được trao đổi với nhà cung cấp trong các bước tiếp theo."],
  ["Thông tin và kế hoạch của tôi có được lưu lại không?", "Các công việc, chi phí, thiệp cưới và yêu cầu dịch vụ được lưu theo tài khoản. Đăng nhập lại để tiếp tục chuẩn bị cùng nhau."],
];
function categoryStyle(category: CategoryDto) {
  return categoryStyles[category.slug] ?? { icon: categoryIcons[category.icon ?? ""] ?? Heart, tile: 5, hint: "Tìm lựa chọn dành cho bạn" };
}

export default function HomePage() {
  const { user, token } = useAuthStore();
  const startPath = token && user ? dashboardPath(user) : "/register";
  const navigate = useNavigate();
  const [marketplaceSearch, setMarketplaceSearch] = useState(initialSearchDraft);
  const categoryQuery = usePublicCategories();
  const categories = categoryQuery.data ?? [];
  const featuredListings = usePublicListings({ sortBy: "popular", page: 1, pageSize: 4 });
  const discoverSearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const filters = marketplaceSearchFilters(marketplaceSearch);
    navigate({ pathname: "/browse", search: serviceFilterSearch(filters) });
  };
  return <main className="wm-home wm-home-romantic">
    <section className="wm-hero marketplace-hero">
      <div className="marketplace-hero-top">
        <div className="home-hero-copy">
          <span className="home-eyebrow"><Flower2 size={15} strokeWidth={1.6} aria-hidden="true" />WEDDING MATCH · MARKETPLACE DỊCH VỤ CƯỚI</span>
          <h1><span>Tìm đúng dịch vụ.</span><em>Chọn đúng người đồng hành.</em></h1>
          <p>Khám phá dịch vụ cưới theo phong cách, khu vực và ngân sách của hai bạn. Bắt đầu từ một lựa chọn hợp gu, để ngày vui nhẹ nhàng hơn.</p>
          <div className="home-hero-actions"><Link to="/browse" className="home-primary-action">Khám phá dịch vụ <ArrowUpRight size={18} aria-hidden="true" /></Link><a href="#cong-cu" className="home-intro-action">Wedding Match có gì? <ArrowDown size={16} aria-hidden="true" /></a></div>
          <div className="home-hero-note"><span><Check size={13} aria-hidden="true" />Xem dịch vụ theo nhu cầu</span><span><Check size={13} aria-hidden="true" />Trao đổi trước khi đặt</span></div>
        </div>
        <figure className="home-hero-photo">
          <span className="home-photo-flower flower-one" aria-hidden="true"><Flower2 size={54} strokeWidth={1} /></span>
          <span className="home-photo-flower flower-two" aria-hidden="true"><Sparkles size={30} strokeWidth={1} /></span>
          <div className="home-hero-photo-frame"><img src="/wedding-match/homepage/hero-romantic-couple.png" alt="Ảnh minh họa cặp đôi giữa hoa tươi trong khu vườn cưới" width={1086} height={1448} fetchPriority="high" /><span className="home-photo-note">Một ngày thật đẹp,<br />theo cách của hai bạn.</span></div>
          <figcaption>Ảnh minh họa phong cách cưới ngoài trời.</figcaption>
        </figure>
      </div>
      <section id="kham-pha-dich-vu" className="marketplace-search-at-top home-marketplace-search" aria-label="Khám phá dịch vụ cưới">
        <form className="home-search-form" onSubmit={discoverSearch}>
          <div className="home-search-heading"><Search size={17} aria-hidden="true" /><strong>Bắt đầu tìm dịch vụ cho ngày vui</strong><span>Chọn điều phù hợp với hai bạn.</span></div>
          <div className="home-search-fields">
            <label><span>Danh mục dịch vụ</span><select aria-label="Danh mục dịch vụ" value={marketplaceSearch.categoryId} onChange={event => setMarketplaceSearch({ ...marketplaceSearch, categoryId: event.target.value })}><option value="">Tất cả dịch vụ</option>{categories.map(category => <option key={category.id} value={category.id}>{serviceCategoryLabel(category)}</option>)}</select></label>
            <label><span>Từ khóa</span><input maxLength={150} value={marketplaceSearch.keyword} onChange={event => setMarketplaceSearch({ ...marketplaceSearch, keyword: event.target.value })} placeholder="Chụp ảnh, trang trí..." /></label>
            <label><span>Khu vực</span><div className="home-search-location"><MapPin size={15} aria-hidden="true" /><input maxLength={100} value={marketplaceSearch.location} onChange={event => setMarketplaceSearch({ ...marketplaceSearch, location: event.target.value })} placeholder="Hà Nội, TP.HCM..." /></div></label>
            <label><span>Ngân sách tối đa</span><select aria-label="Ngân sách tối đa" value={marketplaceSearch.maxPrice} onChange={event => setMarketplaceSearch({ ...marketplaceSearch, maxPrice: event.target.value })}><option value="">Mọi mức giá</option><option value="10000000">Dưới 10 triệu</option><option value="30000000">Dưới 30 triệu</option><option value="80000000">Dưới 80 triệu</option><option value="150000000">Dưới 150 triệu</option><option value="300000000">Dưới 300 triệu</option></select></label>
            <button type="submit" className="home-primary-action"><Search size={17} aria-hidden="true" />Tìm dịch vụ</button>
          </div>
        </form>
      </section>
    </section>

    <section className="marketplace-categories section-width" aria-labelledby="marketplace-categories-title">
      <div className="home-section-heading"><div><span className="home-eyebrow">MỖI ĐIỀU NHỎ, MỘT LỰA CHỌN HỢP GU</span><h2 id="marketplace-categories-title">Khám phá dịch vụ <em>cho ngày cưới</em></h2></div><Link to="/browse" className="home-text-link">Xem tất cả dịch vụ <ArrowRight size={16} aria-hidden="true" /></Link></div>
      {categoryQuery.isPending ? <div className="home-category-grid" aria-label="Đang tải danh mục">{Array.from({ length: 8 }, (_, i) => <div key={i} className="home-category-skeleton" />)}</div> : categories.length ? <div className="home-category-grid">
        {categories.slice(0, 8).map(category => {
          const { icon: Icon, tile, hint } = categoryStyle(category);
          return <Link key={category.id} to={{ pathname: "/browse", search: serviceFilterSearch({ categoryId: category.id, sortBy: "popular" }) }} className={`home-category-card category-tile-${tile}`}><div className="home-category-copy"><span className="home-category-icon"><Icon size={22} strokeWidth={1.5} aria-hidden="true" /></span><h3>{serviceCategoryLabel(category)}</h3><p>{hint}</p><span className="home-category-arrow" aria-hidden="true"><ArrowUpRight size={16} /></span></div><span className={`home-category-photo tile-${tile}`} role="img" aria-label={`Ảnh minh họa ${serviceCategoryLabel(category)}`} /></Link>;
        })}
      </div> : <div className="home-inline-empty" role={categoryQuery.isError ? "alert" : "status"}><p>{categoryQuery.isError ? "Chưa tải được danh mục dịch vụ. Bạn có thể thử lại hoặc mở trang khám phá." : "Danh mục dịch vụ đang được cập nhật."}</p>{categoryQuery.isError && <button type="button" className="home-text-link" onClick={() => void categoryQuery.refetch()}>Thử lại</button>}<Link to="/browse" className="home-text-link">Mở trang khám phá <ArrowRight size={15} /></Link></div>}
      <p className="home-image-caption">Ảnh danh mục minh họa phong cách dịch vụ.</p>
    </section>

    <section className="marketplace-featured section-width" aria-labelledby="marketplace-featured-title">
      <div className="home-section-heading"><div><span className="home-eyebrow">CÙNG TÌM MỘT NGƯỜI ĐỒNG HÀNH</span><h2 id="marketplace-featured-title">Một lựa chọn đẹp. <em>Một ngày thật riêng.</em></h2><p>Xem thông tin và hình ảnh từ những dịch vụ đang hiển thị trên Wedding Match.</p></div><Link to="/browse" className="home-text-link">Khám phá marketplace <ArrowRight size={16} /></Link></div>
      {featuredListings.isLoading ? <div className="home-listing-grid" aria-label="Đang tải dịch vụ">{Array.from({ length: 4 }, (_, i) => <div key={i} className="home-listing-skeleton" />)}</div> : featuredListings.data?.items.length ? <div className="home-listing-grid">{featuredListings.data.items.map(listing => <ListingCard key={listing.id} listing={listing} returnPath="/" />)}</div> : <div className="home-marketplace-empty">
        <div className="home-marketplace-empty-icon"><Flower2 size={42} strokeWidth={1} aria-hidden="true" /></div><div><span className="home-eyebrow">TỪ MỘT ĐIỀU HỢP GU</span><h3>{featuredListings.isError ? "Chưa tải được danh sách dịch vụ" : "Marketplace đang được cập nhật"}</h3><p>{featuredListings.isError ? "Bạn có thể tải lại danh sách hoặc mở trang khám phá để tiếp tục tìm kiếm." : "Các dịch vụ sẽ xuất hiện khi nhà cung cấp đăng và được duyệt. Trong lúc đó, bạn có thể xem các danh mục hoặc bắt đầu kế hoạch cưới của riêng mình."}</p></div><div className="home-empty-actions">{featuredListings.isError ? <button type="button" className="home-primary-action" onClick={() => void featuredListings.refetch()}>Tải lại dịch vụ <ArrowRight size={16} /></button> : <Link to="/browse" className="home-primary-action">Khám phá dịch vụ <ArrowRight size={16} /></Link>}<Link to="/register?role=vendor" className="home-text-link">Bạn là nhà cung cấp? Tham gia cùng chúng mình <ArrowUpRight size={15} /></Link></div>
      </div>}
    </section>

    <section id="cong-cu" className="home-planner-section" aria-labelledby="home-planner-title">
      <div className="home-section-inner"><div className="home-section-heading is-centered"><span className="home-section-flower" aria-hidden="true"><Flower2 size={30} strokeWidth={1.2} /></span><span className="home-eyebrow">WEDDING PLANNER CỦA HAI BẠN</span><h2 id="home-planner-title">Chuẩn bị cưới <em>nhẹ nhàng hơn.</em></h2><p>Gom những đầu việc quan trọng vào một nơi, để có thêm thời gian tận hưởng hành trình của hai bạn.</p></div>
        <div className="home-planner-grid">{features.map(({ icon: Icon, ...feature }) => <Link key={feature.preview} to={feature.to} className={`home-planner-card tone-${feature.tone}`}><div className="home-planner-copy"><span className="home-planner-icon"><Icon size={22} strokeWidth={1.4} aria-hidden="true" /></span><h3>{feature.title}</h3><p>{feature.description}</p><span className="home-planner-action">{feature.action} <ArrowUpRight size={16} aria-hidden="true" /></span></div><div className="home-planner-preview"><span className="home-preview-note">MINH HỌA CÔNG CỤ</span><FeaturePreview kind={feature.preview} /></div></Link>)}</div>
        <div className="home-planner-start"><p>Bắt đầu từ kế hoạch của riêng hai bạn.</p><Link to={startPath} className="home-primary-action">{token && user ? "Về không gian của bạn" : "Tạo kế hoạch cưới"} <ArrowRight size={17} aria-hidden="true" /></Link></div>
      </div>
    </section>

    <section className="home-faq section-width" aria-labelledby="home-faq-title"><div><span className="home-eyebrow">MÌNH CÙNG BẮT ĐẦU NHÉ?</span><h2 id="home-faq-title">Một chút giải đáp,<br /><em>thêm phần an tâm.</em></h2><p>Những điều hai bạn có thể muốn biết trước khi bắt đầu.</p><Link to={startPath} className="home-text-link">Mở không gian Wedding Match <ArrowRight size={16} /></Link></div><div className="home-faq-list">{faqs.map(([question, answer]) => <details key={question}><summary>{question}<ChevronDown size={18} aria-hidden="true" /></summary><p>{answer}</p></details>)}</div></section>
    <section className="home-closing section-width"><span className="home-closing-flower" aria-hidden="true"><Flower2 size={54} strokeWidth={1} /></span><div><span className="home-eyebrow">MỘT KHỞI ĐẦU NHỎ, MỘT NGÀY THẬT ĐẸP</span><h2>Ngày vui của hai bạn.<br /><em>Mình cùng chuẩn bị nhé?</em></h2></div><Link to={startPath} className="home-primary-action">{token && user ? "Về không gian của bạn" : "Bắt đầu cùng Wedding Match"}<ArrowUpRight size={18} aria-hidden="true" /></Link><Heart size={66} className="home-closing-heart" strokeWidth={.8} aria-hidden="true" /></section>
  </main>;
}