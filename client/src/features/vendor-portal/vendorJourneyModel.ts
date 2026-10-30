/** Read-only guidance: a journey is not a publication, payment or commission approval. */
export const VENDOR_JOURNEY_TABS = [
  { id: "overview", label: "Tổng quan" },
  { id: "guide", label: "Hướng dẫn từng bước" },
  { id: "listings", label: "Dịch vụ của tôi" },
  { id: "combos", label: "Combo của tôi" },
  { id: "leads", label: "Yêu cầu tư vấn" },
  { id: "quotes", label: "Báo giá" },
  { id: "change-requests", label: "Thay đổi & hủy" },
  { id: "appointments", label: "Lịch hẹn" },
  { id: "contracts-couple", label: "Hợp đồng với cặp đôi" },
  { id: "contracts-admin", label: "Hoa hồng & đối soát" },
  { id: "partnership-policy", label: "Chính sách hợp tác" },
  { id: "verification", label: "Xác minh" },
] as const;

export type VendorJourneyTab = (typeof VENDOR_JOURNEY_TABS)[number]["id"];

export interface VendorJourneySummary {
  vendorName: string;
  isVerified: boolean;
  latestVerificationStatus: string | null;
  taxProfileStatus: string | null;
  policyAccepted: boolean;
  allowInCombos: boolean;
  listingCount: number;
  publicListingCount: number;
  pendingListingCount: number;
  rejectedListingCount: number;
  serviceItemCount: number;
  approvedServiceItemCount: number;
  comboCount: number;
  approvedComboCount: number;
  pendingComboCount: number;
  newLeadCount: number;
  activeLeadCount: number;
  quoteCount: number;
  contractCount: number;
  pendingOrderChangeCount: number;
  upcomingAppointmentCount: number;
}

export interface VendorJourneyStage {
  id: string;
  title: string;
  shortTitle: string;
  tab: VendorJourneyTab;
  summary: string;
  steps: readonly string[];
  outcome: string;
  nextActor: string;
  caution: string;
  optional?: boolean;
  relatedTabs: readonly { tab: VendorJourneyTab; label: string }[];
}

