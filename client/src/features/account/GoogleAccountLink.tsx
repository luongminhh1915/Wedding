import { useEffect, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link2, ShieldCheck } from 'lucide-react';
import { useAuthStore } from '../../store/authStore';
import { apiError, authApi } from '../journey/api';

type GoogleApi = {
  accounts: {
    id: {
      initialize: (options: { client_id: string; callback: (response: { credential: string }) => void }) => void;
      renderButton: (element: HTMLElement, options: { theme: string; size: string; text: string }) => void;
    };
  };
};

export default function GoogleAccountLink() {
  const token = useAuthStore(state => state.token);
  const role = useAuthStore(state => state.user?.role);
  const sessionRevision = useAuthStore(state => state.sessionRevision);
  const queryClient = useQueryClient();
  const button = useRef<HTMLDivElement>(null);
  const sending = useRef(false);
  const [pending, setPending] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const session = useRef({ token, sessionRevision });
  const providers = useQuery({ queryKey: ['auth-providers'], queryFn: authApi.providers, staleTime: 60000 });
  const linkStatus = useQuery({
    queryKey: ['google-link-status', useAuthStore.getState().user?.id],
    enabled: !!token && role === 'Customer',
    queryFn: () => authApi.googleLinkStatus(token!),
    staleTime: 30000,
  });

  useEffect(() => {
    const clientId = providers.data?.googleClientId;
    if (!clientId || !button.current || linkStatus.data?.linked) return;
    let cancelled = false;
    const render = () => {
      const google = (window as Window & { google?: GoogleApi }).google;
      if (cancelled || !google || !button.current) return;
      google.accounts.id.initialize({
        client_id: clientId,
        callback: ({ credential }) => {
          if (sending.current || !credential) return;
          const current = useAuthStore.getState();
          if (current.token !== session.current.token || current.sessionRevision !== session.current.sessionRevision) {
            setError('Phiên đăng nhập đã thay đổi. Vui lòng tải lại trang rồi thử liên kết lại.');
            return;
          }
          sending.current = true;
          setPending(true);
          setError('');
          setMessage('');
          authApi.linkGoogle(credential, session.current.token ?? '').then(async () => {
            const latest = useAuthStore.getState();
            if (latest.token !== session.current.token || latest.sessionRevision !== session.current.sessionRevision) return;
            setMessage('Đã liên kết Google với tài khoản này.');
            await queryClient.invalidateQueries({ queryKey: ['google-link-status'] });
          }).catch(failure => {
            const latest = useAuthStore.getState();
            if (latest.token === session.current.token && latest.sessionRevision === session.current.sessionRevision) setError(apiError(failure, 'Chưa thể liên kết Google. Vui lòng thử lại.'));
          }).finally(() => {
            sending.current = false;
            setPending(false);
          });
        },
      });
      google.accounts.id.renderButton(button.current, { theme: 'outline', size: 'large', text: 'continue_with' });
    };
    let script = document.querySelector<HTMLScriptElement>('script[data-google-identity]');
    if (!script) {
      script = document.createElement('script');
      script.src = 'https://accounts.google.com/gsi/client';
      script.async = true;
      script.dataset.googleIdentity = 'true';
      document.head.appendChild(script);
    }
    script.addEventListener('load', render);
    render();
    return () => { cancelled = true; script?.removeEventListener('load', render); };
  }, [providers.data?.googleClientId, linkStatus.data?.linked, queryClient]);

  if (role !== 'Customer') return null;

  return <section className="account-google-link" aria-labelledby="google-link-title">
    <div className="account-google-link__heading"><ShieldCheck size={22} /><div><h2 id="google-link-title">Đăng nhập bằng Google</h2><p>Liên kết Google đã xác minh với tài khoản khách hiện tại. Việc này không tạo tài khoản mới hoặc đổi email đăng nhập.</p></div></div>
    {providers.isLoading || linkStatus.isLoading ? <p role="status">Đang kiểm tra trạng thái liên kết...</p> : null}
    {(providers.isError || linkStatus.isError) && <div className="account-google-link__notice" role="alert"><p>Chưa kiểm tra được trạng thái liên kết. Hãy thử tải lại trang.</p><button type="button" onClick={() => { void providers.refetch(); void linkStatus.refetch(); }}>Thử lại</button></div>}
    {!providers.isLoading && !providers.isError && !providers.data?.googleClientId && <p className="account-google-link__notice">Đăng nhập Google chưa được cấu hình cho môi trường này.</p>}
    {!linkStatus.isLoading && !linkStatus.isError && linkStatus.data?.linked && <p className="account-google-link__success" role="status"><Link2 size={16} /> Tài khoản này đã liên kết với Google. Bạn có thể dùng Google ở trang đăng nhập.</p>}
    {!linkStatus.isLoading && !linkStatus.isError && !linkStatus.data?.linked && !!providers.data?.googleClientId && <><div ref={button} aria-label="Liên kết tài khoản với Google" />{pending && <p role="status">Đang xác minh và liên kết...</p>}</>}
    {message && !linkStatus.data?.linked && <p className="account-google-link__success" role="status">{message}</p>}
    {error && <p className="account-google-link__error" role="alert">{error}</p>}
    <p className="account-google-link__footnote">Chỉ tài khoản Customer đã đăng nhập mới dùng được. Email Google phải khớp email tài khoản và chưa được liên kết với tài khoản khác.</p>
  </section>;
}
