import fs from 'fs';

let content = fs.readFileSync('scratch/all-users-detail-utf8.json', 'utf8');
if (content.charCodeAt(0) === 0xFEFF) content = content.slice(1);
const users = JSON.parse(content);

const admins = users.filter(u => u.Role === 'SuperAdmin');
const vendors = users.filter(u => u.Role === 'VendorOwner');
const customers = users.filter(u => u.Role === 'Customer');

let md = `# 💍 WEDDING MATCH — TÀI LIỆU DANH SÁCH TÀI KHOẢN HỆ THỐNG

> **Website chính thức**: [https://weddingmatch.website](https://weddingmatch.website)  
> **Tổng số tài khoản**: 258 tài khoản (254 tài khoản đang hoạt động - Active)  
> **Ngày cập nhật**: 06/11/2026  
> **Cấu trúc email khách hàng**: 100% sử dụng tên miền \`@gmail.com\` và \`@fpt.edu.vn\` (đã lọc sạch họ Lý & các hòm thư Yahoo/Outlook)  
> **File Word tải về**:
> - [WeddingMatch_DanhSachTaiKhoan_HeThong.docx](file:///d:/Gitgithubhub/Wedding/WeddingMatch_DanhSachTaiKhoan_HeThong.docx)
> - [WeddingMatch_DanhSachTaiKhoan_HeThong.doc](file:///d:/Gitgithubhub/Wedding/WeddingMatch_DanhSachTaiKhoan_HeThong.doc)

---

## 📌 QUY ƯỚC ĐĂNG NHẬP CHUNG

1. **Khách hàng / Cặp đôi (Customer - 195 tài khoản)**:
   - **Tên đăng nhập**: Nhập địa chỉ **Email**
   - **Tên miền email**: 100% là \`@gmail.com\` (128 tài khoản) hoặc \`@fpt.edu.vn\` (67 tài khoản sinh viên/cặp đôi FPT)
   - **Mật khẩu chuẩn**: \`123456\` (đã xác thực và kích hoạt 100% tài khoản hoạt động)
2. **Nhà cung cấp dịch vụ (Vendor - 58 tài khoản)**:
   - **Tên đăng nhập**: Nhập **Tên đăng nhập (LoginName)** hoặc **Email**
   - **Mật khẩu chuẩn**: \`1234567890\` (đối với tài khoản tạo theo lô LoginName) hoặc \`123456\` (đối với tài khoản email riêng)
3. **Quản trị viên hệ thống (SuperAdmin - 5 tài khoản)**:
   - **Tên đăng nhập**: Nhập **Email quản trị**

---

## PHẦN 1. TÀI KHOẢN QUẢN TRỊ VIÊN (SUPERADMIN - 5 TÀI KHOẢN)

| STT | Họ và Tên Quản trị | Email Đăng Nhập | Vai trò | Trạng thái |
| :---: | :--- | :--- | :---: | :---: |
`;

admins.forEach((a, i) => {
  const status = a.IsActive ? '🟢 Hoạt động' : '🔴 Tạm dừng';
  md += `| ${i + 1} | ${a.FullName} | \`${a.Email}\` | SuperAdmin | ${status} |\n`;
});

md += `\n---\n\n## PHẦN 2. TÀI KHOẢN NHÀ CUNG CẤP DỊCH VỤ (VENDOR - 58 NCC)\n\n`;
md += `| STT | Tên Thương Hiệu / Nhà Cung Cấp | Tên Đăng Nhập (LoginName / Email) | Mật khẩu | Trạng thái |\n`;
md += `| :---: | :--- | :--- | :---: | :---: |\n`;

vendors.forEach((v, i) => {
  const brand = v.BrandName || v.FullName;
  const loginId = v.LoginName ? `\`${v.LoginName}\`` : `\`${v.Email}\``;
  const pwd = v.LoginName ? '`1234567890`' : '`123456`';
  const status = v.IsActive ? '🟢 Hoạt động' : '🔴 Tạm dừng';
  md += `| ${i + 1} | ${brand} | ${loginId} | ${pwd} | ${status} |\n`;
});

md += `\n---\n\n## PHẦN 3. TÀI KHOẢN KHÁCH HÀNG / CẶP ĐÔI (CUSTOMER - 195 TÀI KHOẢN)\n\n`;
md += `| STT | Họ và Tên Cặp Đôi | Email Đăng Nhập | Mật khẩu | Số Điện Thoại | Trạng thái |\n`;
md += `| :---: | :--- | :--- | :---: | :---: | :---: |\n`;

customers.forEach((c, i) => {
  const status = c.IsActive ? '🟢 Hoạt động' : '⚪ Tạm dừng (1.5%)';
  const phone = c.PhoneNumber ? `\`${c.PhoneNumber}\`` : '—';
  md += `| ${i + 1} | ${c.FullName} | \`${c.Email}\` | \`123456\` | ${phone} | ${status} |\n`;
});

fs.writeFileSync('scratch/full-artifact.md', md, 'utf8');
console.log('Artifact markdown written to scratch/full-artifact.md');
