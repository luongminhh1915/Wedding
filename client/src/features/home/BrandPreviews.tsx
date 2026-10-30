import {
  Check,
  CheckCheck,
  Heart,
  LayoutDashboard,
  Mail,
  MapPin,
  Users,
  Wallet,
} from "lucide-react";

// Presentational examples only: real planning tools remain on their existing routes.
const tasks = [
  "Chọn địa điểm tổ chức",
  "Tìm studio ảnh cưới",
  "Hoàn thiện danh sách khách",
];

export function PlannerPreview({ compact = false }: { compact?: boolean }) {
  return (
    <div className={`planner-preview${compact ? " is-compact" : ""}`}>
      <div className="preview-topbar">
        <span className="preview-brand">
          WEDDING <b>MATCH</b>
        </span>
        <span className="preview-caption">KẾ HOẠCH MINH HỌA</span>
        <span className="preview-avatar">A</span>
      </div>
      <div className="preview-workspace">
        {!compact && (
          <aside className="preview-sidebar">
            <span className="preview-sidebar-title">NGÀY CỦA CHÚNG MÌNH</span>
            {[
              { icon: LayoutDashboard, label: "Tổng quan" },
              { icon: CheckCheck, label: "Checklist" },
              { icon: Wallet, label: "Ngân sách" },
              { icon: Mail, label: "Thiệp mời" },
            ].map(({ icon: Icon, label }, i) => (
              <span key={label} className={i === 0 ? "selected" : ""}>
                <Icon size={12} />
                {label}
              </span>
            ))}
            <div className="preview-sidebar-heart">
              <Heart size={25} strokeWidth={1} />
              <small>
                Từng bước nhỏ,
                <br />
                một ngày thật đẹp.
              </small>
            </div>
          </aside>
        )}
        <div className="preview-main">
          <div className="preview-greeting">
            <div>
              <small>KHÔNG GIAN CỦA HAI BẠN</small>
              <h3>Một ngày thật đẹp, bắt đầu từ đây.</h3>
            </div>
            <Heart size={20} strokeWidth={1} />
          </div>
          <div className="preview-stats">
            <div>
              <CheckCheck size={14} />
              <small>Công việc</small>
              <strong>
                12 <span>/ 20</span>
              </strong>
              <i />
            </div>
            <div>
              <Wallet size={14} />
              <small>Ngân sách dự kiến</small>
              <strong>
                200 <span>triệu</span>
              </strong>
              <span className="preview-stat-note">
                Mọi khoản chi, thật rõ ràng
              </span>
            </div>
            <div>
              <Users size={14} />
              <small>Khách xác nhận</small>
              <strong>
                86 <span>khách</span>
              </strong>
              <span className="preview-stat-note">Cùng đón chờ ngày vui</span>
            </div>
          </div>
          <div className="preview-task-panel">
            <div className="preview-panel-title">
              <strong>Những việc sắp tới</strong>
              <span>CHECKLIST CƯỚI</span>
            </div>
            {tasks.map((task, i) => (
              <div className="preview-task" key={task}>
                <span className={i === 0 ? "checked" : ""}>
                  {i === 0 && <Check size={10} />}
                </span>
                <div>
                  {task}
                  <small>
                    {i === 0 ? "Đã hoàn thành" : "Cùng nhau chuẩn bị"}
                  </small>
                </div>
                <span className="preview-task-dot" />
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

export type PreviewKind = "checklist" | "guests" | "invitation" | "vendors";

export function FeaturePreview({ kind }: { kind: PreviewKind }) {
  if (kind === "checklist")
    return (
      <div className="mini-preview">
        <span className="mini-preview-label">CHECKLIST MINH HỌA</span>
        <h4>Từng việc nhỏ, đều có nơi.</h4>
        {tasks.map((task, i) => (
          <div className="mini-task" key={task}>
            <span className={i < 2 ? "done" : ""}>
              {i < 2 && <Check size={12} />}
            </span>
            <span>{task}</span>
            <small>{i < 2 ? "Hoàn tất" : "Sắp tới"}</small>
          </div>
        ))}
      </div>
    );
  if (kind === "guests")
    return (
      <div className="mini-preview">
        <span className="mini-preview-label">KHÁCH MỜI MINH HỌA</span>
        <div className="guest-preview-summary">
          <strong>
            86 <small>khách xác nhận</small>
          </strong>
          <Users size={26} strokeWidth={1} />
        </div>
        {["Nguyễn Minh Anh", "Trần Hoàng Nam", "Lê Thảo Vy"].map((name) => (
          <div className="mini-guest" key={name}>
            <span>{name.split(" ").at(-1)?.slice(0, 1)}</span>
            <span>{name}</span>
            <small>
              <Check size={10} /> Tham dự
            </small>
          </div>
        ))}
      </div>
    );
  if (kind === "invitation")
    return (
      <div className="mini-invitation">
        <span>TRÂN TRỌNG KÍNH MỜI</span>
        <Heart size={24} strokeWidth={1} />
        <h4>
          Minh Anh <em>&</em> Hoàng Nam
        </h4>
        <p>20 . 12 . 2026</p>
        <small>Cùng viết tiếp câu chuyện của chúng mình.</small>
        <span className="mini-invite-label">MẪU THIỆP MINH HỌA</span>
      </div>
    );
  return (
    <div className="mini-preview">
      <span className="mini-preview-label">KHÁM PHÁ DỊCH VỤ CƯỚI</span>
      <div className="vendor-preview-grid">
        {[
          {
            icon: Heart,
            title: "Studio ảnh cưới",
            description: "Lưu giữ khoảnh khắc",
          },
          {
            icon: MapPin,
            title: "Địa điểm tiệc cưới",
            description: "Nơi bắt đầu hạnh phúc",
          },
        ].map(({ icon: Icon, title, description }) => (
          <div key={title}>
            <div className="vendor-preview-cover">
              <Icon size={30} strokeWidth={1} />
            </div>
            <h4>{title}</h4>
            <p>{description}</p>
            <small>Khám phá dịch vụ ↗</small>
          </div>
        ))}
      </div>
    </div>
  );
}

export function MobilePreview() {
  return (
    <div className="phone-preview">
      <div className="phone-camera" />
      <span className="preview-brand">
        WEDDING <b>MATCH</b>
      </span>
      <small className="phone-caption">MINH HỌA TRÊN ĐIỆN THOẠI</small>
      <h3>
        Ngày vui của
        <br />
        chúng mình.
      </h3>
      <div className="phone-date">
        <Heart size={22} strokeWidth={1} />
        <span>
          20.12.2026<small>Mỗi ngày, gần nhau hơn.</small>
        </span>
      </div>
      <div className="phone-panel">
        <strong>Checklist cưới</strong>
        <span className="phone-progress">
          <i />
        </span>
        <small>12 / 20 công việc hoàn tất</small>
        {tasks.map((task, i) => (
          <div className="phone-task" key={task}>
            {i === 0 ? <CheckCheck size={14} /> : <span />}
            <span>{task}</span>
          </div>
        ))}
      </div>
      <div className="phone-nav">
        <LayoutDashboard size={17} />
        <CheckCheck size={17} />
        <Mail size={17} />
        <Users size={17} />
      </div>
    </div>
  );
}
