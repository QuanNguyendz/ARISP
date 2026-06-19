import { Link } from 'react-router-dom'
import LegalPageShell, { LegalSection } from '@components/legal/LegalPageShell'

// NOTE: Nội dung mang tính mẫu cho nền tảng ARISP, nên được bộ phận pháp chế rà soát trước khi go-live.
export default function TermsPage() {
  return (
    <LegalPageShell
      title="Điều khoản sử dụng"
      lastUpdated="18/06/2026"
      intro={
        <p>
          Chào mừng bạn đến với ARISP — nền tảng tuyển dụng tích hợp phỏng vấn tự động bằng AI. Khi
          tạo tài khoản hoặc sử dụng dịch vụ, bạn xác nhận đã đọc, hiểu và đồng ý với các điều khoản
          dưới đây. Vui lòng đọc kỹ trước khi tiếp tục.
        </p>
      }
    >
      <LegalSection title="1. Chấp nhận điều khoản">
        <p>
          Bằng việc đăng ký tài khoản ứng viên, nộp hồ sơ hoặc tham gia phỏng vấn trên ARISP, bạn
          đồng ý chịu sự ràng buộc bởi Điều khoản sử dụng này cùng{' '}
          <Link to="/privacy" className="text-brand-600 hover:underline">
            Chính sách bảo mật
          </Link>
          . Nếu bạn không đồng ý, vui lòng không sử dụng nền tảng.
        </p>
      </LegalSection>

      <LegalSection title="2. Tài khoản ứng viên">
        <ul className="list-disc space-y-1.5 pl-5">
          <li>Bạn có thể đăng ký miễn phí bằng email cá nhân hoặc đăng nhập qua Google.</li>
          <li>
            Tài khoản đăng ký bằng email/mật khẩu cần được xác minh qua email trước khi đăng nhập
            lần đầu.
          </li>
          <li>
            Bạn chịu trách nhiệm bảo mật thông tin đăng nhập và mọi hoạt động diễn ra trên tài
            khoản.
          </li>
          <li>Mỗi cá nhân chỉ nên sử dụng một tài khoản với thông tin chính xác, cập nhật.</li>
        </ul>
      </LegalSection>

      <LegalSection title="3. Quy trình phỏng vấn bằng AI">
        <p>
          ARISP sử dụng trí tuệ nhân tạo để phỏng vấn ứng viên qua nhiều vòng. Khi tham gia, bạn
          hiểu rằng:
        </p>
        <ul className="list-disc space-y-1.5 pl-5">
          <li>
            Buổi phỏng vấn có thể được ghi âm, ghi hình và chuyển thành văn bản (transcript) phục vụ
            đánh giá.
          </li>
          <li>
            AI đưa ra kết quả đề xuất (Pass / Not Pass) cùng điểm số mang tính{' '}
            <strong>tham khảo</strong>; quyết định tuyển dụng cuối cùng thuộc về bộ phận nhân sự
            (HR).
          </li>
          <li>
            Phỏng vấn thật (On-site) được thực hiện tại văn phòng thông qua mã phỏng vấn dùng một
            lần.
          </li>
        </ul>
      </LegalSection>

      <LegalSection title="4. Trách nhiệm của ứng viên">
        <ul className="list-disc space-y-1.5 pl-5">
          <li>Cung cấp thông tin và hồ sơ (CV) trung thực, không giả mạo.</li>
          <li>
            Không gian lận, nhờ người khác làm thay, hoặc sử dụng công cụ trái phép trong khi phỏng
            vấn.
          </li>
          <li>
            Không can thiệp, dò quét hoặc gây ảnh hưởng đến hoạt động bình thường của hệ thống.
          </li>
          <li>Tôn trọng quyền sở hữu trí tuệ và tính bảo mật của nội dung trên nền tảng.</li>
        </ul>
      </LegalSection>

      <LegalSection title="5. Sở hữu trí tuệ">
        <p>
          Toàn bộ phần mềm, giao diện, thương hiệu, bộ câu hỏi và nội dung do ARISP cung cấp thuộc
          quyền sở hữu của ARISP và/hoặc doanh nghiệp vận hành. Bạn vẫn giữ quyền sở hữu đối với hồ
          sơ và nội dung do chính bạn cung cấp, đồng thời cấp cho ARISP quyền xử lý các nội dung này
          để phục vụ mục đích tuyển dụng.
        </p>
      </LegalSection>

      <LegalSection title="6. Giới hạn trách nhiệm">
        <p>
          Dịch vụ được cung cấp trên cơ sở “hiện trạng”. Kết quả đánh giá của AI có thể không hoàn
          toàn chính xác trong mọi trường hợp và không cấu thành cam kết tuyển dụng. ARISP không
          chịu trách nhiệm cho các thiệt hại gián tiếp phát sinh từ việc sử dụng nền tảng, trong
          phạm vi pháp luật cho phép.
        </p>
      </LegalSection>

      <LegalSection title="7. Tạm ngừng & chấm dứt">
        <p>
          Chúng tôi có quyền tạm ngừng hoặc chấm dứt tài khoản nếu phát hiện hành vi vi phạm điều
          khoản, gian lận hoặc gây rủi ro cho hệ thống. Bạn có thể yêu cầu xóa tài khoản bất cứ lúc
          nào theo hướng dẫn tại{' '}
          <Link to="/privacy" className="text-brand-600 hover:underline">
            Chính sách bảo mật
          </Link>
          .
        </p>
      </LegalSection>

      <LegalSection title="8. Thay đổi điều khoản">
        <p>
          ARISP có thể cập nhật Điều khoản sử dụng theo thời gian. Bản cập nhật sẽ được công bố trên
          trang này kèm ngày hiệu lực mới. Việc bạn tiếp tục sử dụng dịch vụ sau khi cập nhật đồng
          nghĩa với việc chấp nhận các thay đổi.
        </p>
      </LegalSection>

      <LegalSection title="9. Liên hệ">
        <p>
          Mọi thắc mắc về Điều khoản sử dụng, vui lòng liên hệ qua email:{' '}
          <a href="mailto:support@arisp.com" className="text-brand-600 hover:underline">
            support@arisp.com
          </a>
          .
        </p>
      </LegalSection>
    </LegalPageShell>
  )
}
