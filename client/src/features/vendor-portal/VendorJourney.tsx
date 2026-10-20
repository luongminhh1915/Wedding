import { Link } from "react-router-dom";
import type { UseQueryResult } from "@tanstack/react-query";
import {
  ArrowLeft, ArrowRight, BadgeCheck, BookOpen, CalendarClock, CheckCheck,
  ChevronRight, CircleHelp, FileCheck2, FileText, LayoutList, MessageSquare,
  Package, RefreshCw, ShieldCheck, Wallet,
} from "lucide-react";
import {
  VENDOR_GUIDE_FAQS, VENDOR_JOURNEY_STAGES, vendorJourneyHref,
  vendorJourneyNext, vendorJourneyStageStatus,
  type VendorJourneySummary,
} from "./vendorJourneyModel";
import "./vendor-journey.css";

type JourneyQuery = UseQueryResult<VendorJourneySummary>;

const stageIcons = {
  verification: BadgeCheck, policy: ShieldCheck, listings: LayoutList,
  combos: Package, appointments: CalendarClock, leads: MessageSquare,
  quotes: FileText, contracts: FileCheck2, changes: RefreshCw, settlement: Wallet,
};

function QueryNotice({ query }: { query: JourneyQuery }) {
  if (query.isPending) return <p className="vj-notice" role="status">Đang tải tình trạng thực tế của shop…</p>;
  if (query.isError) return <div className="vj-notice vj-notice-error" role="alert">
    <span>Chưa tải được tiến độ của shop. Bạn vẫn có thể đọc hướng dẫn và mở các mục bên dưới; dữ liệu đã lưu không bị thay đổi.</span>
    <button type="button" disabled={query.isFetching} onClick={() => void query.refetch()}><RefreshCw size={14} /> Thử lại</button>
  </div>;
  return null;
}

