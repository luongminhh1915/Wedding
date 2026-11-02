import type { Contract } from '../../types/contract.types';
import { contractCategoryPresentation } from './contractCategoryModel';

export function ContractCategorySummary({ contract, showCommissionPolicy = false }: { contract: Contract; showCommissionPolicy?: boolean }) {
  const view = contractCategoryPresentation(contract);
  return (
    <section aria-label="Danh mục và chính sách tại lúc xác nhận" className="rounded-xl border border-slate-200 bg-slate-50 p-3 text-xs space-y-1 break-words">
      <p className="text-slate-500">Danh mục của hợp đồng</p>
      <p className="font-semibold text-slate-800">{view.title}</p>
      {view.group && <p className="text-slate-600">Nhóm: {view.group}</p>}
      <p className="text-slate-500">{view.note}</p>
      {showCommissionPolicy && <>
        {view.rate && <p className="text-slate-700">Hoa hồng: <strong>{view.rate}</strong> · {view.source}</p>}
        {view.policy && <p className="text-slate-500">{view.policy}</p>}
      </>}
    </section>
  );
}
