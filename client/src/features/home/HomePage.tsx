import { useState } from "react";
import { Link } from "react-router-dom";
import {
  ArrowRight,
  ArrowUpRight,
  Check,
  CheckCheck,
  ChevronDown,
  FileCheck2,
  Heart,
  LayoutDashboard,
  Mail,
  MapPin,
  MessageCircle,
  Sparkles,
  Star,
  Users,
  Wallet,
} from "lucide-react";
import { dashboardPath } from "../../utils/navigation";
import { useAuthStore } from "../../store/authStore";
import {
  FeaturePreview,
  MobilePreview,
  PlannerPreview,
  type PreviewKind,
} from "./BrandPreviews";

const features = [
  {
    title: (
      <>
        Lên plan,
        <br />
        không lên stress
      </>
    ),
    description:
      "Checklist thông minh, công việc rõ ràng, quản lý ngân sách chi tiết.",
    preview: "checklist" as PreviewKind,
    to: "/customer/dashboard?tab=checklist",
    className: "navy-tint",
  },
  {
    title: (
      <>
        Khách mời đông,
        <br />
        quản lý vẫn gọn
      </>
    ),
    description:
      "Một nơi theo dõi RSVP, lời chúc và số lượng khách xác nhận tham dự.",
    preview: "guests" as PreviewKind,
    to: "/customer/dashboard?tab=invitations",
    className: "cream",
  },
  {
    title: (
      <>
        Thiệp cưới đẹp,
        <br />
        gửi trong một chạm
      </>
    ),
    description:
      "Thiệp cưới online đẹp mắt, gửi dễ dàng, theo dõi RSVP tức thì.",
    preview: "invitation" as PreviewKind,
    to: "/customer/dashboard?tab=invitations",
    className: "cream",
  },
  {
    title: (
      <>
        Vendor hợp gu,
        <br />
        khỏi tìm tới khuya
      </>
    ),
    description:
      "Khám phá dịch vụ, xem portfolio và gửi yêu cầu tư vấn dễ dàng.",
    preview: "vendors" as PreviewKind,
    to: "/browse",
    className: "navy-tint",
  },
];
const tools = [
  {
    label: "Checklist cưới",
    icon: CheckCheck,
    to: "/customer/dashboard?tab=checklist",
  },
  {
    label: "Ngân sách cưới",
    icon: Wallet,
    to: "/customer/dashboard?tab=budget",
  },
  {
    label: "Khách mời & RSVP",
    icon: Users,
    to: "/customer/dashboard?tab=invitations",
  },
  {
    label: "Thiệp cưới online",
    icon: Mail,
    to: "/customer/dashboard?tab=invitations",
  },
  { label: "Tìm nhà cung cấp", icon: Star, to: "/browse" },
  {
    label: "Chat & báo giá",
    icon: MessageCircle,
    to: "/customer/dashboard?tab=contracts",
  },
  {
    label: "Hợp đồng dịch vụ",
    icon: FileCheck2,
    to: "/customer/dashboard?tab=contracts",
  },
  {
    label: "Theo dõi thanh toán",
    icon: Wallet,
    to: "/customer/dashboard?tab=contracts",
  },
];
const faqs = [
  [
    "Wedding Match giúp hai bạn chuẩn bị cưới như thế nào?",
    "Hai bạn có thể theo dõi checklist, quản lý ngân sách, tạo thiệp online, nhận RSVP và tìm kiếm dịch vụ cưới ngay trong một tài khoản.",
  ],
  [
    "Có thể bắt đầu sử dụng miễn phí không?",
    "Bạn có thể đăng ký tài khoản để bắt đầu lập kế hoạch và sử dụng bộ công cụ cưới. Chi phí dịch vụ của nhà cung cấp được trao đổi riêng khi tư vấn.",
  ],
  [
    "Làm sao để gửi thiệp và nhận RSVP?",
    "Vào Thiệp mời, chọn mẫu, nhập thông tin và lưu thiệp. Chia sẻ đường dẫn thiệp cho người thân; khách có thể xác nhận tham dự và gửi lời chúc mà không cần đăng nhập.",
  ],
  [
    "Tôi có thể liên hệ nhà cung cấp ở đâu?",
    "Tại Dịch vụ, bạn có thể lọc gói theo ngành hàng, địa điểm và ngân sách. Mở chi tiết dịch vụ rồi gửi yêu cầu tư vấn để nhận mã ưu đãi và trao đổi với nhà cung cấp.",
  ],
  [
    "Thông tin và kế hoạch của tôi có được lưu lại không?",
    "Các công việc, chi phí, thiệp cưới và hợp đồng được lưu theo tài khoản. Bạn có thể đăng nhập lại để tiếp tục chỉnh sửa và theo dõi.",
  ],
];

