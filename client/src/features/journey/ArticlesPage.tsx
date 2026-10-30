import { useState } from "react";
import { Search } from "lucide-react";
import { useArticles } from "./api";
import { vnDate } from "./format";
import { articleImageSrc } from "./admin/articleCmsModel";
import { Link } from "react-router-dom";
import "../../styles/marketplace-experience.css";

/** P6 – Cẩm nang cưới (CMS bài viết do Admin / Moderator đăng). */
export default function ArticlesPage() {
  const { data = [], isLoading, isError, refetch } = useArticles();
  const [query, setQuery] = useState("");
  const [activeCategory, setActiveCategory] = useState("Tất cả");
  const categories = ["Tất cả", ...new Set(data.map(article => article.category.trim()).filter(Boolean))];
  const normalizedQuery = query.trim().toLocaleLowerCase("vi");
  const articles = data.filter(article => {
    const matchesCategory = activeCategory === "Tất cả" || article.category === activeCategory;
    const searchText = `${article.title} ${article.excerpt} ${article.category}`.toLocaleLowerCase("vi");
    return matchesCategory && (!normalizedQuery || searchText.includes(normalizedQuery));
  });

  return (
    <main className="wm-editorial-page">
      <div className="wm-editorial-container">
        <header className="wm-editorial-heading">
          <p className="wm-eyebrow">CẨM NANG WEDDING MATCH</p>
          <h1>Bí kíp chuẩn bị <em>ngày vui</em></h1>
          <p>Những câu chuyện, kinh nghiệm và gợi ý hữu ích cho hành trình cưới của hai bạn.</p>
        </header>

        <section className="wm-editorial-tools" aria-label="Tìm bài viết">
          <label className="wm-editorial-search"><Search size={18} aria-hidden="true" /><span className="sr-only">Tìm bài viết</span>
            <input type="search" maxLength={120} value={query} onChange={event => setQuery(event.target.value)} placeholder="Tìm bài viết, kinh nghiệm, xu hướng..." />
          </label>
          <div className="wm-editorial-categories" aria-label="Danh mục bài viết">
            {categories.map(category => <button key={category} type="button" aria-pressed={activeCategory === category} className={activeCategory === category ? "is-active" : ""} onClick={() => setActiveCategory(category)}>{category}</button>)}
          </div>
        </section>

        {isLoading ? <div className="wm-editorial-grid" aria-label="Đang tải bài viết">{Array.from({ length: 6 }, (_, index) => <div className="wm-editorial-skeleton" key={index} />)}</div> : isError ? (
          <div className="wm-editorial-state" role="alert"><h2>Chưa tải được cẩm nang</h2><p>Kiểm tra kết nối rồi thử tải danh sách bài viết một lần nữa.</p><button type="button" className="wm-button small" onClick={() => void refetch()}>Thử lại</button></div>
        ) : articles.length ? (
          <div className="wm-editorial-grid">
            {articles.map(article => (
              <article key={article.id} className="wm-editorial-card">
                <Link to={`/wiki/${article.slug}`} className="wm-editorial-image">
                  {articleImageSrc(article.thumbnailUrl) ? <img src={articleImageSrc(article.thumbnailUrl)} alt={article.title} loading="lazy" /> : <span aria-hidden="true">WEDDING MATCH</span>}
                </Link>
                <div className="wm-editorial-card-body">
                  <p className="wm-editorial-meta">{article.category} <span aria-hidden="true">·</span> {vnDate(article.publishedAt)}</p>
                  <h2><Link to={`/wiki/${article.slug}`}>{article.title}</Link></h2>
                  <p className="wm-editorial-excerpt">{article.excerpt}</p>
                  <Link to={`/wiki/${article.slug}`} className="wm-editorial-read-more">Đọc bài viết <span aria-hidden="true">↗</span></Link>
                </div>
              </article>
            ))}
          </div>
        ) : (
          <div className="wm-editorial-state"><h2>{data.length ? "Chưa có bài viết phù hợp" : "Cẩm nang đang được cập nhật"}</h2><p>{data.length ? "Thử từ khóa khác hoặc chọn một danh mục khác nhé." : "Các bài viết mới sẽ xuất hiện tại đây khi được biên tập và xuất bản."}</p></div>
        )}
      </div>
    </main>
  );
}
