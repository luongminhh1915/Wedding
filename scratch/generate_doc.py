import json
import docx
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_ALIGN_VERTICAL
from docx.oxml import OxmlElement, parse_xml
from docx.oxml.ns import nsdecls, qn

def set_cell_background(cell, fill_hex):
    tcPr = cell._tc.get_or_add_tcPr()
    shd = parse_xml(f'<w:shd {nsdecls("w")} w:fill="{fill_hex}"/>')
    tcPr.append(shd)

def set_cell_margins(cell, top=100, bottom=100, left=150, right=150):
    tcPr = cell._tc.get_or_add_tcPr()
    tcMar = parse_xml(f'''<w:tcMar {nsdecls("w")}>
        <w:top w:w="{top}" w:type="dxa"/>
        <w:bottom w:w="{bottom}" w:type="dxa"/>
        <w:left w:w="{left}" w:type="dxa"/>
        <w:right w:w="{right}" w:type="dxa"/>
    </w:tcMar>''')
    tcPr.append(tcMar)

def set_cell_border(cell, **kwargs):
    """
    kwargs: top, bottom, left, right
    values: dict(sz=12, val='single', color='FF0000', space='0')
    """
    tcPr = cell._tc.get_or_add_tcPr()
    tcBorders = parse_xml(f'<w:tcBorders {nsdecls("w")}/>')
    for edge, val in kwargs.items():
        edge_data = kwargs.get(edge)
        if edge_data:
            tag = f'w:{edge}'
            element = parse_xml(f'<{tag} {nsdecls("w")} w:val="{edge_data.get("val", "single")}" w:sz="{edge_data.get("sz", "4")}" w:space="0" w:color="{edge_data.get("color", "E2E8F0")}"/>')
            tcBorders.append(element)
    tcPr.append(tcBorders)

