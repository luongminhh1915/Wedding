# Checklist refactor dịch vụ / combo —20/10/2026

Nguồn: bản yêu cầu20 acceptance criteria và quyết định giá nguyên gói + chat/đề xuất riêng. Bối cảnh trước refactor: `weddy-journey`; khi khôi phục source đã được commit bên ngoài thành `3596ae8`, không reset/revert phần đó.

## Audit / kiến trúc tái sử dụng

- [x] Listing + ListingRevision: giới thiệu, media, structured details, public baseline/private proposal.
- [x] ServiceItem + ItemVariant/ItemAddOn: cấu hình giá, đơn vị, capacity; giữ legacy/API.
- [x] ComboTemplate/Item/Revision: combo public của shop, không phải đơn khách.
- [x] ComboOrder/Item, Lead, Quote, BookingContract: quy trình mua và snapshot riêng, không thay thế bằng catalog sống.
- [x] Moderation queue và Lead-scoped chat tái sử dụng; không mở quyền staff vào đề xuất riêng.

## Acceptance criteria — implementation và automated/API gate

| # | Yêu cầu | Trạng thái / bằng chứng |
|---|---|---|
|1|Một nơi tạo/quản lý dịch vụ|Có: ListingForm tích hợp cấu hình giá; bỏ creator trùng khỏi tab combo.|
|2|Tạo/nháp/gửi duyệt dịch vụ|Có: UnifiedServiceAuthoringTests; một proposal chứa pricing, variant/add-on.|
|3–4|Chỉ Approved+Active đủ điều kiện|Có: picker + backend eligibility/review/checkout, không chỉ filter client.|
|5–7|Đa dịch vụ own-shop, tối thiểu2|Có: FixedComboPackageTests, gồm cùng category, foreign/duplicate/nested/unit/optional deny.|
|8–9|Không copy nguồn, tái dùng nhiều combo|Có: tham chiếu ServiceItem/Listing, test counts và nhiều template.|
|10|Original/Combo/Savings đúng|Có: fixed amount server, số VND chính xác, markup không giả savings/âm allocation.|
|11–13|Lưu/tải/gửi duyệt/public boundary|Có: immutable revisions, draft/pending không public, stale/self-review deny.|
|14|Khách xem đúng thành phần|Có: detail/cart/saved-order ưu tiên frozen component; fixed cart không configurator.|
|15|Nguồn đổi không sửa giao dịch cũ|Có: immutable component/order snapshot, hold/re-review public, frozen original private scope.|
|16|Không thay commission hiện hữu|Có regression: legacy/category snapshots và original financial rows; không gọi là xong engine tài chính mới.|
|17|Giữ variant/add-on|Có: unchanged IDs, omitted payload preserve, canonical legacy routing, standalone legacy API retained.|
|18|Migration không mất dữ liệu|Migration14/15 additive; SQL migration/replay/old-row tests; local preservation phải ghi rõ trong báo cáo.|
|19–20|Build/test|Xem báo cáo và artifact gate cuối; không dùng artifact/hash trước sửa cuối.|

## Bổ sung theo quyết định chat/đơn riêng

- [x] **Customer business follow-up21/10:** read-error/retry/stale-action guards ở Leads/Quotes/Contracts/Combos; private agreement invalidates lifecycle reads và read capabilities đồng bộ command guard, giữ quyền Reject/Request changes. Full BE1073/1073 actualSQL/0skip, FE331/331/lint/build/API; [report](reports/customer-business-experience-2026-10-21.md) có phạm vi/browser/remaining. Không sửa giá, snapshot, hoa hồng, schema hoặc DB ứng dụng. Đây không phải full desktop/mobile E2E acceptance của refactor.

- [x] **Customer discovery21/10:** `/combo` hiển thị dịch vụ lẻ và combo cùng trang mặc định; bounded category preview, photo/price/configurator/cart, giữ singleVendor/FixedPrice và navigation persistence. FE275/build/lint + targeted browserSQL/mobile đạt; report `reports/customer-combo-service-discovery-2026-10-21.md`. Không coi các gate refactor lịch sử bên dưới đã được chạy lại toàn bộ.

- [x] Workspace chỉ đúng Customer/VendorOwner từ DB + ownership, staff/foreign/anonymous deny.
- [x] Vendor gửi/rút; Customer đồng ý/từ chối; version-pinned, serializable, immutable body/history.
- [x] Current price/deposit/payment/commission không sửa; original order/contract/public template không thay.
- [x] Frozen scope từ order snapshot, không lấy mô tả dịch vụ hiện tại; linked lead hết hạn/dừng chặn xác nhận.
- [x] Báo giá/hợp đồng cũ trước accepted combo change bị chặn; phát hành lại rồi khách review. Không tự gán toàn combo vào từng hợp đồng.
- [x] Combo cùng category không chiếm chung một wizard budget seed; cancel khôi phục seed đúng.
- [x] Chat dùng session/axios/config chung; SignalR rejoin, REST fallback và lỗi hiển thị thật.
- [ ] Browser E2E desktop/mobile của bản refactor: công cụ không có browser surface; đã ghi giới hạn, chưa nghiệm thu trực quan.

## Không đánh dấu hoàn tất toàn Master Prompt

**Cập nhật22/10:** Independent reviews và music/navigation/MoneyInput đã có implementation/automated evidence trong các reports mới hơn, không tạo lại. Phase5 policy/basis/adjustment foundation nay cóAPI+SQLverified, nhưng scopedruntimeengine/numericVAT/basis/activation/financialdelta/AdminUI/negotiation/partnership còn thiếu. Extended browser và host/load/restore vẫn chưa nghiệm thu. Xem [master checklist](master-upgrade-checklist.md), [roadmap](PRODUCT_COMPLETION_ROADMAP.md) và [START_HERE](START_HERE.md).

Deployment vẫn tạm dừng theo yêu cầu. Không force-push/reset/xóa dữ liệu hoặc tự đặt chính sách tài chính mới.

Gate refactor lịch sử20/10:929/929 backend có SQL,229/229 frontend; HTTP QA28/28, local schema15/API5008 xác minh lúc đó. Browser khi đó chưa có surface; targeted checks sau xem reports mới, không suy fullE2E. Backendgate22/10 là1135/1135actualSQL/0skip, không chứng nhận main schema hay latestbrowser. Xem [báo cáo cũ](reports/vendor-services-fixed-combos-2026-10-20.md) để phân biệt kết quả local refresh với việc schema đã được áp dụng trước guard.
