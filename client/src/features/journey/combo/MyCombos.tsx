import { Link } from "react-router-dom";
import { useMyCombos } from "../api";
import { Empty, Panel } from "../ui";
import { ComboStatusList } from "./ComboBuilderPage";

/** P5 – Theo dõi combo đã gửi (trạng thái từng NCC đồng bộ theo Lead/Báo giá/Hợp đồng). */
export function MyCombos() {
  const { data = [], isLoading } = useMyCombos();
  return (
    <Panel eyebrow="Combo" title="Combo đã gửi" action={<Link to="/combo" className="btn btn-gold">Tạo combo mới</Link>}>
      {isLoading ? <p className="muted text-sm">Đang tải...</p> : data.length === 0 ? <Empty>Chưa có combo nào – thử chọn combo mẫu chỉ với vài chạm.</Empty> : <ComboStatusList orders={data} />}
    </Panel>
  );
}
