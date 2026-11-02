import { Link } from 'react-router-dom';
import { ArrowRight, CheckCheck, CircleHelp } from 'lucide-react';
import type { PlannerOverview } from '../journey/api';
import { JOURNEY_STAGES, journeyNext } from './journeyPolicy';
import './customer-journey.css';

export function CustomerJourney({ active, data, isLoading, isError, retry }: { active: string; data?: PlannerOverview; isLoading: boolean; isError: boolean; retry: () => void }) {
  const next = journeyNext(active, data);
  return <section className="customer-journey" aria-labelledby="customer-journey-title">
    <div className="customer-journey-heading"><div><span className="wm-eyebrow">HÀNH TRÌNH NGÀY CƯỚI</span><h2 id="customer-journey-title">Biết mình đang ở đâu, chọn việc tiếp theo.</h2></div>{data?.profile?.onboardingCompletedAt && <span className="journey-setup"><CheckCheck size={15} />Đã hoàn tất thiết lập ban đầu</span>}</div>
    {active === 'overview' && <ol className="journey-stages">{JOURNEY_STAGES.map((stage, index) => <li key={stage.id}><Link to={stage.path} aria-current={active === stage.id ? 'step' : undefined}><span>{String(index + 1).padStart(2, '0')}</span><strong>{stage.label}</strong><small>{stage.hint}</small></Link></li>)}</ol>}
    {isLoading ? <p role="status" className="journey-note">Đang tải tiến độ kế hoạch...</p> : isError ? <p role="alert" className="journey-note">Chưa tải được tiến độ. <button onClick={retry}>Thử lại</button></p> : data && active === 'overview' && <div className="journey-metrics"><span><strong>{data.checklistDone}/{data.checklistTotal}</strong> công việc đã xong</span><span><strong>{data.leadCount}</strong> yêu cầu tư vấn</span><span><strong>{data.contractCount}</strong> hợp đồng</span><small>Tiến độ này lấy từ dữ liệu đã lưu, không phải tỷ lệ hoàn thành toàn bộ đám cưới.</small></div>}
    <div className="journey-bottom"><p><CircleHelp size={16} /><span>Chỉ 10 câu thiết lập là bắt buộc. Công việc, ngân sách, tìm dịch vụ và thiệp có thể làm song song; bạn luôn có thể quay lại.</span></p>{next && <Link to={next.path} className="wm-button small" title={next.note}>{next.label}<ArrowRight size={15} /></Link>}</div>
  </section>;
}