export const VENDOR_JOURNEY_STAGES = [
  {
    id: "verification", title: "Chuẩn bị và xác minh hồ sơ shop", shortTitle: "Hồ sơ shop", tab: "verification", optional: false,
    summary: "Bắt đầu bằng thông tin pháp nhân, thuế, ngân hàng và giấy tờ xác minh của nhà cung cấp.",
    steps: [
      "Mở Xác minh, điền tên pháp nhân, mã số thuế, mã ngân hàng, số tài khoản và tên chủ tài khoản chính xác; đọc thỏa thuận rồi lưu hồ sơ.",
      "Ở phần huy hiệu xác minh, nhập số giấy phép kinh doanh/mã số thuế và 3 đường dẫn HTTPS tới GPKD, giấy tờ đại diện và ảnh cơ sở, mỗi dòng một link.",
      "Nộp hồ sơ và theo dõi trạng thái cùng ghi chú của người duyệt. Không nộp thông tin ngân hàng hay giấy tờ nhạy cảm trong nội dung dịch vụ công khai.",
      "Nếu bị từ chối, sửa theo ghi chú và nộp lại. Hồ sơ thuế và hồ sơ huy hiệu là các trạng thái riêng, cần kiểm tra cả hai.",
    ],
    outcome: "Hồ sơ được lưu để bộ phận phụ trách kiểm tra; huy hiệu và hồ sơ thuế chỉ được duyệt ở bước xét duyệt riêng.",
    nextActor: "Nhà cung cấp nộp đúng hồ sơ; Moderator/Admin kiểm tra và trả kết quả.",
    caution: "Nộp hồ sơ không đồng nghĩa đã xác minh. Không có thời gian duyệt được bảo đảm; chỉnh thông tin hồ sơ đã duyệt có thể cần xác minh lại.",
    relatedTabs: [{ tab: "partnership-policy", label: "Xem chính sách hợp tác" }],
  },
  {
    id: "policy", title: "Hiểu chính sách hợp tác đang được ghi nhận", shortTitle: "Chính sách", tab: "partnership-policy", optional: false,
    summary: "Biết nhóm hoa hồng tương ứng với từng loại dịch vụ trước khi tư vấn, báo giá hoặc ký hợp đồng.",
    steps: [
      "Mở Chính sách hợp tác để xem các nhóm hoa hồng tham khảo. Không cần chọn nhóm khi đăng ký; nhóm và mức mặc định được hiển thị khi bạn chọn loại dịch vụ.",
      "Nếu shop thuộc dữ liệu cũ, đọc cảnh báo chính sách cũ được giữ nguyên; liên hệ Admin khi cần rà soát hoặc chuyển đổi.",
      "Với dịch vụ tổ chức cưới A–Z, trao đổi với Admin về cơ sở phí quản lý/tổ chức/điều phối được phê duyệt; không dùng toàn bộ ngân sách cưới làm cơ sở mặc định.",
      "Khi cần thỏa thuận riêng hoặc điều chỉnh một hợp đồng đang thực hiện, liên hệ Admin và lưu trao đổi/chứng từ. Hướng dẫn này không cung cấp luồng tự duyệt hay tự đổi hoa hồng.",
    ],
    outcome: "Bạn biết dữ liệu chính sách đang được lưu và đầu mối xử lý các trường hợp cần phê duyệt.",
    nextActor: "Nhà cung cấp đọc và đối chiếu; Admin xử lý các thay đổi cần phê duyệt.",
    caution: "Đây là khu vực chỉ xem. Đọc hướng dẫn, nhắn tin hoặc duyệt nội dung dịch vụ không kích hoạt chính sách hoa hồng mới; snapshot giao dịch đã chốt được giữ nguyên.",
    relatedTabs: [{ tab: "contracts-admin", label: "Xem hoa hồng & đối soát" }],
  },
  {
    id: "listings", title: "Đăng dịch vụ để khách có thể tìm thấy", shortTitle: "Đăng dịch vụ", tab: "listings", optional: false,
    summary: "Bài dịch vụ giới thiệu shop và là đầu vào để khách gửi yêu cầu tư vấn.",
    steps: [
      "Mở Dịch vụ của tôi, tạo bài mới và tự chọn loại dịch vụ đang hoạt động; xem nhóm hoa hồng và mức mặc định tương ứng, không cần Admin bổ sung nhóm.",
      "Điền tên, khu vực, khoảng giá, mô tả, hạng mục bao gồm/không bao gồm và thêm ảnh thực tế; kiểm tra nội dung trước khi gửi.",
      "Lưu nháp, mở xem trước riêng tư rồi gửi duyệt. Theo dõi trạng thái và sửa theo lý do từ chối nếu có.",
      "Sau khi bài được duyệt, kiểm tra trạng thái bật/ẩn. Khi chỉnh bài đã công khai, gửi bản sửa để duyệt; bản sửa không tự thay nội dung đã được duyệt ngay.",
    ],
    outcome: "Bài đã duyệt và đang bật có thể được hiển thị công khai theo điều kiện của hệ thống; khách có thể bắt đầu gửi yêu cầu.",
    nextActor: "Nhà cung cấp chuẩn bị nội dung; Moderator/Admin duyệt; khách chọn dịch vụ và gửi yêu cầu.",
    caution: "Có bài trong danh sách không có nghĩa đã công khai. Bài nháp, chờ duyệt, bị từ chối hoặc đang ẩn không tương đương bài đang hiển thị.",
    relatedTabs: [{ tab: "verification", label: "Kiểm tra hồ sơ shop" }, { tab: "leads", label: "Mở yêu cầu tư vấn" }],
  },
  {
    id: "combos", title: "Kết hợp dịch vụ thành combo của riêng shop", shortTitle: "Combo riêng", tab: "combos", optional: true,
    summary: "Chọn nhiều dịch vụ đã được duyệt của shop và nhập giá bán cho một gói cố định.",
    steps: [
      "Mở Combo của tôi, đọc phần tham gia combo; bật tham gia và cấu hình thông tin giảm giá trong phạm vi hệ thống cho phép nếu muốn bán theo combo.",
      "Tạo dịch vụ một lần tại Dịch vụ của tôi, thiết lập giá bán lẻ và gửi duyệt. Biến thể, món kèm hiện có được quản lý trong màn hình chỉnh sửa dịch vụ.",
      "Chọn ít nhất hai dịch vụ độc lập đã duyệt và đang hoạt động, nhập giá combo, kiểm tra mức tiết kiệm, ảnh và điều kiện rồi gửi duyệt riêng cho combo.",
      "Dùng xem trước và lịch sử phiên bản để rà soát. Sau khi được duyệt, kiểm tra cả trạng thái hoạt động và điều kiện hồ sơ trước khi tìm combo trên trang công khai.",
    ],
    outcome: "Khách xem đúng thành phần và giá của combo đã duyệt rồi gửi yêu cầu; điều chỉnh riêng được trao đổi trong chat và xác nhận trên giao dịch.",
    nextActor: "Nhà cung cấp chọn dịch vụ và tạo combo; Moderator/Admin duyệt nội dung công khai; khách gửi yêu cầu về shop.",
    caution: "Combo chỉ gồm dịch vụ của một nhà cung cấp. Được duyệt chưa bảo đảm hiển thị nếu gói/món/bài bị tắt, hết hiệu lực hoặc hồ sơ chưa đủ điều kiện. Không tự cộng hoa hồng cả gói lẫn các món.",
    relatedTabs: [{ tab: "listings", label: "Chuẩn bị bài dịch vụ" }, { tab: "leads", label: "Theo dõi yêu cầu từ combo" }],
  },
  {
    id: "appointments", title: "Mở và quản lý lịch hẹn tư vấn", shortTitle: "Lịch hẹn", tab: "appointments", optional: true,
    summary: "Dùng khi shop nhận lịch tư vấn, gặp trực tiếp hoặc các lịch hẹn theo cấu hình hiện có.",
    steps: [
      "Mở Lịch hẹn, cấu hình loại lịch, giờ/ngày nhận khách, sức chứa và tiền cọc nếu loại lịch cần; thêm ngày ngoại lệ khi shop bận.",
      "Kiểm tra thông tin khách và khung giờ trước khi duyệt lịch; phối hợp đổi giờ theo thao tác và trạng thái được hỗ trợ.",
      "Nếu có cọc, kiểm tra khoản tiền thực nhận trước khi xác nhận. Không coi việc tải ảnh chuyển khoản là đã nhận tiền.",
      "Khi khách đến, cập nhật check-in và hoàn tất lịch theo thực tế; tiếp tục trao đổi dịch vụ/báo giá ở các mục tương ứng.",
    ],
    outcome: "Lịch hẹn được theo dõi theo trạng thái riêng, hỗ trợ quá trình tư vấn nhưng không thay thế báo giá hoặc hợp đồng.",
    nextActor: "Nhà cung cấp mở lịch và xử lý yêu cầu; khách đặt/đổi lịch và tham dự.",
    caution: "Đây là công cụ tùy chọn, không phải bước bắt buộc để nhận mọi yêu cầu. Xác nhận lịch hẹn không đồng nghĩa xác nhận hợp đồng dịch vụ.",
    relatedTabs: [{ tab: "leads", label: "Theo dõi tư vấn" }, { tab: "quotes", label: "Chuẩn bị báo giá" }],
  },
  {
    id: "leads", title: "Tiếp nhận yêu cầu và trao đổi với khách", shortTitle: "Tiếp nhận khách", tab: "leads", optional: false,
    summary: "Mỗi yêu cầu tư vấn là một đầu mối làm việc, có thể đến từ dịch vụ lẻ hoặc combo.",
    steps: [
      "Mở Yêu cầu tư vấn và kiểm tra yêu cầu mới: dịch vụ/gói, ngày cưới, ngân sách và ghi chú của khách.",
      "Nếu phù hợp, bấm tiếp nhận và trao đổi trong chat để làm rõ nhu cầu, phạm vi dịch vụ, thời gian và các khoản tiền dự kiến.",
      "Theo dõi trạng thái và thời hạn hiển thị trên từng yêu cầu; phản hồi các yêu cầu đang trao đổi trước khi tạo báo giá.",
      "Chỉ dùng thông tin liên hệ được hệ thống cho phép hiển thị. Khi đã thống nhất nội dung sơ bộ, chuyển sang Báo giá hoặc lập hợp đồng theo luồng được hỗ trợ.",
    ],
    outcome: "Yêu cầu được tiếp nhận và có nội dung trao đổi rõ ràng để làm căn cứ báo giá/hợp đồng.",
    nextActor: "Nhà cung cấp tiếp nhận và hỏi thêm; khách cung cấp nhu cầu, hai bên thống nhất nội dung sơ bộ.",
    caution: "Tiếp nhận yêu cầu không tự mở mọi thông tin cá nhân, không phải khách đã mua dịch vụ và không xác nhận thanh toán.",
    relatedTabs: [{ tab: "quotes", label: "Gửi báo giá" }, { tab: "contracts-couple", label: "Theo dõi hợp đồng" }],
  },
  {
    id: "quotes", title: "Gửi báo giá có hạng mục và điều khoản rõ ràng", shortTitle: "Báo giá", tab: "quotes", optional: false,
    summary: "Gửi cho yêu cầu đã tiếp nhận/đang trao đổi để khách xem trước khi xác nhận hợp đồng riêng.",
    steps: [
      "Mở Báo giá và chọn yêu cầu ở trạng thái đã tiếp nhận hoặc đang trao đổi. Nếu chưa thấy yêu cầu, kiểm tra lại trong Yêu cầu tư vấn.",
      "Điền từng hạng mục, số lượng, đơn giá, VAT, tiền cọc và điều khoản; đối chiếu lại tổng tiền trước khi gửi.",
      "Gửi báo giá, kiểm tra hạn hiệu lực trong danh sách báo giá đã gửi rồi chờ khách phản hồi. Nếu cần thay đổi, tạo bản báo giá mới theo thao tác hiện có; bản mới thay báo giá cũ của yêu cầu.",
      "Khi khách chấp nhận, mở Hợp đồng với cặp đôi để kiểm tra bản hợp đồng được tạo và chờ bước xác nhận hợp đồng riêng của khách.",
    ],
    outcome: "Khách có báo giá chi tiết để phản hồi; báo giá được chấp nhận có thể chuyển thành hợp đồng chờ xác nhận.",
    nextActor: "Nhà cung cấp gửi báo giá; khách xem, chấp nhận hoặc trao đổi lại; sau đó khách xác nhận hợp đồng ở bước riêng.",
    caution: "Gửi/chấp nhận báo giá không đồng nghĩa hợp đồng đã được xác nhận hoặc tiền cọc đã thực nhận. Không tự coi VAT trên báo giá là cơ sở hoa hồng đã được Admin duyệt.",
    relatedTabs: [{ tab: "leads", label: "Mở cuộc trao đổi" }, { tab: "contracts-couple", label: "Kiểm tra hợp đồng" }],
  },
  {
    id: "contracts", title: "Kiểm tra và theo dõi hợp đồng với cặp đôi", shortTitle: "Hợp đồng", tab: "contracts-couple", optional: false,
    summary: "Tách rõ bản hợp đồng, xác nhận của khách và việc thực hiện/thanh toán thực tế.",
    steps: [
      "Mở Hợp đồng với cặp đôi và kiểm tra hợp đồng từ báo giá được chấp nhận; hoặc lập hợp đồng từ yêu cầu sau khi hai bên đã trao đổi.",
      "Đối chiếu phạm vi, ngày thực hiện, giá trị và nội dung thanh toán trước khi gửi cho khách xác nhận.",
      "Theo dõi trạng thái chờ xác nhận. Nếu khách báo thông tin sai và hợp đồng trở về nháp, sửa nội dung rồi gửi lại theo luồng hiện có.",
      "Trong quá trình thực hiện, theo dõi nội dung đã chốt và các khoản tiền theo thao tác được hỗ trợ. Khi phát sinh thay đổi/hủy, mở mục Thay đổi & hủy để giữ lịch sử.",
    ],
    outcome: "Hai bên có bản hợp đồng và trạng thái xác nhận để theo dõi; dữ liệu tài chính đã chốt được bảo toàn.",
    nextActor: "Nhà cung cấp kiểm tra/gửi hợp đồng; khách xác nhận hoặc báo sai; hai bên thực hiện theo thỏa thuận.",
    caution: "Không tự sửa snapshot hoa hồng hợp đồng cũ. Luồng ngoại lệ hoa hồng riêng đang chưa hoàn thiện; cần làm việc với Admin, không có nút tự thay tỷ lệ ở đây.",
    relatedTabs: [{ tab: "change-requests", label: "Xử lý thay đổi & hủy" }, { tab: "contracts-admin", label: "Xem đối soát" }],
  },
  {
    id: "changes", title: "Phản hồi yêu cầu thay đổi hoặc hủy", shortTitle: "Thay đổi & hủy", tab: "change-requests", optional: false,
    summary: "Giữ nội dung trao đổi và trạng thái xử lý khi khách muốn sửa lựa chọn hoặc dừng yêu cầu.",
    steps: [
      "Mở Thay đổi & hủy, đọc đề nghị của khách, đối tượng liên quan và lịch sử phản hồi.",
      "Rà soát phạm vi thay đổi, tình trạng hợp đồng và các khoản tiền trước khi phản hồi tiếp nhận hoặc từ chối kèm nội dung rõ ràng.",
      "Với yêu cầu có ràng buộc hợp đồng/thanh toán, phối hợp bộ phận phụ trách hoặc Finance theo trạng thái hệ thống, không tự xử lý bằng cách xóa lịch sử.",
      "Theo dõi phản hồi tiếp theo. Nếu hai bên thống nhất thay đổi, bảo đảm nội dung hợp đồng và xử lý tài chính đi qua bước xác nhận riêng tương ứng.",
    ],
    outcome: "Đề nghị có phản hồi và lịch sử để các bên tiếp tục xử lý đúng luồng.",
    nextActor: "Nhà cung cấp phản hồi; khách và bộ phận phụ trách/Finance phối hợp khi có ràng buộc.",
    caution: "Bấm tiếp nhận đề nghị không tự sửa hợp đồng, hủy hợp đồng, hoàn tiền hay thay đổi hoa hồng. Không hứa khoản hoàn tiền trước khi có xử lý được phê duyệt.",
    relatedTabs: [{ tab: "contracts-couple", label: "Đối chiếu hợp đồng" }, { tab: "contracts-admin", label: "Đối chiếu công nợ" }],
  },
  {
    id: "settlement", title: "Đối chiếu hoa hồng và công nợ", shortTitle: "Đối soát", tab: "contracts-admin", optional: false,
    summary: "Theo dõi bảng kê theo kỳ, đối chiếu hợp đồng và trạng thái ghi nhận thanh toán thực tế.",
    steps: [
      "Mở Hoa hồng & đối soát, chọn tháng/năm cần kiểm tra và đọc bảng kê, hợp đồng, hoa hồng/công nợ và trạng thái đang hiển thị.",
      "Đối chiếu số liệu với hợp đồng cùng các khoản thanh toán đã được ghi nhận. Nếu có sai lệch, giữ chứng từ và liên hệ bộ phận phụ trách để rà soát.",
      "Nếu luồng hiện tại cung cấp QR hoặc ghi nhận thanh toán, kiểm tra đúng kỳ, nội dung và số tiền; tiếp tục theo dõi trạng thái xác nhận sau thao tác.",
      "Khi muốn điều chỉnh chính sách hoặc một hợp đồng đang thực hiện, liên hệ Admin; không sửa dữ liệu gốc hoặc coi tin nhắn là phê duyệt tài chính.",
    ],
    outcome: "Bạn có thông tin đối chiếu theo kỳ và biết khoản nào cần phối hợp xác minh; chưa thể suy ra đã quyết toán chỉ từ số lượng hợp đồng.",
    nextActor: "Nhà cung cấp đối chiếu; Finance/Admin xác minh và xử lý các nghiệp vụ thuộc thẩm quyền.",
    caution: "QR, ảnh chuyển khoản hoặc thao tác gửi yêu cầu không phải bằng chứng tiền đã thực nhận. Công thức/quy trình hoa hồng nâng cấp chưa được triển khai đầy đủ; hướng dẫn không thay đổi chính sách đang áp dụng.",
    relatedTabs: [{ tab: "contracts-couple", label: "Đối chiếu hợp đồng" }, { tab: "partnership-policy", label: "Xem chính sách đang lưu" }],
  },
] as const satisfies readonly VendorJourneyStage[];

