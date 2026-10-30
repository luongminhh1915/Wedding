import { apiError } from "../api";

interface ReadQuery {
  isPending: boolean;
  isError: boolean;
  isFetching: boolean;
  error: unknown;
  refetch: () => unknown;
}

/** Read failures are not empty results; retry never invokes a business mutation. */
export function AdminQueryReadState({ query, label }: { query: ReadQuery; label: string }) {
  if (query.isPending) return <p role="status" className="muted text-sm py-4">Đang tải {label}...</p>;
  if (!query.isError) return null;
  return <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800">
    <p>Chưa tải được {label}. {apiError(query.error)}</p>
    <button type="button" className="btn btn-outline mt-3" disabled={query.isFetching}
      onClick={() => { void query.refetch(); }}>
      {query.isFetching ? "Đang thử lại..." : "Thử lại"}
    </button>
  </div>;
}
