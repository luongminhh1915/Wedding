import { Link } from "react-router-dom";
import { useCancelCombo } from "../api";
import { useSavedCombos } from "../comboApi";
import { Empty, Panel } from "../ui";
import { ComboStatusList } from "./ComboBuilderPage";

/** P5 – Theo dõi combo đã gửi (trạng thái từng NCC đồng bộ theo Lead/Báo giá/Hợp đồng). */
export function MyCombos() {
  const { data = [], isLoading } = useSavedCombos();
  const cancel = useCancelCombo();
  return (
    <Panel eyebrow="Combo" title="Combo đã gửi" action={<Link to="/combo" className="btn btn-gold">Tạo combo mới</Link>}>
      {isLoading ? <p className="muted text-sm">Đang tải...</p> : data.length === 0 ? <Empty>Chưa có combo nào – thử chọn combo mẫu chỉ với vài chạm.</Empty> : <ComboStatusList orders={data} />}
      <div className="grid gap-2 mt-4">{data.filter(o => o.status === "Draft").map(o => <div key={o.id} className="flex gap-2 items-center text-sm"><span>{o.code}</span><Link className="btn btn-outline" to="/combo" state={{ draft: o }}>Tiếp tục bản nháp</Link><button className="underline" disabled={cancel.isPending} onClick={() => cancel.mutate(o.id)}>Hủy bản nháp</button></div>)}</div>
    </Panel>
  );
}