export type VendorJourneyStageId = (typeof VENDOR_JOURNEY_STAGES)[number]["id"];

export const VENDOR_GUIDE_FAQS = [
  { question: "Tôi có bắt buộc làm đủ các bước theo thứ tự không?", answer: "Không. Hồ sơ, chính sách và bài dịch vụ là phần chuẩn bị; yêu cầu tư vấn, báo giá, hợp đồng là luồng làm việc với khách. Combo và lịch hẹn là công cụ tùy chọn. Khi có khách mới hoặc yêu cầu thay đổi, hãy xử lý trước rồi quay lại phần chuẩn bị còn thiếu." },
  { question: "Bài dịch vụ, món dịch vụ và combo khác nhau thế nào?", answer: "Bài dịch vụ là nội dung giới thiệu công khai. Món dịch vụ là thành phần có giá/lựa chọn dùng trong combo. Mẫu combo gom các thành phần của chính một shop để khách xem và tùy chỉnh. Mỗi loại có bước duyệt và điều kiện hoạt động riêng." },
  { question: "Tôi đã tạo combo nhưng khách chưa thấy thì kiểm tra gì?", answer: "Kiểm tra shop đã bật tham gia, hồ sơ xác minh/thuế, bài dịch vụ đang hoạt động, món và mẫu combo đã duyệt, trạng thái bật và thời gian hiệu lực. Số combo được duyệt trên tổng quan chỉ là trạng thái xét duyệt, không phải số combo chắc chắn đang công khai." },
  { question: "Khách chấp nhận báo giá là đã chốt hợp đồng chưa?", answer: "Chưa. Khách chấp nhận báo giá có thể tạo hợp đồng chờ xác nhận; khách vẫn phải kiểm tra và xác nhận hợp đồng ở bước riêng. Cả hai thao tác đều không tự xác nhận đã nhận tiền." },
  { question: "Tôi có thể tự đổi tỷ lệ hoa hồng hoặc xin ngoại lệ trên web không?", answer: "Hiện chính sách hợp tác là khu vực chỉ xem. Luồng thương lượng hoa hồng, hợp đồng đối tác/phụ lục và ngoại lệ từng hợp đồng chưa hoàn thiện. Liên hệ Admin và giữ nội dung trao đổi/chứng từ. Vendor cũ giữ chính sách hiện tại đến khi Admin xác nhận; hợp đồng đã chốt giữ snapshot ban đầu. Chính sách mới chỉ được kích hoạt khi đủ phê duyệt Admin, đến ngày hiệu lực và hợp đồng/phụ lục liên quan được ghi nhận ký ngoài hệ thống; đọc hướng dẫn không thực hiện các bước đó." },
  { question: "Mô tả phí tổ chức trong dịch vụ A–Z đã là cơ sở hoa hồng được duyệt chưa?", answer: "Chưa. Mô tả nội dung dịch vụ không thay thế phê duyệt tài chính. Cơ sở phí quản lý/tổ chức/điều phối cần được xác định và Admin phê duyệt theo chính sách; không mặc định lấy toàn bộ ngân sách cưới. Các khoản VAT, giảm giá và chi hộ cũng không được tự suy đoán hay tự khấu trừ từ hướng dẫn này." },
  { question: "Tổng quan có nghĩa shop đã hoàn thành mọi việc khi có dữ liệu không?", answer: "Không. “Có dữ liệu” chỉ cho biết hệ thống đã lưu dữ liệu ở mục đó. Nó không xác nhận nội dung đã công khai, khách đã ký hợp đồng, tiền đã thực nhận hay kỳ đối soát đã hoàn tất. Trạng thái chi tiết trong từng mục mới là căn cứ xử lý." },
] as const;

