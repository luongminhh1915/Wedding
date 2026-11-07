export interface ModerationActor { id: string; role: string; token: string; revision: number }
interface Session { user?: { id: string; role: string } | null; token?: string | null; sessionRevision?: number; validated?: boolean }
export function moderationActor(session: Session): ModerationActor | null {
  return session.validated && session.token && session.user && ["Moderator", "SuperAdmin"].includes(session.user.role)
    ? { id: session.user.id, role: session.user.role, token: session.token, revision: session.sessionRevision ?? 0 } : null;
}
export function sameModerationActor(actor: ModerationActor | null, session: Session): boolean {
  const current = moderationActor(session);
  return !!actor && !!current && actor.id === current.id && actor.role === current.role && actor.token === current.token && actor.revision === current.revision;
}
export interface BulkModerationResult { approvedListings: number; approvedServices: number; approvedCombos: number; totalApproved: number; attempted: number; skipped: number; hasMore: boolean }
export interface BulkModerationProposal { mode: "once" | "automatic"; type: "all" | "listing"; actor: ModerationActor }
export const BULK_MODERATION_INTERVAL = 3000;
/** One timer, one awaited batch, then another delay. No mutation-status dependency or immediate retry loop. */
export function startModerationPolling({ isCurrent, hasPending, approve, onResult, onError, schedule = setTimeout, cancel = clearTimeout }: {
  isCurrent: () => boolean; hasPending: () => boolean; approve: () => Promise<BulkModerationResult>;
  onResult: (result: BulkModerationResult) => void; onError: (error: unknown) => void;
  schedule?: typeof setTimeout; cancel?: typeof clearTimeout;
}) {
  let active = true;
  let timer: ReturnType<typeof setTimeout>;
  const stop = () => { active = false; cancel(timer); };
  const next = () => { if (active) timer = schedule(() => { void tick(); }, BULK_MODERATION_INTERVAL); };
  const tick = async () => {
    if (!active || !isCurrent()) { stop(); return; }
    if (!hasPending()) { next(); return; }
    try {
      const result = await approve();
      if (!active || !isCurrent()) { stop(); return; }
      onResult(result);
      // Invalid/stale items need deliberate review, not an endless automatic retry.
      if (result.skipped > 0) { stop(); return; }
    } catch (error) {
      if (active && isCurrent()) onError(error);
      stop(); return;
    }
    next();
  };
  next();
  return stop;
}
