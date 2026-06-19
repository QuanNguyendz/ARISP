import { Link } from 'react-router-dom'
import LegalPageShell, { LegalSection } from '@components/legal/LegalPageShell'

// NOTE: Nội dung mang tính mẫu cho nền tảng ARISP, nên được bộ phận pháp chế rà soát trước khi go-live.
export default function PrivacyPolicyPage() {
  return (
    <LegalPageShell
      title="Chính sách bảo mật"
      lastUpdated="18/06/2026"
      intro={
        <p>
          ARISP coi trọng quyền riêng tư của bạn. Chính sách này mô tả những dữ liệu chúng tôi thu
          thập, cách sử dụng và bảo vệ chúng khi bạn tham gia quy trình tuyển dụng và phỏng vấn bằng
          AI trên nền tảng.
        </p>
      }
    >
      <LegalSection title="1. Dữ liệu chúng tôi thu thập">
        <ul className="list-disc space-y-1.5 pl-5">
          <li>
            <strong>Thông tin tài khoản:</strong> họ tên, email, số điện thoại, mật khẩu (được mã
            hóa) hoặc thông tin hồ sơ Google khi đăng nhập bằng Google.
          </li>
          <li>
            <strong>Hồ sơ ứng tuyển:</strong> CV (PDF/DOCX), kinh nghiệm, kỹ năng và các thông tin
            bạn cung cấp khi ứng tuyển.
          </li>
          <li>
            <strong>Dữ liệu phỏng vấn:</strong> bản ghi âm, ghi hình, văn bản hội thoại
            (transcript), câu trả lời và kết quả đánh giá của AI.
          </li>
          <li>
            <strong>Dữ liệu kỹ thuật:</strong> nhật ký đăng nhập, thiết bị, trình duyệt và địa chỉ
            IP nhằm bảo đảm an ninh.
          </li>
        </ul>
      </LegalSection>

      <LegalSection title="2. Mục đích sử dụng dữ liệu">
        <ul className="list-disc space-y-1.5 pl-5">
          <li>Tạo và quản lý tài khoản, xác minh email, đăng nhập an toàn.</li>
          <li>Phân tích mức độ phù hợp giữa CV và mô tả công việc (CV–JD Match).</li>
          <li>Vận hành buổi phỏng vấn AI, tạo câu hỏi và đánh giá kết quả.</li>
          <li>Hỗ trợ bộ phận nhân sự xem xét, xác nhận kết quả và liên hệ với bạn.</li>
          <li>Cải thiện chất lượng dịch vụ và bảo đảm an ninh hệ thống.</li>
        </ul>
      </LegalSection>

      <LegalSection title="3. Chia sẻ với bên thứ ba xử lý dữ liệu">
        <p>
          Để cung cấp tính năng AI, một phần dữ liệu có thể được xử lý bởi các nhà cung cấp dịch vụ
          đáng tin cậy, chỉ trong phạm vi cần thiết:
        </p>
        <ul className="list-disc space-y-1.5 pl-5">
          <li>Mô hình ngôn ngữ và phỏng vấn AI (xử lý câu hỏi, đánh giá hội thoại).</li>
          <li>Phân tích CV–JD bằng AI.</li>
          <li>Chuyển giọng nói thành văn bản, tổng hợp giọng nói và avatar phỏng vấn.</li>
          <li>Dịch vụ gửi email (xác minh tài khoản, thông báo kết quả).</li>
        </ul>
        <p>Chúng tôi không bán dữ liệu cá nhân của bạn cho bên thứ ba vì mục đích quảng cáo.</p>
      </LegalSection>

      <LegalSection title="4. Lưu trữ & thời gian lưu giữ">
        <p>
          Dữ liệu được lưu trữ an toàn và chỉ được giữ trong thời gian cần thiết phục vụ mục đích
          tuyển dụng hoặc theo quy định pháp luật. Khi không còn cần thiết, dữ liệu sẽ được xóa hoặc
          ẩn danh hóa.
        </p>
      </LegalSection>

      <LegalSection title="5. Bảo mật dữ liệu">
        <p>
          Chúng tôi áp dụng các biện pháp kỹ thuật và tổ chức phù hợp như mã hóa mật khẩu, kết nối
          an toàn (HTTPS), phân quyền truy cập và nhật ký kiểm toán để bảo vệ dữ liệu khỏi truy cập
          trái phép.
        </p>
      </LegalSection>

      <LegalSection title="6. Quyền của bạn">
        <ul className="list-disc space-y-1.5 pl-5">
          <li>Truy cập và xem lại thông tin cá nhân của mình.</li>
          <li>Yêu cầu chỉnh sửa thông tin không chính xác.</li>
          <li>Yêu cầu xóa tài khoản và dữ liệu liên quan.</li>
          <li>
            Rút lại sự đồng ý xử lý dữ liệu (có thể ảnh hưởng đến khả năng tiếp tục ứng tuyển).
          </li>
        </ul>
      </LegalSection>

      <LegalSection title="7. Cookie">
        <p>
          ARISP sử dụng cookie và công nghệ tương tự để duy trì phiên đăng nhập và bảo đảm an ninh.
          Bạn có thể quản lý cookie thông qua cài đặt trình duyệt, tuy nhiên một số tính năng có thể
          không hoạt động đầy đủ nếu bị vô hiệu hóa.
        </p>
      </LegalSection>

      <LegalSection title="8. Thay đổi chính sách">
        <p>
          Chính sách bảo mật có thể được cập nhật theo thời gian. Phiên bản mới nhất luôn được công
          bố tại trang này kèm ngày hiệu lực.
        </p>
      </LegalSection>

      <LegalSection title="9. Liên hệ">
        <p>
          Để thực hiện các quyền của bạn hoặc gửi thắc mắc về quyền riêng tư, vui lòng liên hệ:{' '}
          <a href="mailto:privacy@arisp.com" className="text-brand-600 hover:underline">
            privacy@arisp.com
          </a>
          . Xem thêm{' '}
          <Link to="/terms" className="text-brand-600 hover:underline">
            Điều khoản sử dụng
          </Link>
          .
        </p>
      </LegalSection>
    </LegalPageShell>
  )
}