export function vendorJourneyTab(value: string | null | undefined): VendorJourneyTab {
  return VENDOR_JOURNEY_TABS.find(tab => tab.id === value)?.id ?? "overview";
}

/** Preserve listing-filter bookmarks created before the dashboard gained an overview. */
export function vendorDashboardTab(params: URLSearchParams): VendorJourneyTab {
  if (params.has("tab")) return vendorJourneyTab(params.get("tab"));
  return ["serviceKeyword", "serviceStatus", "servicePage", "servicePageSize"].some(name => params.has(name)) ? "listings" : "overview";
}

export function vendorJourneyHref(tab: string | null | undefined, guideStep?: string | null): string {
  const safeTab = vendorJourneyTab(tab);
  if (safeTab === "overview") return "/vendor/dashboard";
  const query = new URLSearchParams({ tab: safeTab });
  if (safeTab === "guide" && VENDOR_JOURNEY_STAGES.some(stage => stage.id === guideStep)) query.set("guideStep", guideStep!);
  return `/vendor/dashboard?${query.toString()}`;
}

export interface VendorJourneyStageStatus {
  label: "Chưa tải dữ liệu" | "Chưa phát sinh" | "Cần chuẩn bị" | "Chờ duyệt" | "Có dữ liệu" | "Tùy chọn" | "Cần xử lý" | "Cần kiểm tra";
  tone: "neutral" | "attention" | "pending" | "positive" | "optional";
  note: string;
}