export function VendorJourneyOverview({ query }: { query: JourneyQuery }) {
  const data = query.isError ? undefined : query.data;
  const next = vendorJourneyNext(data);
  const metrics = [
    { label: "Bài đã duyệt · đang bật", value: data?.publicListingCount, tab: "listings", icon: LayoutList },
    { label: "Yêu cầu tư vấn mới", value: data?.newLeadCount, tab: "leads", icon: MessageSquare },
    { label: "Hợp đồng đã tạo", value: data?.contractCount, tab: "contracts-couple", icon: FileCheck2 },
    { label: "Đề nghị chờ bạn phản hồi", value: data?.pendingOrderChangeCount, tab: "change-requests", icon: RefreshCw },
  ];
  return <div className="vendor-journey">
    <section className="vj-hero" aria-labelledby="vendor-journey-title">
      <div className="vj-hero-copy">
        <span className="vj-eyebrow">HÀNH TRÌNH ĐỐI TÁC</span>
        <h2 id="vendor-journey-title">Từ gian hàng đầu tiên,<br />đến một ngày cưới trọn vẹn.</h2>
        <p>Biết shop đang ở đâu, ai cần phản hồi và việc nên làm tiếp theo. Mỗi bước đều có hướng dẫn và lối tắt tới đúng công cụ.</p>
        <div className="vj-actions">
          <Link className="vj-button vj-button-light" to={vendorJourneyHref("guide", "verification")}><BookOpen size={16} /> Đọc hướng dẫn từ đầu</Link>
          {next && <Link className="vj-hero-link" to={next.path}>{next.label}<ArrowRight size={16} /></Link>}
        </div>
      </div>
      <aside className="vj-hero-aside">
        <span className="vj-small-icon"><HeartHandshakeIcon /></span>
        <strong>Shop chuẩn bị.<br />Cặp đôi an tâm.</strong>
        <p>Không phải quy trình bắt buộc 10 bước. Bạn có thể quay lại bất cứ lúc nào; combo và lịch hẹn là tùy chọn.</p>
      </aside>
    </section>
    <QueryNotice query={query} />
    <section className="vj-metrics" aria-label="Tình trạng shop từ dữ liệu đã lưu">
      {metrics.map(({ label, value, tab, icon: Icon }) => <Link key={tab} to={vendorJourneyHref(tab)} className="vj-metric">
        <span><Icon size={18} aria-hidden="true" /><ChevronRight size={15} aria-hidden="true" /></span>
        <strong>{value === undefined ? "—" : value.toLocaleString("vi-VN")}</strong><small>{label}</small>
      </Link>)}
    </section>
    <p className="vj-data-note">Số liệu lấy từ dữ liệu đã lưu của riêng shop. Có hợp đồng hoặc combo đã duyệt không đồng nghĩa đã ký, đã thanh toán hay chắc chắn đang hiển thị công khai.</p>
    {next && <section className="vj-next" aria-labelledby="vendor-next-title">
      <span className="vj-next-icon"><ArrowRight size={22} /></span>
      <div><span className="vj-eyebrow">VIỆC NÊN XEM TRƯỚC</span><h3 id="vendor-next-title">{next.label}</h3><p>{next.note}</p></div>
      <Link className="vj-button" to={next.path}>Mở mục này<ArrowRight size={15} /></Link>
    </section>}
    <section aria-labelledby="vendor-roadmap-title">
      <div className="vj-section-heading"><div><span className="vj-eyebrow">BẢN ĐỒ CÔNG VIỆC</span><h2 id="vendor-roadmap-title">Từng bước, rõ việc cần làm.</h2></div><Link to={vendorJourneyHref("guide")}>Xem hướng dẫn chi tiết<ArrowRight size={15} /></Link></div>
      <ol className="vj-roadmap">
        {VENDOR_JOURNEY_STAGES.map((stage, index) => {
          const Icon = stageIcons[stage.id];
          const status = vendorJourneyStageStatus(stage.id, data);
          return <li key={stage.id}><Link to={vendorJourneyHref("guide", stage.id)} className="vj-stage-card">
            <div className="vj-stage-top"><span className="vj-stage-number">{String(index + 1).padStart(2, "0")}</span><Icon size={21} strokeWidth={1.5} aria-hidden="true" /></div>
            <span className={`vj-status vj-status-${status.tone}`}>{status.label}</span>
            <h3>{stage.shortTitle}</h3><p>{stage.summary}</p>
            <span className="vj-stage-link">Cách thực hiện<ArrowRight size={14} /></span>
          </Link></li>;
        })}
      </ol>
    </section>
    <section className="vj-handoff" aria-labelledby="vendor-handoff-title">
      <div><span className="vj-eyebrow">HIỂU NHANH LUỒNG ĐẶT DỊCH VỤ</span><h2 id="vendor-handoff-title">Mỗi bên biết lúc nào đến lượt mình.</h2></div>
      <ol>
        <li><small>KHÁCH HÀNG</small><strong>Gửi yêu cầu tư vấn</strong></li>
        <li><small>NHÀ CUNG CẤP</small><strong>Tiếp nhận, trao đổi, báo giá</strong></li>
        <li><small>KHÁCH HÀNG</small><strong>Chấp nhận báo giá, xác nhận hợp đồng riêng</strong></li>
        <li><small>HAI BÊN & NỀN TẢNG</small><strong>Thực hiện, đối chiếu và đối soát</strong></li>
      </ol>
      <p><CircleHelp size={16} />Gửi yêu cầu không phải đặt cọc. Chấp nhận báo giá không tự xác nhận hợp đồng hoặc thanh toán.</p>
    </section>
  </div>;
}

function HeartHandshakeIcon() {
  return <CheckCheck size={28} strokeWidth={1.4} aria-hidden="true" />;
}

