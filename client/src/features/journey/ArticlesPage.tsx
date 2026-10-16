import { useArticles } from "./api";
import { vnDate } from "./format";
import { Link } from "react-router-dom";

/** P6 – Cẩm nang cưới (CMS bài viết do Moderator đăng). */
export default function ArticlesPage() {
  const { data = [], isLoading } = useArticles();
  return (
    <main className="wd wd-page">
      <div className="container">
        <p className="eyebrow text-[var(--gold)] text-center">Cẩm nang</p>
        <h1 className="serif text-4xl font-bold text-center mb-8">Bí kíp chuẩn bị đám cưới</h1>
        {isLoading ? <p className="muted">Đang tải...</p> : (
          <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-5">
            {data.map((a) => (
              <article key={a.id} className="card overflow-hidden">
                {a.thumbnailUrl && <img src={a.thumbnailUrl} alt="" className="h-[180px] w-full object-cover" loading="lazy" />}
                <div className="p-5">
                  <p className="eyebrow muted">{a.category} · {vnDate(a.publishedAt)}</p>
                  <h2 className="serif text-xl font-bold mt-1"><Link to={`/wiki/${a.slug}`}>{a.title}</Link></h2>
                  <p className="text-sm muted mt-2">{a.excerpt}</p>
                </div>
              </article>
            ))}
          </div>
        )}
      </div>
    </main>
  );
}