export function vendorJourneyStageStatus(stageId: string, data?: VendorJourneySummary | null): VendorJourneyStageStatus {
  if (!data) return { label: "Chưa tải dữ liệu", tone: "neutral", note: "Chưa có dữ liệu tổng quan để kết luận trạng thái. Bạn vẫn có thể đọc hướng dẫn và mở từng mục." };
  switch (stageId) {
    case "verification":
      if (data.taxProfileStatus === "Rejected" || data.taxProfileStatus === "Suspended" || (!data.isVerified && ["Rejected", "Suspended"].includes(data.latestVerificationStatus ?? "")))
        return { label: "Cần xử lý", tone: "attention", note: "Có hồ sơ cần rà soát. Mở Xác minh để đọc ghi chú và trạng thái của từng hồ sơ." };
      if (data.isVerified && data.taxProfileStatus === "Approved")
        return { label: "Có dữ liệu", tone: "positive", note: "Shop có huy hiệu xác minh và hồ sơ thuế đã duyệt. Các điều kiện nội dung/công khai vẫn được kiểm tra riêng." };
      if (data.latestVerificationStatus === "Submitted" || data.taxProfileStatus === "Submitted")
        return { label: "Chờ duyệt", tone: "pending", note: "Có hồ sơ đang chờ xét duyệt. Kiểm tra cả hồ sơ thuế và hồ sơ huy hiệu; không có thời gian duyệt được bảo đảm." };
      return { label: "Cần chuẩn bị", tone: "attention", note: "Kiểm tra thông tin pháp nhân, thuế/ngân hàng và giấy tờ xác minh trước khi đưa dịch vụ vào hoạt động." };
    case "policy":
      return data.policyAccepted
        ? { label: "Có dữ liệu", tone: "positive", note: "Đã ghi nhận phiên bản và thời điểm xác nhận chính sách. Điều này không tự kích hoạt thỏa thuận hoa hồng mới." }
        : { label: "Cần kiểm tra", tone: "attention", note: "Chưa ghi nhận xác nhận điều khoản mới. Bạn vẫn được chọn loại dịch vụ khi tạo bài; shop cũ giữ chính sách hiện tại. Xem chính sách tham khảo hoặc liên hệ Admin nếu cần điều chỉnh chính sách riêng." };
    case "listings":
      if (data.rejectedListingCount > 0) return { label: "Cần xử lý", tone: "attention", note: `${data.rejectedListingCount} bài/bản sửa bị từ chối; kiểm tra lý do. ${data.publicListingCount} bài đã duyệt và đang bật.` };
      if (data.pendingListingCount > 0) return { label: "Chờ duyệt", tone: "pending", note: `${data.pendingListingCount} bài/bản sửa đang chờ duyệt. ${data.publicListingCount} bài đã duyệt và đang bật; bản sửa không tự thay nội dung công khai.` };
      if (data.publicListingCount > 0) return { label: "Có dữ liệu", tone: "positive", note: `${data.publicListingCount} bài đã duyệt và đang bật. Kiểm tra nội dung và điều kiện shop khi cần.` };
      return { label: "Cần chuẩn bị", tone: "attention", note: data.listingCount > 0 ? `Đã có ${data.listingCount} bài nhưng chưa có bài đã duyệt và đang bật; mở danh sách để kiểm tra nháp/trạng thái.` : "Chưa có bài dịch vụ. Tạo bài, xem trước và gửi duyệt để bắt đầu." };
    case "combos":
      if (!data.allowInCombos) return { label: "Tùy chọn", tone: "optional", note: "Shop chưa bật tham gia combo. Bạn vẫn có thể làm việc với khách qua dịch vụ lẻ." };
      if (data.pendingComboCount > 0) return { label: "Chờ duyệt", tone: "pending", note: `${data.pendingComboCount} mẫu combo/bản sửa đang chờ duyệt. Điều kiện công khai được kiểm tra riêng.` };
      if (data.approvedComboCount > 0) return { label: "Có dữ liệu", tone: "positive", note: `${data.approvedComboCount} mẫu combo được duyệt; số này không khẳng định gói đang bật, còn hiệu lực hoặc đủ điều kiện công khai.` };
      return { label: "Cần chuẩn bị", tone: "attention", note: data.approvedServiceItemCount > 0 ? `Có ${data.approvedServiceItemCount} cấu hình dịch vụ đã duyệt; mở Combo của tôi để kiểm tra dịch vụ đủ điều kiện và tạo gói.` : "Shop đã bật tham gia. Tạo dịch vụ và thiết lập giá tại Dịch vụ của tôi, gửi duyệt trước khi chọn vào combo." };
    case "appointments":
      return data.upcomingAppointmentCount > 0
        ? { label: "Có dữ liệu", tone: "positive", note: `${data.upcomingAppointmentCount} lịch hẹn đang hoạt động trong tương lai. Mở lịch để kiểm tra giờ và trạng thái cần xử lý.` }
        : { label: "Tùy chọn", tone: "optional", note: "Chưa có lịch hẹn sắp tới đang hoạt động. Bạn có thể cấu hình lịch nhận khách nếu shop cần." };
    case "leads":
      if (data.newLeadCount > 0) return { label: "Cần xử lý", tone: "attention", note: `${data.newLeadCount} yêu cầu mới đang chờ tiếp nhận. Kiểm tra nhu cầu và thời hạn trên từng yêu cầu.` };
      if (data.activeLeadCount > 0) return { label: "Có dữ liệu", tone: "positive", note: `${data.activeLeadCount} yêu cầu đã tiếp nhận/đang trao đổi. Tiếp tục phản hồi khách và chuẩn bị báo giá.` };
      return { label: "Cần chuẩn bị", tone: "neutral", note: "Chưa có yêu cầu mới hoặc đang trao đổi. Kiểm tra dịch vụ công khai và quay lại khi có khách gửi yêu cầu." };
    case "quotes":
      return data.quoteCount > 0
        ? { label: "Có dữ liệu", tone: "positive", note: `${data.quoteCount} báo giá đã được lưu. Mở từng báo giá để xem trạng thái; số lượng không thể hiện khách đã chấp nhận hay đã ký hợp đồng.` }
        : { label: "Cần chuẩn bị", tone: "neutral", note: "Chưa có báo giá. Tiếp nhận và trao đổi yêu cầu trước khi gửi các hạng mục cùng điều khoản." };
    case "contracts":
      return data.contractCount > 0
        ? { label: "Có dữ liệu", tone: "positive", note: `${data.contractCount} hợp đồng được lưu. Cần mở từng hợp đồng để kiểm tra xác nhận của khách, việc thực hiện và các khoản tiền.` }
        : { label: "Cần chuẩn bị", tone: "neutral", note: "Chưa có hợp đồng. Trao đổi/báo giá trước; khách xác nhận hợp đồng ở bước riêng." };
    case "changes":
      return data.pendingOrderChangeCount > 0
        ? { label: "Cần xử lý", tone: "attention", note: `${data.pendingOrderChangeCount} đề nghị thay đổi/hủy đang chờ xử lý. Tiếp nhận đề nghị không tự sửa hợp đồng hoặc hoàn tiền.` }
        : { label: "Chưa phát sinh", tone: "neutral", note: "Hiện không có đề nghị thay đổi/hủy chờ xử lý. Điều này không kết luận hợp đồng hay nghĩa vụ tài chính đã hoàn tất." };
    case "settlement":
      return { label: "Cần kiểm tra", tone: "neutral", note: "Mở bảng kê theo kỳ để kiểm tra hoa hồng, công nợ và trạng thái thanh toán. Tổng quan không có dữ liệu để kết luận đã quyết toán." };
    default:
      return { label: "Chưa tải dữ liệu", tone: "neutral", note: "Chưa xác định được bước hướng dẫn. Mở hướng dẫn từng bước để chọn mục phù hợp." };
  }
}

