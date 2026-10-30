import { useRef, useState } from "react";
import { Link2, ShieldCheck } from "lucide-react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "../../store/authStore";
import { apiError, authApi } from "../journey/api";
import { loadFacebookSdk } from "../auth/facebookSdk";

export default function FacebookAccountLink() {
  const token = useAuthStore(state => state.token);
  const role = useAuthStore(state => state.user?.role);
  const sessionRevision = useAuthStore(state => state.sessionRevision);
  const userId = useAuthStore(state => state.user?.id);
  const queryClient = useQueryClient();
  const sending = useRef(false);
  const session = useRef({ token, sessionRevision });
  const [pending, setPending] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const providers = useQuery({ queryKey: ["auth-providers"], queryFn: authApi.providers, staleTime: 60000 });
  const linkStatus = useQuery({
    queryKey: ["facebook-link-status", userId],
    enabled: !!token && role === "Customer",
    queryFn: () => authApi.facebookLinkStatus(token!),
    staleTime: 30000,
  });
  const appId = providers.data?.facebookAppId;
  const version = providers.data?.facebookGraphApiVersion;

  if (role !== "Customer") return null;

  const linkFacebook = async () => {
    const sessionToken = session.current.token;
    if (sending.current || !token || !sessionToken || !appId || !version) return;
    const current = useAuthStore.getState();
    if (current.token !== session.current.token || current.sessionRevision !== session.current.sessionRevision) {
      setError("Phiên đăng nhập đã thay đổi. Hãy tải lại trang rồi thử lại.");
      return;
    }
    sending.current = true;
    setPending(true);
    setError("");
    setMessage("");
    try {
      const facebook = await loadFacebookSdk(appId, version);
      const response = await new Promise<{ authResponse?: { accessToken: string } | null }>(resolve =>
        facebook.login(resolve, { scope: "public_profile" }));
      const accessToken = response.authResponse?.accessToken;
      if (!accessToken) return;
      await authApi.linkFacebook(accessToken, sessionToken);
      const latest = useAuthStore.getState();
      if (latest.token !== session.current.token || latest.sessionRevision !== session.current.sessionRevision) return;
      setMessage("Đã liên kết Facebook với tài khoản này.");
      await queryClient.invalidateQueries({ queryKey: ["facebook-link-status"] });
    } catch (failure) {
      const latest = useAuthStore.getState();
      if (latest.token === session.current.token && latest.sessionRevision === session.current.sessionRevision) {
        setError(apiError(failure, failure instanceof Error ? failure.message : "Chưa thể liên kết Facebook. Vui lòng thử lại."));
      }
    } finally {
      sending.current = false;
      setPending(false);
    }
  };

  return <section className="account-google-link" aria-labelledby="facebook-link-title">
    <div className="account-google-link__heading"><ShieldCheck size={22} /><div>
      <h2 id="facebook-link-title">Đăng nhập bằng Facebook</h2>
      <p>Liên kết Facebook với tài khoản khách hiện tại. Việc này không tạo tài khoản mới hoặc đổi email đăng nhập.</p>
    </div></div>
    {providers.isLoading || linkStatus.isLoading ? <p role="status">Đang kiểm tra trạng thái liên kết...</p> : null}
    {(providers.isError || linkStatus.isError) && <div className="account-google-link__notice" role="alert">
      <p>Chưa kiểm tra được trạng thái liên kết. Hãy thử tải lại trang.</p>
      <button type="button" onClick={() => { void providers.refetch(); void linkStatus.refetch(); }}>Thử lại</button>
    </div>}
    {!providers.isLoading && !providers.isError && !providers.data?.facebookAppId &&
      <p className="account-google-link__notice">Facebook chưa được cấu hình cho môi trường này.</p>}
    {!linkStatus.isLoading && !linkStatus.isError && linkStatus.data?.linked &&
      <p className="account-google-link__success" role="status"><Link2 size={16} /> Tài khoản này đã liên kết với Facebook.</p>}
    {!linkStatus.isLoading && !linkStatus.isError && !linkStatus.data?.linked && !!appId && !!version &&
      <button type="button" className="btn btn-outline mt-4" disabled={pending} onClick={() => void linkFacebook()}>
        <span aria-hidden="true" className="font-bold text-blue-700">f</span> {pending ? "Đang xác minh..." : "Liên kết Facebook"}
      </button>}
    {message && !linkStatus.data?.linked && <p className="account-google-link__success" role="status">{message}</p>}
    {error && <p className="account-google-link__error" role="alert">{error}</p>}
    <p className="account-google-link__footnote">Chỉ khách hàng đã đăng nhập mới thêm được phương thức này. Tài khoản Facebook phải do bạn xác thực và chưa thuộc tài khoản khác; không yêu cầu quyền email.</p>
  </section>;
}
