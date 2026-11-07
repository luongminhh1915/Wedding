import { useEffect, useRef, useState } from "react";
import {
  AlignCenter,
  AlignJustify,
  AlignLeft,
  AlignRight,
  Bold,
  BookOpen,
  Code2,
  Eye,
  FileText,
  Highlighter,
  ImagePlus,
  Italic,
  Link2,
  List,
  ListOrdered,
  Maximize2,
  Minimize2,
  Minus,
  Palette,
  Quote,
  RotateCcw,
  RotateCw,
  Sparkles,
  Strikethrough,
  Table,
  Underline,
  X,
} from "lucide-react";
import { CURATED_ARTICLES, type CuratedArticle } from "./curatedArticles";
import { sanitizeArticleHtml } from "./articleHtml";
import "../../../styles/editorial-newspaper.css";

interface GoogleDocsArticleEditorProps {
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  onApplyCurated?: (article: CuratedArticle) => void;
  actorToken?: string;
}

export function GoogleDocsArticleEditor({
  value,
  onChange,
  disabled = false,
  onApplyCurated,
  actorToken,
}: GoogleDocsArticleEditorProps) {
  const editorRef = useRef<HTMLDivElement>(null);
  const [activeTab, setActiveTab] = useState<"docs" | "preview" | "html">("docs");
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [isCuratedModalOpen, setIsCuratedModalOpen] = useState(false);
  const [isTableModalOpen, setIsTableModalOpen] = useState(false);
  const [isImageModalOpen, setIsImageModalOpen] = useState(false);
  const [isLinkModalOpen, setIsLinkModalOpen] = useState(false);

  // Table modal state
  const [tableRows, setTableRows] = useState(3);
  const [tableCols, setTableCols] = useState(3);
  const [tablePreset, setTablePreset] = useState<"custom" | "quote" | "budget">("custom");

  // Image modal state
  const [imageUrl, setImageUrl] = useState("");
  const [imageCaption, setImageCaption] = useState("");
  const [imageAlt, setImageAlt] = useState("");
  const [isUploadingImage, setIsUploadingImage] = useState(false);

  // Link modal state
  const [linkUrl, setLinkUrl] = useState("");
  const [linkText, setLinkText] = useState("");

  // Stats
  const [stats, setStats] = useState({ words: 0, readTime: 1 });

  // Synchronize internal innerHTML with external value only when different and not focused
  const isInternalChange = useRef(false);

  useEffect(() => {
    if (!editorRef.current) return;
    if (isInternalChange.current && editorRef.current.innerHTML === value) {
      isInternalChange.current = false;
      return;
    }
    isInternalChange.current = false;
    const clean = sanitizeArticleHtml(value || "<p>Bắt đầu viết nội dung bài báo...</p>");
    if (editorRef.current.innerHTML !== clean) {
      editorRef.current.innerHTML = clean;
      updateStats(clean);
    }
  }, [value, activeTab]);

  const updateStats = (html: string) => {
    const text = html.replace(/<[^>]+>/g, " ").replace(/\s+/g, " ").trim();
    const words = text ? text.split(/\s+/).length : 0;
    const readTime = Math.max(1, Math.ceil(words / 220));
    setStats({ words, readTime });
  };

  const handleInput = () => {
    if (!editorRef.current) return;
    const html = editorRef.current.innerHTML;
    isInternalChange.current = true;
    onChange(html);
    updateStats(html);
  };

  // Rich formatting helper via document.execCommand
  const exec = (command: string, val: string | undefined = undefined) => {
    if (disabled || activeTab !== "docs") return;
    editorRef.current?.focus();
    document.execCommand(command, false, val);
    handleInput();
  };

  // Insert custom HTML block at caret
  const insertHtml = (htmlSnippet: string) => {
    if (disabled || activeTab !== "docs") return;
    htmlSnippet = sanitizeArticleHtml(htmlSnippet);
    editorRef.current?.focus();
    const sel = window.getSelection();
    if (!sel || sel.rangeCount === 0) {
      if (editorRef.current) {
        editorRef.current.innerHTML += htmlSnippet;
        handleInput();
      }
      return;
    }
    const range = sel.getRangeAt(0);
    range.deleteContents();
    const el = document.createElement("div");
    el.innerHTML = htmlSnippet;
    const frag = document.createDocumentFragment();
    let node: ChildNode | null;
    let lastNode: ChildNode | null = null;
    while ((node = el.firstChild)) {
      lastNode = frag.appendChild(node);
    }
    range.insertNode(frag);
    if (lastNode) {
      range.setStartAfter(lastNode);
      range.collapse(true);
      sel.removeAllRanges();
      sel.addRange(range);
    }
    handleInput();
  };

  // Format block: Paragraph, Heading 2, Heading 3, Blockquote
  const handleBlockFormat = (format: string) => {
    if (format === "p" || format === "h2" || format === "h3") {
      exec("formatBlock", `<${format}>`);
    } else if (format === "lead") {
      insertHtml(`<p class="editorial-lead">Đoạn tóm tắt mở đầu bài báo (Sapo phong cách báo chí)...</p>`);
    } else if (format === "blockquote") {
      insertHtml(`<blockquote>Trích dẫn hoặc câu nói tâm đắc của chuyên gia cưới...</blockquote>`);
    } else if (format === "callout") {
      insertHtml(
        `<div class="editorial-callout"><div class="callout-title">✨ Bước tiếp theo cùng Wedding Match</div><p>Ghi lại các việc cần làm hoặc lời khuyên dành cho bạn đọc...</p></div>`
      );
    }
  };

  // Handle Insert Table
  const handleInsertTable = () => {
    let tblHtml = "";
    if (tablePreset === "quote") {
      tblHtml = `
<div class="editorial-table-wrapper">
  <table class="editorial-table">
    <thead>
      <tr>
        <th>Hạng mục dịch vụ</th>
        <th>Nhà cung cấp A</th>
        <th>Nhà cung cấp B</th>
        <th>Ghi chú so sánh</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td>Gói dịch vụ chính</td>
        <td>15.000.000 VNĐ</td>
        <td>16.500.000 VNĐ</td>
        <td>Đã gồm nhân sự và thiết bị</td>
      </tr>
      <tr>
        <td>Phát sinh dự kiến</td>
        <td>2.000.000 VNĐ</td>
        <td>Miễn phí di chuyển</td>
        <td>Xác nhận trước 14 ngày</td>
      </tr>
    </tbody>
  </table>
</div><p></p>`;
    } else if (tablePreset === "budget") {
      tblHtml = `
<div class="editorial-table-wrapper">
  <table class="editorial-table">
    <thead>
      <tr>
        <th>Nội dung chi phí</th>
        <th>Dự trù ban đầu</th>
        <th>Thực tế phát sinh</th>
        <th>Trạng thái</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td>Trang trí sảnh & gia tiên</td>
        <td>35.000.000 VNĐ</td>
        <td>36.500.000 VNĐ</td>
        <td>Đã cọc 50%</td>
      </tr>
      <tr>
        <td>Quay phim & chụp ảnh cưới</td>
        <td>22.000.000 VNĐ</td>
        <td>22.000.000 VNĐ</td>
        <td>Chờ nghiệm thu file</td>
      </tr>
    </tbody>
  </table>
</div><p></p>`;
    } else {
      tblHtml = '<div class="editorial-table-wrapper"><table class="editorial-table"><thead><tr>';
      for (let c = 1; c <= tableCols; c++) {
        tblHtml += `<th>Tiêu đề cột ${c}</th>`;
      }
      tblHtml += "</tr></thead><tbody>";
      for (let r = 1; r <= tableRows; r++) {
        tblHtml += "<tr>";
        for (let c = 1; c <= tableCols; c++) {
          tblHtml += `<td>Nội dung ô ${r}-${c}</td>`;
        }
        tblHtml += "</tr>";
      }
      tblHtml += "</tbody></table></div><p></p>";
    }
    insertHtml(tblHtml);
    setIsTableModalOpen(false);
  };

  // Handle Insert Image
  const handleInsertImage = () => {
    if (!imageUrl.trim()) return;
    const figHtml = `
<figure class="editorial-figure">
  <img src="${imageUrl.trim()}" alt="${imageAlt || imageCaption || "Ảnh minh họa bài viết"}" loading="lazy" />
  ${imageCaption.trim() ? `<figcaption>${imageCaption.trim()}</figcaption>` : ""}
</figure><p></p>`;
    insertHtml(figHtml);
    setImageUrl("");
    setImageCaption("");
    setImageAlt("");
    setIsImageModalOpen(false);
  };

  // Image file upload directly into content
  const handleFileUpload = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;
    setIsUploadingImage(true);
    try {
      const formData = new FormData();
      formData.append("file", file);
      const res = await fetch("/api/admin/articles/images", {
        method: "POST",
        headers: actorToken ? { Authorization: `Bearer ${actorToken}` } : {},
        body: formData,
      });
      if (!res.ok) throw new Error("Tải ảnh thất bại");
      const data = await res.json();
      if (data.url) {
        setImageUrl(data.url);
      }
    } catch {
      // Fallback: local blob for immediate preview
      setImageUrl(URL.createObjectURL(file));
    } finally {
      setIsUploadingImage(false);
    }
  };

  // Handle Insert Link
  const handleInsertLink = () => {
    if (!linkUrl.trim()) return;
    const aHtml = `<a href="${linkUrl.trim()}" target="_blank" rel="noopener noreferrer" class="text-rose-600 underline font-semibold">${linkText || linkUrl}</a>`;
    insertHtml(aHtml);
    setLinkUrl("");
    setLinkText("");
    setIsLinkModalOpen(false);
  };

  // Apply a curated article from 24 docx articles
  const handleSelectCurated = (art: CuratedArticle) => {
    if (onApplyCurated) {
      onApplyCurated(art);
    } else {
      onChange(art.content);
    }
    setIsCuratedModalOpen(false);
  };

  return (
    <div
      className={`docs-editor-workspace transition-all ${
        isFullscreen ? "fixed inset-0 z-50 rounded-none h-screen bg-slate-900/40 backdrop-blur-sm p-4 flex flex-col" : ""
      }`}
    >
      {/* Top Header Bar */}
      <div className="bg-slate-50 border-b border-slate-200 px-4 py-2.5 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1.5 text-xs font-semibold text-slate-700 bg-white px-2.5 py-1.5 rounded-lg border border-slate-200 shadow-2xs">
            <FileText size={15} className="text-rose-600" />
            <span>Tài liệu bài báo</span>
          </div>
          <span className="text-xs text-slate-500 font-mono hidden sm:inline-block">
            {stats.words.toLocaleString("vi-VN")} từ · ~{stats.readTime} phút đọc
          </span>
        </div>

        {/* View mode toggle */}
        <div className="flex items-center gap-1 bg-slate-200/80 p-1 rounded-lg">
          <button
            type="button"
            onClick={() => setActiveTab("docs")}
            className={`px-3 py-1 rounded-md text-xs font-semibold flex items-center gap-1.5 transition ${
              activeTab === "docs" ? "bg-white text-rose-700 shadow-2xs" : "text-slate-600 hover:text-slate-900"
            }`}
          >
            <FileText size={13} />
            <span>Google Docs</span>
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("preview")}
            className={`px-3 py-1 rounded-md text-xs font-semibold flex items-center gap-1.5 transition ${
              activeTab === "preview" ? "bg-white text-rose-700 shadow-2xs" : "text-slate-600 hover:text-slate-900"
            }`}
          >
            <Eye size={13} />
            <span>Xem trước Báo chí</span>
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("html")}
            className={`px-3 py-1 rounded-md text-xs font-semibold flex items-center gap-1.5 transition ${
              activeTab === "html" ? "bg-white text-rose-700 shadow-2xs" : "text-slate-600 hover:text-slate-900"
            }`}
          >
            <Code2 size={13} />
            <span>Mã HTML</span>
          </button>
        </div>

        {/* Templates & Fullscreen buttons */}
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => setIsCuratedModalOpen(true)}
            className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-rose-50 hover:bg-rose-100 text-rose-700 border border-rose-200 flex items-center gap-1.5 transition cursor-pointer"
          >
            <BookOpen size={14} />
            <span className="hidden md:inline">Bộ 24 bài cẩm nang (Word)</span>
            <span className="md:hidden">24 bài mẫu</span>
          </button>

          <button
            type="button"
            onClick={() => setIsFullscreen(!isFullscreen)}
            className="p-1.5 rounded-lg text-slate-500 hover:text-slate-800 hover:bg-slate-200 transition"
            title={isFullscreen ? "Thu nhỏ" : "Toàn màn hình"}
          >
            {isFullscreen ? <Minimize2 size={16} /> : <Maximize2 size={16} />}
          </button>
        </div>
      </div>

      {/* Google Docs Sticky Toolbar */}
      {activeTab === "docs" && (
        <div className="docs-toolbar-container">
          {/* Undo / Redo */}
          <button type="button" onClick={() => exec("undo")} className="docs-tool-btn" title="Hoàn tác (Ctrl+Z)">
            <RotateCcw size={15} />
          </button>
          <button type="button" onClick={() => exec("redo")} className="docs-tool-btn" title="Làm lại (Ctrl+Y)">
            <RotateCw size={15} />
          </button>

          <div className="docs-tool-sep" />

          {/* Block Style */}
          <select
            className="docs-tool-select"
            onChange={(e) => {
              handleBlockFormat(e.target.value);
              e.target.value = "";
            }}
            defaultValue=""
          >
            <option value="" disabled>
              Kiểu văn bản...
            </option>
            <option value="p">Văn bản thường</option>
            <option value="h2">Tiêu đề mục (H2)</option>
            <option value="h3">Tiêu đề nhỏ (H3)</option>
            <option value="lead">Đoạn mở đầu (Sapo báo chí)</option>
            <option value="blockquote">Trích dẫn (Pull Quote)</option>
            <option value="callout">Khung lưu ý (Callout)</option>
          </select>

          {/* Font size */}
          <select
            className="docs-tool-select"
            onChange={(e) => {
              exec("fontSize", e.target.value);
              e.target.value = "";
            }}
            defaultValue=""
            title="Cỡ chữ"
          >
            <option value="" disabled>
              Cỡ chữ...
            </option>
            <option value="2">Nhỏ (13px)</option>
            <option value="3">Chuẩn (16px)</option>
            <option value="4">Lớn (18px)</option>
            <option value="5">Tiêu đề (24px)</option>
          </select>

          <div className="docs-tool-sep" />

          {/* Basic Inlines */}
          <button type="button" onClick={() => exec("bold")} className="docs-tool-btn font-bold" title="In đậm (Ctrl+B)">
            <Bold size={15} />
          </button>
          <button type="button" onClick={() => exec("italic")} className="docs-tool-btn italic" title="In nghiêng (Ctrl+I)">
            <Italic size={15} />
          </button>
          <button type="button" onClick={() => exec("underline")} className="docs-tool-btn underline" title="Gạch chân (Ctrl+U)">
            <Underline size={15} />
          </button>
          <button type="button" onClick={() => exec("strikeThrough")} className="docs-tool-btn" title="Gạch ngang">
            <Strikethrough size={15} />
          </button>

          {/* Highlights */}
          <button
            type="button"
            onClick={() => exec("hiliteColor", "#fef08a")}
            className="docs-tool-btn text-amber-600"
            title="Đánh dấu màu vàng"
          >
            <Highlighter size={15} />
          </button>
          <button
            type="button"
            onClick={() => exec("hiliteColor", "#ffe4e6")}
            className="docs-tool-btn text-rose-600"
            title="Đánh dấu màu hồng"
          >
            <Palette size={15} />
          </button>

          <div className="docs-tool-sep" />

          {/* Alignment */}
          <button type="button" onClick={() => exec("justifyLeft")} className="docs-tool-btn" title="Căn trái">
            <AlignLeft size={15} />
          </button>
          <button type="button" onClick={() => exec("justifyCenter")} className="docs-tool-btn" title="Căn giữa">
            <AlignCenter size={15} />
          </button>
          <button type="button" onClick={() => exec("justifyRight")} className="docs-tool-btn" title="Căn phải">
            <AlignRight size={15} />
          </button>
          <button type="button" onClick={() => exec("justifyFull")} className="docs-tool-btn" title="Căn đều">
            <AlignJustify size={15} />
          </button>

          <div className="docs-tool-sep" />

          {/* Lists */}
          <button type="button" onClick={() => exec("insertUnorderedList")} className="docs-tool-btn" title="Danh sách chấm tròn">
            <List size={15} />
          </button>
          <button type="button" onClick={() => exec("insertOrderedList")} className="docs-tool-btn" title="Danh sách số thứ tự">
            <ListOrdered size={15} />
          </button>

          <div className="docs-tool-sep" />

          {/* Special Elements */}
          <button
            type="button"
            onClick={() => setIsTableModalOpen(true)}
            className="docs-tool-btn text-slate-800 font-semibold"
            title="Chèn bảng biểu"
          >
            <Table size={15} className="text-emerald-700" />
            <span className="text-xs">Bảng</span>
          </button>

          <button
            type="button"
            onClick={() => setIsImageModalOpen(true)}
            className="docs-tool-btn text-slate-800 font-semibold"
            title="Chèn ảnh minh họa kèm chú thích"
          >
            <ImagePlus size={15} className="text-blue-700" />
            <span className="text-xs">Ảnh</span>
          </button>

          <button
            type="button"
            onClick={() => handleBlockFormat("blockquote")}
            className="docs-tool-btn text-slate-800 font-semibold"
            title="Chèn trích dẫn"
          >
            <Quote size={15} className="text-rose-700" />
            <span className="text-xs">Trích dẫn</span>
          </button>

          <button
            type="button"
            onClick={() => handleBlockFormat("callout")}
            className="docs-tool-btn text-slate-800 font-semibold"
            title="Chèn hộp lưu ý / Lời khuyên"
          >
            <Sparkles size={15} className="text-amber-700" />
            <span className="text-xs">Lưu ý</span>
          </button>

          <button
            type="button"
            onClick={() => setIsLinkModalOpen(true)}
            className="docs-tool-btn text-slate-800"
            title="Chèn liên kết"
          >
            <Link2 size={15} className="text-indigo-700" />
          </button>

          <button
            type="button"
            onClick={() => exec("insertHorizontalRule")}
            className="docs-tool-btn text-slate-800"
            title="Đường kẻ ngang phân đoạn"
          >
            <Minus size={15} />
          </button>
        </div>
      )}

      {/* Editor Body Canvas */}
      <div className="docs-canvas-scroll">
        {activeTab === "docs" ? (
          <div
            ref={editorRef}
            contentEditable={!disabled}
            onInput={handleInput}
            onPaste={event => {
              const html = event.clipboardData.getData("text/html");
              if (!html) return;
              event.preventDefault();
              insertHtml(html);
            }}
            className="docs-paper-sheet editorial-content"
            style={{ minHeight: "850px" }}
            spellCheck={false}
          />
        ) : activeTab === "preview" ? (
          <div className="docs-paper-sheet editorial-content bg-white">
            <div className="mb-6 pb-4 border-b border-rose-100 flex items-center justify-between">
              <span className="text-xs font-semibold text-rose-700 uppercase tracking-widest bg-rose-50 px-3 py-1 rounded-full border border-rose-200">
                Xem trước giao diện bài báo
              </span>
              <span className="text-xs text-slate-500 font-mono">
                {stats.words} từ · ~{stats.readTime} phút đọc
              </span>
            </div>
            <div dangerouslySetInnerHTML={{ __html: sanitizeArticleHtml(value || "<p>Chưa có nội dung bài viết.</p>") }} />
          </div>
        ) : (
          <div className="w-full max-w-4xl bg-slate-900 text-slate-100 p-6 rounded-xl font-mono text-xs overflow-auto">
            <textarea
              className="w-full h-[700px] bg-transparent text-emerald-300 font-mono text-xs resize-none outline-none leading-relaxed"
              value={value}
              onChange={(e) => {
                onChange(e.target.value);
                updateStats(e.target.value);
              }}
            />
          </div>
        )}
      </div>

      {/* Modal: Curated 24 Articles from Word */}
      {isCuratedModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/50 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-3xl w-full max-h-[85vh] flex flex-col shadow-2xl border border-slate-200 overflow-hidden">
            <div className="p-4 border-b border-slate-200 flex items-center justify-between bg-rose-50/60">
              <div className="flex items-center gap-2">
                <BookOpen className="text-rose-600" size={20} />
                <h3 className="font-bold text-slate-900 text-base">Bộ 24 bài cẩm nang chuẩn (Biên tập Wedding Match)</h3>
              </div>
              <button
                type="button"
                onClick={() => setIsCuratedModalOpen(false)}
                className="text-slate-400 hover:text-slate-700 p-1 rounded-lg"
              >
                <X size={18} />
              </button>
            </div>
            <p className="px-5 py-2.5 bg-amber-50/70 border-b border-amber-100 text-xs text-amber-800">
              💡 Toàn bộ 24 bài viết hoàn chỉnh với nội dung chi tiết, bảng biểu và ảnh minh họa được trích xuất trực tiếp từ
              file Word. Chọn bài để nạp toàn bộ vào editor ngay lập tức.
            </p>
            <div className="p-4 overflow-y-auto flex-1 divide-y divide-slate-100">
              {CURATED_ARTICLES.map((art) => (
                <div key={art.num} className="py-3 flex items-start justify-between gap-4 hover:bg-slate-50 p-3 rounded-xl transition">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="text-[11px] font-bold px-2 py-0.5 rounded-full bg-rose-100 text-rose-800">
                        Bài {String(art.num).padStart(2, "0")}
                      </span>
                      <span className="text-xs font-semibold text-slate-500">{art.category}</span>
                    </div>
                    <h4 className="font-bold text-sm text-slate-900 mt-1 line-clamp-1">{art.title}</h4>
                    <p className="text-xs text-slate-600 mt-0.5 line-clamp-2">{art.excerpt}</p>
                    <div className="mt-1 text-[11px] text-slate-400 font-mono">Slug: /wiki/{art.slug}</div>
                  </div>
                  <button
                    type="button"
                    onClick={() => handleSelectCurated(art)}
                    className="px-3 py-1.5 rounded-lg bg-rose-600 hover:bg-rose-700 text-white font-semibold text-xs whitespace-nowrap shadow-xs transition"
                  >
                    Nạp vào bài viết
                  </button>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Modal: Insert Table */}
      {isTableModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-xl border border-slate-200">
            <h3 className="font-bold text-slate-900 text-base mb-4 flex items-center gap-2">
              <Table className="text-emerald-600" size={18} />
              <span>Chèn bảng biểu phong cách báo chí</span>
            </h3>

            <div className="space-y-4">
              <div>
                <label className="text-xs font-semibold text-slate-700 block mb-1">Mẫu bảng tạo sẵn</label>
                <div className="grid grid-cols-3 gap-2">
                  <button
                    type="button"
                    onClick={() => setTablePreset("custom")}
                    className={`p-2 rounded-lg border text-xs font-semibold text-center transition ${
                      tablePreset === "custom"
                        ? "border-emerald-600 bg-emerald-50 text-emerald-800"
                        : "border-slate-200 text-slate-600 hover:bg-slate-50"
                    }`}
                  >
                    Tùy chỉnh
                  </button>
                  <button
                    type="button"
                    onClick={() => setTablePreset("quote")}
                    className={`p-2 rounded-lg border text-xs font-semibold text-center transition ${
                      tablePreset === "quote"
                        ? "border-emerald-600 bg-emerald-50 text-emerald-800"
                        : "border-slate-200 text-slate-600 hover:bg-slate-50"
                    }`}
                  >
                    Bảng báo giá
                  </button>
                  <button
                    type="button"
                    onClick={() => setTablePreset("budget")}
                    className={`p-2 rounded-lg border text-xs font-semibold text-center transition ${
                      tablePreset === "budget"
                        ? "border-emerald-600 bg-emerald-50 text-emerald-800"
                        : "border-slate-200 text-slate-600 hover:bg-slate-50"
                    }`}
                  >
                    Bảng ngân sách
                  </button>
                </div>
              </div>

              {tablePreset === "custom" && (
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs font-semibold text-slate-700 block mb-1">Số cột (Headers)</label>
                    <input
                      type="number"
                      min={1}
                      max={8}
                      value={tableCols}
                      onChange={(e) => setTableCols(Math.max(1, parseInt(e.target.value) || 1))}
                      className="w-full px-3 py-1.5 text-xs rounded-lg border border-slate-300"
                    />
                  </div>
                  <div>
                    <label className="text-xs font-semibold text-slate-700 block mb-1">Số hàng dữ liệu</label>
                    <input
                      type="number"
                      min={1}
                      max={20}
                      value={tableRows}
                      onChange={(e) => setTableRows(Math.max(1, parseInt(e.target.value) || 1))}
                      className="w-full px-3 py-1.5 text-xs rounded-lg border border-slate-300"
                    />
                  </div>
                </div>
              )}
            </div>

            <div className="mt-6 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setIsTableModalOpen(false)}
                className="px-4 py-2 rounded-lg border border-slate-300 text-xs font-semibold text-slate-700 hover:bg-slate-100"
              >
                Hủy
              </button>
              <button
                type="button"
                onClick={handleInsertTable}
                className="px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold shadow-xs"
              >
                Chèn bảng ngay
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Insert Image */}
      {isImageModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-xl border border-slate-200">
            <h3 className="font-bold text-slate-900 text-base mb-4 flex items-center gap-2">
              <ImagePlus className="text-blue-600" size={18} />
              <span>Chèn ảnh minh họa bài báo</span>
            </h3>

            <div className="space-y-4">
              <div>
                <label className="text-xs font-semibold text-slate-700 block mb-1">Tải ảnh từ máy tính</label>
                <input
                  type="file"
                  accept="image/png,image/jpeg,image/webp"
                  onChange={handleFileUpload}
                  className="w-full text-xs text-slate-500 file:mr-3 file:py-1.5 file:px-3 file:rounded-lg file:border-0 file:text-xs file:font-semibold file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100"
                />
                {isUploadingImage && <span className="text-[11px] text-blue-600 mt-1 block">Đang tải ảnh lên máy chủ...</span>}
              </div>

              <div>
                <label className="text-xs font-semibold text-slate-700 block mb-1">Hoặc dán URL ảnh</label>
                <input
                  type="text"
                  placeholder="https://... hoặc /blog/image1.png"
                  value={imageUrl}
                  onChange={(e) => setImageUrl(e.target.value)}
                  className="w-full px-3 py-1.5 text-xs rounded-lg border border-slate-300"
                />
              </div>

              <div>
                <label className="text-xs font-semibold text-slate-700 block mb-1">
                  Chú thích ảnh (Figcaption - Hiển thị in nghiêng dưới ảnh)
                </label>
                <input
                  type="text"
                  placeholder="VD: Hình minh họa — Checklist cưới cùng Wedding Match"
                  value={imageCaption}
                  onChange={(e) => setImageCaption(e.target.value)}
                  className="w-full px-3 py-1.5 text-xs rounded-lg border border-slate-300"
                />
              </div>

              {imageUrl && (
                <div className="p-2 border rounded-xl bg-slate-50">
                  <span className="text-[10px] text-slate-500 block mb-1">Xem trước ảnh:</span>
                  <img src={imageUrl} alt="preview" className="h-28 w-full object-cover rounded-lg" />
                </div>
              )}
            </div>

            <div className="mt-6 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setIsImageModalOpen(false)}
                className="px-4 py-2 rounded-lg border border-slate-300 text-xs font-semibold text-slate-700 hover:bg-slate-100"
              >
                Hủy
              </button>
              <button
                type="button"
                onClick={handleInsertImage}
                disabled={!imageUrl.trim()}
                className="px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-700 text-white text-xs font-semibold shadow-xs disabled:opacity-50"
              >
                Chèn ảnh vào bài
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Insert Link */}
      {isLinkModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-sm w-full p-6 shadow-xl border border-slate-200">
            <h3 className="font-bold text-slate-900 text-base mb-4 flex items-center gap-2">
              <Link2 className="text-indigo-600" size={18} />
              <span>Chèn liên kết vào văn bản</span>
            </h3>

            <div className="space-y-3">
              <div>
                <label className="text-xs font-semibold text-slate-700 block mb-1">Địa chỉ URL</label>
                <input
                  type="url"
                  placeholder="https://... hoặc /wiki/..."
                  value={linkUrl}
                  onChange={(e) => setLinkUrl(e.target.value)}
                  className="w-full px-3 py-1.5 text-xs rounded-lg border border-slate-300"
                />
              </div>
              <div>
                <label className="text-xs font-semibold text-slate-700 block mb-1">Văn bản hiển thị</label>
                <input
                  type="text"
                  placeholder="Để trống nếu lấy theo URL"
                  value={linkText}
                  onChange={(e) => setLinkText(e.target.value)}
                  className="w-full px-3 py-1.5 text-xs rounded-lg border border-slate-300"
                />
              </div>
            </div>

            <div className="mt-6 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setIsLinkModalOpen(false)}
                className="px-4 py-2 rounded-lg border border-slate-300 text-xs font-semibold text-slate-700 hover:bg-slate-100"
              >
                Hủy
              </button>
              <button
                type="button"
                onClick={handleInsertLink}
                disabled={!linkUrl.trim()}
                className="px-4 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold shadow-xs disabled:opacity-50"
              >
                Chèn liên kết
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