export interface VendorJourneyNextAction {
  stageId: VendorJourneyStageId;
  tab: VendorJourneyTab;
  path: string;
  label: string;
  note: string;
}

function nextAction(stageId: VendorJourneyStageId, label: string, note: string): VendorJourneyNextAction {
  const stage = VENDOR_JOURNEY_STAGES.find(item => item.id === stageId)!;
  return { stageId, tab: stage.tab, path: vendorJourneyHref(stage.tab), label, note };
}

/** Suggest a useful next action, never gate routes or infer overall business completion. */
export function vendorJourneyNext(data?: VendorJourneySummary | null): VendorJourneyNextAction | null {
  if (!data) return null;
  if (data.pendingOrderChangeCount > 0) return nextAction("changes", "Phản hồi thay đổi & hủy", "Có đề nghị của khách đang chờ xử lý. Đọc nội dung và lịch sử trước khi phản hồi.");
  if (data.newLeadCount > 0) return nextAction("leads", "Tiếp nhận yêu cầu mới", "Khách đang chờ phản hồi. Kiểm tra nhu cầu, thời hạn và trao đổi trước khi báo giá.");
  if (data.activeLeadCount > 0) return nextAction("leads", "Tiếp tục trao đổi với khách", "Bạn có yêu cầu đã tiếp nhận/đang trao đổi. Kiểm tra phản hồi trước khi chuyển sang báo giá hoặc hợp đồng.");
  if (data.upcomingAppointmentCount > 0) return nextAction("appointments", "Kiểm tra lịch hẹn sắp tới", "Xem giờ, thông tin khách và trạng thái cần xử lý; xác nhận tiền chỉ sau khi kiểm tra thực nhận.");
  if (data.taxProfileStatus === "Rejected" || data.taxProfileStatus === "Suspended" || (!data.isVerified && ["Rejected", "Suspended"].includes(data.latestVerificationStatus ?? "")))
    return nextAction("verification", "Rà soát hồ sơ cần xử lý", "Đọc ghi chú của người duyệt và kiểm tra cả hồ sơ thuế lẫn hồ sơ xác minh.");
  if (!data.isVerified || data.taxProfileStatus !== "Approved") return nextAction("verification", data.latestVerificationStatus === "Submitted" || data.taxProfileStatus === "Submitted" ? "Theo dõi hồ sơ đang chờ duyệt" : "Chuẩn bị hồ sơ shop", "Kiểm tra các trạng thái riêng của hồ sơ thuế và huy hiệu; không nộp lại hoặc coi hồ sơ đã được duyệt chỉ từ hướng dẫn.");
  if (data.rejectedListingCount > 0) return nextAction("listings", "Sửa bài hoặc bản sửa bị từ chối", "Đọc lý do từ chối, sửa nội dung và gửi duyệt lại. Nội dung đang công khai không bị tự thay thế.");
  if (data.pendingListingCount > 0) return nextAction("listings", "Theo dõi bài đang chờ duyệt", "Mở trạng thái và lịch sử phiên bản; không cần tạo bài trùng chỉ vì bản sửa đang chờ duyệt.");
  if (!data.policyAccepted && data.listingCount === 0 && data.quoteCount === 0 && data.contractCount === 0) return nextAction("policy", "Đối chiếu chính sách hợp tác", "Đây là mục chỉ xem, không phải bước khóa thao tác. Chính sách cũ giữ nguyên; bạn vẫn có thể mở Dịch vụ của tôi và liên hệ Admin khi cần rà soát.");
  if (data.publicListingCount === 0) return nextAction("listings", data.listingCount > 0 ? "Kiểm tra bài dịch vụ của shop" : "Tạo bài dịch vụ đầu tiên", "Chuẩn bị nội dung, xem trước và gửi duyệt; chỉ bài đã duyệt và đang bật mới có thể hiển thị công khai.");
  if (data.contractCount > 0) return nextAction("contracts", "Theo dõi hợp đồng với cặp đôi", "Kiểm tra trạng thái xác nhận và nghĩa vụ đang thực hiện. Có hợp đồng không có nghĩa đã thanh toán hay quyết toán xong.");
  if (data.quoteCount > 0) return nextAction("quotes", "Theo dõi phản hồi báo giá", "Mở báo giá để xem trạng thái. Khách chấp nhận báo giá vẫn cần xác nhận hợp đồng ở bước riêng.");
  if (data.allowInCombos && data.approvedComboCount === 0) return nextAction("combos", data.pendingComboCount > 0 ? "Theo dõi combo đang chờ duyệt" : "Chuẩn bị combo của riêng shop", "Combo là tùy chọn. Chọn ít nhất hai dịch vụ độc lập đã duyệt và đang hoạt động của chính shop; nhập giá bán gói và gửi duyệt riêng.");
  return nextAction("leads", "Theo dõi yêu cầu tư vấn", "Shop đã có bài dịch vụ đã duyệt và đang bật. Theo dõi yêu cầu mới; bạn vẫn có thể xem lại hướng dẫn hoặc dùng combo/lịch hẹn khi cần.");
}
