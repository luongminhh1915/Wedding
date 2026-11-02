export default function StudyNotice() {
  if (import.meta.env.VITE_STUDY_MODE !== 'true') return null;
  return <aside role="note" style={{ background: '#f4f1e9', color: '#263349', borderBottom: '1px solid #ddd5c4', padding: '8px 16px', textAlign: 'center', fontSize: 12, lineHeight: 1.6 }}>
    Bản thử nghiệm phục vụ môn học. Chỉ cung cấp dữ liệu cần thiết; không tải giấy tờ định danh hoặc chuyển tiền thật.
  </aside>;
}