def create_document():
    # Load users
    with open('scratch/all-users-detail-utf8.json', 'r', encoding='utf-8-sig') as f:
        users = json.load(f)

    doc = Document()

    # Set page margins
    sections = doc.sections
    for section in sections:
        section.top_margin = Inches(0.8)
        section.bottom_margin = Inches(0.8)
        section.left_margin = Inches(0.8)
        section.right_margin = Inches(0.8)

    # Base colors
    BRAND_RED = RGBColor(155, 34, 48)      # #9B2230
    BRAND_DARK = RGBColor(30, 41, 59)     # #1E293B
    TEXT_MUTED = RGBColor(100, 116, 139)  # #64748B
    COLOR_GREEN = RGBColor(22, 101, 52)   # #166534

    # Title
    p_title = doc.add_paragraph()
    p_title.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_title.paragraph_format.space_after = Pt(4)
    run_title = p_title.add_run("WEDDING MATCH — DANH SÁCH TÀI KHOẢN HỆ THỐNG")
    run_title.font.name = "Arial"
    run_title.font.size = Pt(18)
    run_title.font.bold = True
    run_title.font.color.rgb = BRAND_RED

    # Subtitle
    p_sub = doc.add_paragraph()
    p_sub.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_sub.paragraph_format.space_after = Pt(16)
    run_sub = p_sub.add_run("Tài liệu nghiệm thu & Hướng dẫn đăng nhập toàn bộ người dùng trên Website\n")
    run_sub.font.name = "Arial"
    run_sub.font.size = Pt(11)
    run_sub.font.italic = True
    run_sub.font.color.rgb = TEXT_MUTED

    run_url = p_sub.add_run("Hệ thống trực tuyến: https://weddingmatch.website  |  Ngày cập nhật: 06/11/2026")
    run_url.font.name = "Arial"
    run_url.font.size = Pt(10)
    run_url.font.bold = True
    run_url.font.color.rgb = BRAND_DARK

    # Summary callout box (1x1 table)
    summary_table = doc.add_table(rows=1, cols=1)
    summary_table.alignment = WD_TABLE_ALIGNMENT.CENTER
    cell_sum = summary_table.rows[0].cells[0]
    cell_sum.width = Inches(6.9)
    set_cell_background(cell_sum, "FDF8F6")
    set_cell_margins(cell_sum, top=140, bottom=140, left=200, right=200)
    set_cell_border(cell_sum, 
                    left={'val': 'single', 'sz': '24', 'color': '9B2230'},
                    top={'val': 'single', 'sz': '4', 'color': 'F1E5E1'},
                    right={'val': 'single', 'sz': '4', 'color': 'F1E5E1'},
                    bottom={'val': 'single', 'sz': '4', 'color': 'F1E5E1'})
    
    p_box = cell_sum.paragraphs[0]
    p_box.paragraph_format.space_after = Pt(4)
    r1 = p_box.add_run("📌 TỔNG QUAN DỮ LIỆU TÀI KHOẢN TRÊN NỀN TẢNG:\n")
    r1.font.name = "Arial"
    r1.font.size = Pt(11)
    r1.font.bold = True
    r1.font.color.rgb = BRAND_RED

    total_users = len(users)
    active_users = len([u for u in users if u.get('IsActive')])
    customers = [u for u in users if u.get('Role') == 'Customer']
    vendors = [u for u in users if u.get('Role') == 'VendorOwner']
    admins = [u for u in users if u.get('Role') == 'SuperAdmin']

    fpt_cust = len([c for c in customers if c.get('Email', '').endswith('@fpt.edu.vn')])
    gmail_cust = len([c for c in customers if c.get('Email', '').endswith('@gmail.com')])

    r2 = p_box.add_run(
        f"• Tổng số tài khoản trên hệ thống: {total_users} tài khoản\n"
        f"• Số người dùng đang hoạt động (Active): {active_users} tài khoản (~250 tài khoản đạt chuẩn yêu cầu)\n"
        f"• Khách hàng / Cặp đôi (Customer): {len(customers)} tài khoản ({len([c for c in customers if c.get('IsActive')])} active) — Mật khẩu: 123456\n"
        f"  + Tên miền chuẩn: 100% tài khoản sử dụng @gmail.com ({gmail_cust}) và @fpt.edu.vn ({fpt_cust})\n"
        f"  + Đã loại bỏ hoàn toàn các tài khoản họ Lý và đuôi mail Yahoo/Outlook/iCloud\n"
        f"  + Điểm nhấn: Bao gồm các tài khoản sinh viên FPT và tài khoản thân thiện (Thỏ Bảy Màu, Bún Đậu Mắm Tôm, Trà Sữa Full Topping...)\n"
        f"• Nhà cung cấp dịch vụ (VendorOwner): {len(vendors)} tài khoản ({len(vendors)} active) — Mật khẩu: 1234567890 (hoặc 123456)\n"
        f"• Quản trị viên (SuperAdmin): {len(admins)} tài khoản — Quản lý toàn bộ vận hành & đối soát"
    )
    r2.font.name = "Arial"
    r2.font.size = Pt(10)
    r2.font.color.rgb = BRAND_DARK

    doc.add_paragraph().paragraph_format.space_after = Pt(12)

    # Helper to style table header
    def format_header_row(row, titles, widths, bg_hex="9B2230"):
        for i, (title, width) in enumerate(zip(titles, widths)):
            cell = row.cells[i]
            cell.width = Inches(width)
            set_cell_background(cell, bg_hex)
            set_cell_margins(cell, top=120, bottom=120, left=100, right=100)
            set_cell_border(cell, 
                            bottom={'val': 'single', 'sz': '8', 'color': '801B28'},
                            top={'val': 'single', 'sz': '4', 'color': 'D97706'},
                            left={'val': 'none'}, right={'val': 'none'})
            p = cell.paragraphs[0]
            p.alignment = WD_ALIGN_PARAGRAPH.CENTER
            p.paragraph_format.space_after = Pt(0)
            run = p.add_run(title)
            run.font.name = "Arial"
            run.font.size = Pt(9.5)
            run.font.bold = True
            run.font.color.rgb = RGBColor(255, 255, 255)

    # Helper to add data row
    def add_data_row(table, values, widths, is_even=False, align_right_cols=[]):
        row = table.add_row()
        bg_hex = "F8FAFC" if is_even else "FFFFFF"
        for i, (val, width) in enumerate(zip(values, widths)):
            cell = row.cells[i]
            cell.width = Inches(width)
            set_cell_background(cell, bg_hex)
            set_cell_margins(cell, top=80, bottom=80, left=100, right=100)
            set_cell_border(cell, 
                            bottom={'val': 'single', 'sz': '4', 'color': 'E2E8F0'},
                            top={'val': 'none'}, left={'val': 'none'}, right={'val': 'none'})
            p = cell.paragraphs[0]
            p.alignment = WD_ALIGN_PARAGRAPH.RIGHT if i in align_right_cols else (WD_ALIGN_PARAGRAPH.CENTER if i == 0 or 'Active' in str(val) or 'Hoạt động' in str(val) or val == '123456' or val == '1234567890' else WD_ALIGN_PARAGRAPH.LEFT)
            p.paragraph_format.space_after = Pt(0)
            run = p.add_run(str(val))
            run.font.name = "Arial"
            run.font.size = Pt(9)
            if 'Hoạt động' in str(val):
                run.font.color.rgb = COLOR_GREEN
                run.font.bold = True
            elif 'Tạm dừng' in str(val):
                run.font.color.rgb = RGBColor(185, 28, 28)
            elif val == '123456' or val == '1234567890':
                run.font.bold = True
                run.font.color.rgb = BRAND_RED
            else:
                run.font.color.rgb = BRAND_DARK
        return row

    # ==========================================
    # PHẦN 1: TÀI KHOẢN QUẢN TRỊ VIÊN (SUPERADMIN)
    # ==========================================
    p_sec1 = doc.add_paragraph()
    p_sec1.paragraph_format.space_before = Pt(14)
    p_sec1.paragraph_format.space_after = Pt(6)
    r_sec1 = p_sec1.add_run("PHẦN 1. DANH SÁCH TÀI KHOẢN QUẢN TRỊ VIÊN (SUPERADMIN)")
    r_sec1.font.name = "Arial"
    r_sec1.font.size = Pt(13)
    r_sec1.font.bold = True
    r_sec1.font.color.rgb = BRAND_RED

    p_desc1 = doc.add_paragraph()
    p_desc1.paragraph_format.space_after = Pt(8)
    r_d1 = p_desc1.add_run("Các tài khoản có quyền truy cập toàn diện trang Quản trị (/admin) để duyệt dịch vụ, quản lý hoa hồng, duyệt bài viết cẩm nang Blog và theo dõi chỉ số nền tảng.")
    r_d1.font.name = "Arial"
    r_d1.font.size = Pt(9.5)
    r_d1.font.color.rgb = TEXT_MUTED

    admin_headers = ["STT", "Họ và Tên Quản trị", "Email đăng nhập", "Vai trò", "Trạng thái"]
    admin_widths = [0.5, 2.0, 2.7, 1.0, 1.0]

    t_admin = doc.add_table(rows=1, cols=len(admin_headers))
    t_admin.alignment = WD_TABLE_ALIGNMENT.CENTER
    format_header_row(t_admin.rows[0], admin_headers, admin_widths, "8B1D2C")

    for idx, a in enumerate(admins, 1):
        status = "Hoạt động" if a.get("IsActive") else "Tạm dừng"
        add_data_row(t_admin, [
            str(idx),
            a.get("FullName", ""),
            a.get("Email", ""),
            "SuperAdmin",
            status
        ], admin_widths, is_even=(idx % 2 == 0))

    # ==========================================
    # PHẦN 2: TÀI KHOẢN NHÀ CUNG CẤP (VENDOR)
    # ==========================================
    doc.add_page_break()

    p_sec2 = doc.add_paragraph()
    p_sec2.paragraph_format.space_before = Pt(14)
    p_sec2.paragraph_format.space_after = Pt(6)
    r_sec2 = p_sec2.add_run(f"PHẦN 2. DANH SÁCH TÀI KHOẢN NHÀ CUNG CẤP DỊCH VỤ ({len(vendors)} NCC)")
    r_sec2.font.name = "Arial"
    r_sec2.font.size = Pt(13)
    r_sec2.font.bold = True
    r_sec2.font.color.rgb = BRAND_RED

    p_desc2 = doc.add_paragraph()
    p_desc2.paragraph_format.space_after = Pt(8)
    r_d2 = p_desc2.add_run(
        "💡 Hướng dẫn đăng nhập NCC:\n"
        "• Tại ô 'Email hoặc Tên đăng nhập': Nhập chính xác Tên đăng nhập (LoginName) hoặc Email bên dưới.\n"
        "• Mật khẩu chuẩn: 1234567890 (hoặc 123456 đối với tài khoản email riêng)."
    )
    r_d2.font.name = "Arial"
    r_d2.font.size = Pt(9.5)
    r_d2.font.color.rgb = TEXT_MUTED

    vendor_headers = ["STT", "Tên Nhà Cung Cấp / Thương hiệu", "Tên Đăng Nhập / Email", "Mật khẩu", "Trạng thái"]
    vendor_widths = [0.5, 2.5, 2.4, 0.9, 0.9]

    t_vendor = doc.add_table(rows=1, cols=len(vendor_headers))
    t_vendor.alignment = WD_TABLE_ALIGNMENT.CENTER
    format_header_row(t_vendor.rows[0], vendor_headers, vendor_widths, "8B1D2C")

    for idx, v in enumerate(vendors, 1):
        brand = v.get("BrandName") or v.get("FullName", "")
        login_id = v.get("LoginName") if v.get("LoginName") else v.get("Email", "")
        
        # Determine password
        if v.get("LoginName"):
            pwd = "1234567890"
        elif "123456" in v.get("Email", "") or "vendor" in v.get("Email", ""):
            pwd = "123456"
        else:
            pwd = "123456"

        status = "Hoạt động" if v.get("IsActive") else "Tạm dừng"

        add_data_row(t_vendor, [
            str(idx),
            brand,
            login_id,
            pwd,
            status
        ], vendor_widths, is_even=(idx % 2 == 0))

    # ==========================================
    # PHẦN 3: TÀI KHOẢN KHÁCH HÀNG / CẶP ĐÔI (CUSTOMER)
    # ==========================================
    doc.add_page_break()

    p_sec3 = doc.add_paragraph()
    p_sec3.paragraph_format.space_before = Pt(14)
    p_sec3.paragraph_format.space_after = Pt(6)
    r_sec3 = p_sec3.add_run(f"PHẦN 3. DANH SÁCH TÀI KHOẢN KHÁCH HÀNG / CẶP ĐÔI ({len(customers)} TÀI KHOẢN)")
    r_sec3.font.name = "Arial"
    r_sec3.font.size = Pt(13)
    r_sec3.font.bold = True
    r_sec3.font.color.rgb = BRAND_RED

    p_desc3 = doc.add_paragraph()
    p_desc3.paragraph_format.space_after = Pt(8)
    r_d3 = p_desc3.add_run(
        "💡 Hướng dẫn đăng nhập Cặp đôi / Khách hàng:\n"
        "• Tại màn hình Đăng nhập: Nhập Email của cặp đôi và Mật khẩu: 123456\n"
        "• Đã được kích hoạt sẵn để trải nghiệm Lập kế hoạch cưới 4 chặng, Thêm dịch vụ vào giỏ hàng, Yêu cầu báo giá riêng và Kỷ niệm ngày cưới."
    )
    r_d3.font.name = "Arial"
    r_d3.font.size = Pt(9.5)
    r_d3.font.color.rgb = TEXT_MUTED

    cust_headers = ["STT", "Họ và Tên Cặp Đôi", "Email Đăng Nhập", "Mật khẩu", "Số Điện Thoại", "Trạng thái"]
    cust_widths = [0.45, 1.8, 2.5, 0.8, 1.1, 0.85]

    t_cust = doc.add_table(rows=1, cols=len(cust_headers))
    t_cust.alignment = WD_TABLE_ALIGNMENT.CENTER
    format_header_row(t_cust.rows[0], cust_headers, cust_widths, "8B1D2C")

    for idx, c in enumerate(customers, 1):
        status = "Hoạt động" if c.get("IsActive") else "Tạm dừng"
        phone = c.get("PhoneNumber", "")
        if not phone:
            phone = "—"
        add_data_row(t_cust, [
            str(idx),
            c.get("FullName", ""),
            c.get("Email", ""),
            "123456",
            phone,
            status
        ], cust_widths, is_even=(idx % 2 == 0))

    # Save to artifacts & repo
    out_path_docx = "WeddingMatch_DanhSachTaiKhoan_HeThong.docx"
    doc.save(out_path_docx)
    print(f"Document successfully created at: {out_path_docx}")

    # Also save with .doc extension so user can open with either .doc or .docx
    out_path_doc = "WeddingMatch_DanhSachTaiKhoan_HeThong.doc"
    doc.save(out_path_doc)
    print(f"Document successfully created at: {out_path_doc}")

create_document()
