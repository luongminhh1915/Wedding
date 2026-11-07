import { useEffect, useRef, useState, type ReactNode } from 'react';
import axiosClient from '../../services/axiosClient';
import { useAuthStore } from '../../store/authStore';
import { apiError } from './api';
import { fetchVerificationDocument, legacyVerificationHref, managedVerificationPath, verificationActorIsCurrent, type VerificationActor } from './verificationDocumentModel';

/** Private managed files are requested with a captured Bearer header, never put in a token URL. */
export function VerificationDocumentLink({ reference, vendorId, children, className = '' }: {
  reference: string; vendorId: string; children: ReactNode; className?: string;
}) {
  const session = useAuthStore();
  const key = `${session.user?.id}:${session.user?.role}:${session.sessionRevision}`;
  const [state, setState] = useState({ key: '', busy: false, error: '' });
  const request = useRef<AbortController | null>(null);
  const objectUrls = useRef(new Set<string>());
  useEffect(() => () => {
    request.current?.abort();
    for (const url of objectUrls.current) URL.revokeObjectURL(url);
    objectUrls.current.clear();
  }, [key]);
  const busy = state.key === key && state.busy;
  const path = managedVerificationPath(reference, vendorId);
  const external = !path ? legacyVerificationHref(reference) : null;
  if (external) return <a href={external} target="_blank" rel="noopener noreferrer" referrerPolicy="no-referrer" className={className} title="Liên kết HTTPS bên ngoài do nhà cung cấp cung cấp">{children} ↗</a>;
  if (!path) return <span className="text-xs text-amber-800">Tài liệu cũ cần tải lại qua hồ sơ riêng.</span>;

  const download = async () => {
    if (request.current || !session.user || !session.token) return;
    const actor: VerificationActor = { id: session.user.id, role: session.user.role, token: session.token, revision: session.sessionRevision };
    const current = () => verificationActorIsCurrent(actor, useAuthStore.getState());
    const controller = new AbortController(); request.current = controller;
    setState({ key, busy: true, error: '' });
    try {
      const blob = await fetchVerificationDocument(path, vendorId, { signal: controller.signal, isCurrent: current,
        send: async (url, signal) => (await axiosClient.get<Blob>(url, { responseType: 'blob', signal, headers: { Authorization: `Bearer ${actor.token}` } })).data });
      if (!current() || controller.signal.aborted) return;
      const url = URL.createObjectURL(blob); objectUrls.current.add(url);
      const link = document.createElement('a'); link.href = url; link.download = path.split('/').pop()!; link.rel = 'noopener';
      document.body.appendChild(link); link.click(); link.remove();
      setTimeout(() => { URL.revokeObjectURL(url); objectUrls.current.delete(url); }, 30000);
    } catch (error) {
      if (!controller.signal.aborted && current()) setState({ key, busy: false, error: apiError(error, 'Không tải được tài liệu. Vui lòng thử lại.') });
    } finally {
      if (request.current === controller) request.current = null;
      if (current()) setState(previous => previous.key === key ? { ...previous, busy: false } : previous);
    }
  };
  return <span className="inline-flex flex-col"><button type="button" className={className} disabled={busy || !session.token} onClick={() => void download()}>{busy ? 'Đang tải…' : children}</button>
    {state.key === key && state.error && <span role="alert" className="text-xs text-red-700">{state.error}</span>}</span>;
}
