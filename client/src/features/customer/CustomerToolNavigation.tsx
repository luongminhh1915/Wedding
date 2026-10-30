import { Link } from "react-router-dom";
import { CalendarClock, CheckCheck, ChevronDown, Compass, FileCheck2, Heart, LayoutDashboard, PackageCheck, Search, Star, Users, Wallet } from "lucide-react";
import { CUSTOMER_TAB_LABELS, customerNavigationGroups, customerTab } from "./customerNavigationModel";

const icons = { overview: LayoutDashboard, checklist: CheckCheck, budget: Wallet, discovery: Compass, browse: Search, combos: PackageCheck, contracts: FileCheck2, reviews: Star, appointments: CalendarClock, guests: Users, invitations: Heart };

export function CustomerNavigationGroups({ pathname, search, compact = false }: { pathname: string; search: string; compact?: boolean }) {
  return <div className="min-w-0 space-y-4">{customerNavigationGroups(pathname, search).map(group => <section key={group.id} aria-label={group.label} className="min-w-0">
    <h3 className="mb-1.5 px-3 text-[10px] font-semibold uppercase tracking-wider text-[#7b7152]">{group.label}</h3>
    <ul className="m-0 list-none space-y-1 p-0">{group.items.map(item => { const Icon = icons[item.id]; return <li key={item.id}><Link to={item.to} aria-current={item.active ? "page" : undefined} className={`flex min-w-0 items-center gap-2.5 rounded-xl !px-3 ${compact ? "!py-2 !text-xs" : "py-2.5 text-sm"} font-medium focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#a88a35] ${item.active ? "!bg-[#e38c82] !text-[#233a42]" : "text-[#53637b] hover:bg-[#fcf6ea]"}`}><Icon size={16} className="shrink-0" /><span className="min-w-0 break-words">{item.label}</span></Link></li>; })}</ul>
  </section>)}</div>;
}

export function CustomerToolNavigation({ pathname, search }: { pathname: string; search: string }) {
  const active = customerTab(pathname, search);
  return <nav className="min-w-0 self-start rounded-2xl border border-[#dedccf] bg-white p-3 lg:sticky lg:top-[calc(var(--wm-header-offset,88px)+16px)] lg:max-h-[calc(100dvh-var(--wm-header-offset,88px)-32px)] lg:overflow-y-auto" aria-label="Công cụ của cặp đôi">
    <details key={`${pathname}${search}`} className="lg:hidden">
      <summary className="flex cursor-pointer list-none items-center justify-between gap-3 rounded-xl bg-[#fcf6ea] p-3 text-sm font-semibold text-[#233a42] focus-visible:outline-2 focus-visible:outline-[#a88a35]"><span className="min-w-0"><small className="mb-1 block text-[10px] font-normal uppercase tracking-wider text-[#7b7152]">Không gian kế hoạch</small>{CUSTOMER_TAB_LABELS[active]}</span><ChevronDown size={17} className="shrink-0" /></summary>
      <div className="mt-4"><CustomerNavigationGroups pathname={pathname} search={search} /></div>
    </details>
    <div className="hidden lg:block"><p className="mb-4 px-3 pt-2 text-xs font-semibold text-[#233a42]">Không gian của hai bạn</p><CustomerNavigationGroups pathname={pathname} search={search} /></div>
  </nav>;
}