export default function HomePage() {
  const { user, token } = useAuthStore();
  const startPath = token && user ? dashboardPath(user) : "/register";
  const [slide, setSlide] = useState(0);

  return (
    <main className="wm-home">
      <section className="wm-hero">
        <div className="hero-copy">
          <span className="hero-kicker">
            WEDDING <b>MATCH</b> · CHỌN ĐÚNG, CƯỚI AN TÂM
          </span>
          <h1>
            <span>Chọn đúng.</span>
            <em>Cưới yên tâm.</em>
          </h1>
          <p>
            Checklist, ngân sách, khách mời, thiệp cưới và vendor, tất cả{" "}
            <b>trong một trợ lý cưới</b> dành cho hai bạn.
          </p>
          <Link to={startPath} className="wm-button hero-cta">
            Bắt đầu lên plan miễn phí
          </Link>
          <div className="hero-promises">
            <span>Miễn phí</span>
            <i />
            <span>Dễ dùng</span>
            <i />
            <span>Dành cho mọi cặp đôi</span>
          </div>
        </div>
        <div className="hero-art">
          <PlannerPreview />
          <div className="milo-intro">
            <div>
              Tui là <b>Milo!</b>
              <small>
                Tui giúp mọi thứ
                <br />
                bớt rối nè!
              </small>
            </div>
            <img
              src="/wedding-match/milo.png"
              alt="Milo, người bạn đồng hành của hai bạn"
            />
          </div>
        </div>
      </section>

      <section className="all-in-one section-width" id="cong-cu">
        <div className="all-in-one-copy">
          <h2>
            Một đám cưới.
            <br />
            Trăm đầu việc.
            <br />
            <em>Không nhất thiết</em>
            <br />
            phải trăm cái note.
          </h2>
          <span className="little-note">Tui đã gom lại rồi nè. 😉</span>
          <img
            src="/wedding-match/milo.png"
            alt="Milo đang lên kế hoạch cưới"
            loading="lazy"
          />
        </div>
        <div className="tool-universe">
          <div className="universe-lines" />
          <div className="universe-center">
            <span>
              WEDDING <b>MATCH</b>
            </span>
            <em>
              Một nơi gọn lại
              <br />
              mọi đầu việc
            </em>
          </div>
          {tools.map(({ label, icon: Icon, to }, i) => (
            <Link className={`orbit-tool orbit-${i}`} key={label} to={to}>
              <span>
                <Icon size={16} />
              </span>
              {label}
            </Link>
          ))}
        </div>
      </section>

      <section className="features-section section-width">
        <div className="section-heading">
          <span className="wm-eyebrow">
            Một chút gọn gàng, nhiều hơn hạnh phúc
          </span>
          <h2>
            Chuẩn bị cưới nhẹ nhàng cùng Wedding Match{" "}
            <span className="heading-flower">✿</span>
          </h2>
        </div>
        <div className="feature-grid">
          {features.map((feature, i) => (
            <Link
              key={feature.preview}
              to={feature.to}
              className={`feature-card ${feature.className} ${slide === i ? "mobile-active" : ""}`}
            >
              <div className="feature-copy">
                <h3>{feature.title}</h3>
                <p>{feature.description}</p>
                <span className="feature-arrow">
                  <ArrowUpRight size={21} />
                </span>
              </div>
              <div className="feature-image">
                <FeaturePreview kind={feature.preview} />
              </div>
            </Link>
          ))}
        </div>
        <div className="feature-dots">
          {features.map((feature, i) => (
            <button
              key={feature.preview}
              aria-label={`Xem tính năng ${i + 1}`}
              aria-pressed={slide === i}
              className={slide === i ? "active" : ""}
              onClick={() => setSlide(i)}
            />
          ))}
        </div>
      </section>

      <section className="invitation-showcase section-width">
        <div className="section-heading">
          <span className="wm-eyebrow">
            Gửi một lời mời. Kể một chuyện tình.
          </span>
          <h2>
            Thiệp điện tử kể chuyện cưới.
            <br />
            Gửi trọn <em>yêu thương.</em>
          </h2>
          <p>
            Ảnh cưới, lời nhắn, bản đồ và RSVP gói gọn trong một lời mời online.
          </p>
        </div>
        <div className="invitation-scene">
          <div className="invitation-demo">
            <span className="demo-tag">MẪU MINH HỌA</span>
            <div className="invitation-demo-inner">
              <span>THE WEDDING OF</span>
              <h3>
                Anh Minh
                <br />
                <em>&</em>
                <br />
                Hoàng Yến
              </h3>
              <div className="invite-flower">❀</div>
              <p>20 . 12 . 2026</p>
              <small>THE GARDEN HALL</small>
              <div className="demo-rsvp">
                <Heart size={14} /> Cùng viết tiếp câu chuyện của chúng mình
              </div>
            </div>
          </div>
          <div className="invitation-side">
            <div className="invitation-side-icon">
              <Mail size={30} strokeWidth={1} />
            </div>
            <h3>
              Một lời mời,
              <br />
              ngàn yêu thương.
            </h3>
            <p>
              Chọn phong cách của hai bạn. Thêm câu chuyện tình yêu, chia sẻ
              đường dẫn và đón nhận lời chúc từ những người thân yêu.
            </p>
            <Link
              className="wm-button"
              to="/customer/dashboard?tab=invitations"
            >
              Thiết kế thiệp của bạn <ArrowRight size={17} />
            </Link>
            <div className="invitation-tags">
              {["RSVP", "Mẫu thiệp", "Lời chúc", "Bản đồ", "Mừng cưới"].map(
                (text) => (
                  <span key={text}>
                    <Check size={12} />
                    {text}
                  </span>
                ),
              )}
            </div>
          </div>
        </div>
      </section>

      <section className="together-section section-width">
        <div>
          <span className="wm-eyebrow">LUÔN Ở BÊN HAI BẠN</span>
          <h2>
            Lên kế hoạch mọi nơi.
            <br />
            <em>Hạnh phúc mọi lúc.</em>
          </h2>
          <p>
            Từ chiếc laptop ở nhà đến điện thoại trên tay. Kiểm tra công việc,
            cập nhật ngân sách và theo dõi phản hồi khách mời ngay trên trình
            duyệt.
          </p>
          <Link to={startPath} className="wm-button">
            Mở không gian cưới <ArrowRight size={17} />
          </Link>
        </div>
        <div className="mobile-scene">
          <div className="soft-orbit" />
          <MobilePreview />
          <span className="floating-note">
            <CheckCheck size={17} /> Từng bước, thật nhẹ nhàng
          </span>
        </div>
      </section>

      <section className="getting-started section-width">
        <div className="section-heading">
          <span className="wm-eyebrow">HÀNH TRÌNH CỦA HAI BẠN</span>
          <h2>
            Ngày trọng đại,
            <br />
            <em>bắt đầu từ những điều nhỏ.</em>
          </h2>
        </div>
        <div className="getting-started-grid">
          {[
            {
              icon: LayoutDashboard,
              title: "Một kế hoạch rõ ràng",
              text: "Bắt đầu từ checklist và ngân sách, để từng đầu việc đều có nơi của mình.",
              to: "/customer/dashboard",
              label: "Mở Planner",
            },
            {
              icon: Heart,
              title: "Một lời mời thật riêng",
              text: "Thiết kế thiệp theo câu chuyện của hai bạn và nhận RSVP ngay tại một nơi.",
              to: "/customer/dashboard?tab=invitations",
              label: "Thiết kế thiệp",
            },
            {
              icon: MapPin,
              title: "Một đối tác phù hợp",
              text: "Khám phá dịch vụ cưới, xem hình ảnh và trò chuyện cùng nhà cung cấp.",
              to: "/browse",
              label: "Tìm dịch vụ",
            },
          ].map(({ icon: Icon, ...item }) => (
            <Link key={item.title} to={item.to}>
              <Icon size={25} strokeWidth={1.5} />
              <h3>{item.title}</h3>
              <p>{item.text}</p>
              <span>
                {item.label} <ArrowUpRight size={15} />
              </span>
            </Link>
          ))}
        </div>
      </section>

      <section className="faq-section section-width">
        <div>
          <span className="wm-eyebrow">HỎI ĐÁP THƯỜNG GẶP</span>
          <h2>
            Giải đáp mọi
            <br />
            <em>thắc mắc</em>
          </h2>
          <p>
            Một chút giải đáp để hành trình cưới của hai bạn thêm dễ dàng và
            trọn vẹn.
          </p>
          <Sparkles size={38} strokeWidth={1} />
        </div>
        <div className="faq-list">
          {faqs.map(([question, answer]) => (
            <details key={question}>
              <summary>
                {question}
                <ChevronDown size={18} />
              </summary>
              <p>{answer}</p>
            </details>
          ))}
        </div>
      </section>
      <section className="closing-cta section-width">
        <Heart size={28} strokeWidth={1} />
        <h2>
          Đám cưới nhiều việc.
          <br />
          <em>Mình cùng lo, bạn nhé?</em>
        </h2>
        <Link to={startPath} className="wm-button">
          Bắt đầu lên plan miễn phí <ArrowRight size={17} />
        </Link>
      </section>
    </main>
  );
}