export function VendorJourneyGuide({ query, requestedStep }: { query: JourneyQuery; requestedStep: string | null }) {
  const selected = VENDOR_JOURNEY_STAGES.find(stage => stage.id === requestedStep) ?? VENDOR_JOURNEY_STAGES[0];
  const index = VENDOR_JOURNEY_STAGES.findIndex(stage => stage.id === selected.id);
  const previous = VENDOR_JOURNEY_STAGES[index - 1];
  const next = VENDOR_JOURNEY_STAGES[index + 1];
  const data = query.isError ? undefined : query.data;
  const status = vendorJourneyStageStatus(selected.id, data);
  const Icon = stageIcons[selected.id];
  return <div className="vendor-journey">
    <header className="vj-guide-heading"><div><span className="vj-eyebrow">CẨM NANG NHÀ CUNG CẤP</span><h2>Chưa quen hệ thống? Bắt đầu từ đây.</h2><p>Chọn một bước để xem cần chuẩn bị gì, thao tác ở đâu và ai sẽ xử lý tiếp theo.</p></div><Link className="vj-button vj-button-outline" to={vendorJourneyHref("overview")}><ArrowLeft size={15} />Về tổng quan</Link></header>
    <QueryNotice query={query} />
    <div className="vj-guide-layout">
      <nav className="vj-guide-nav" aria-label="Các bước hướng dẫn nhà cung cấp">
        {VENDOR_JOURNEY_STAGES.map((stage, i) => <Link key={stage.id} to={vendorJourneyHref("guide", stage.id)} aria-current={stage.id === selected.id ? "step" : undefined}>
          <span>{String(i + 1).padStart(2, "0")}</span><span><strong>{stage.shortTitle}</strong>{"optional" in stage && stage.optional && <small>Tùy chọn</small>}</span><ChevronRight size={14} />
        </Link>)}
      </nav>
      <article className="vj-guide-detail" key={selected.id} aria-labelledby="vendor-guide-step-title">
        <div className="vj-guide-kicker"><span><Icon size={21} /> Hướng dẫn {index + 1}/{VENDOR_JOURNEY_STAGES.length}</span><span className={`vj-status vj-status-${status.tone}`}>{status.label}</span></div>
        <h2 id="vendor-guide-step-title">{selected.title}</h2><p className="vj-guide-summary">{selected.summary}</p>
        <p className="vj-current-state"><strong>Tình trạng shop:</strong> {status.note}</p>
        <h3>Cách thực hiện</h3>
        <ol className="vj-instructions">{selected.steps.map((step, i) => <li key={step}><span>{i + 1}</span><p>{step}</p></li>)}</ol>
        <div className="vj-outcome"><CheckCheck size={19} /><div><strong>Kết quả cần thấy</strong><p>{selected.outcome}</p><p><b>Ai làm tiếp?</b> {selected.nextActor}</p></div></div>
        <div className="vj-caution"><ShieldCheck size={19} /><p>{selected.caution}</p></div>
        <div className="vj-actions vj-detail-actions"><Link className="vj-button" to={vendorJourneyHref(selected.tab)}>Mở {selected.shortTitle.toLocaleLowerCase("vi-VN")}<ArrowRight size={15} /></Link>{selected.relatedTabs.map(related => <Link key={related.tab} className="vj-text-link" to={vendorJourneyHref(related.tab)}>{related.label}<ChevronRight size={14} /></Link>)}</div>
        <footer className="vj-guide-pagination">
          {previous ? <Link to={vendorJourneyHref("guide", previous.id)}><ArrowLeft size={15} /><span>Hướng dẫn trước<strong>{previous.shortTitle}</strong></span></Link> : <span />}
          {next ? <Link to={vendorJourneyHref("guide", next.id)}><span>Hướng dẫn tiếp theo<strong>{next.shortTitle}</strong></span><ArrowRight size={15} /></Link> : <Link to={vendorJourneyHref("overview")}>Quay về tổng quan<ArrowRight size={15} /></Link>}
        </footer>
      </article>
    </div>
    <section className="vj-faq" aria-labelledby="vendor-faq-title"><span className="vj-eyebrow">GIẢI ĐÁP NHANH</span><h2 id="vendor-faq-title">Những điều dễ nhầm khi mới bắt đầu</h2>{VENDOR_GUIDE_FAQS.map(item => <details key={item.question}><summary>{item.question}<ChevronRight size={16} /></summary><p>{item.answer}</p></details>)}</section>
  </div>;
}

export function VendorJourneyContext({ active }: { active: string }) {
  const index = VENDOR_JOURNEY_STAGES.findIndex(stage => stage.tab === active);
  if (index < 0) return null;
  const stage = VENDOR_JOURNEY_STAGES[index];
  const next = VENDOR_JOURNEY_STAGES.slice(index + 1).find(item => !("optional" in item && item.optional));
  return <aside className="vendor-journey vj-context" aria-label="Hướng dẫn cho mục đang mở">
    <BookOpen size={21} aria-hidden="true" /><div><strong>{stage.shortTitle}{"optional" in stage && stage.optional && " · tùy chọn"}</strong><p>{stage.summary}</p></div>
    <div className="vj-context-links"><Link to={vendorJourneyHref("guide", stage.id)}>Xem cách thực hiện<ChevronRight size={14} /></Link>{next && <Link to={vendorJourneyHref(next.tab)} title="Chỉ mở mục tiếp theo, không gửi hay xác nhận dữ liệu">Mục tiếp theo: {next.shortTitle.toLocaleLowerCase("vi-VN")}<ArrowRight size={14} /></Link>}</div>
  </aside>;
}
