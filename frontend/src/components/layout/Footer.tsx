import { Link } from 'react-router-dom';
import { Brain } from 'lucide-react';

export default function Footer() {
  return (
    <footer className="bg-bg-secondary border-t border-white/10">
      <div className="max-w-7xl mx-auto px-6 py-12">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-8">
          {/* Brand */}
          <div className="col-span-1">
            <Link to="/" className="flex items-center gap-3 mb-4">
              <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
                <Brain className="w-5 h-5 text-white" />
              </div>
              <span className="text-xl font-semibold text-white">ARISP</span>
            </Link>
            <p className="text-text-secondary text-sm mb-4">
              Nền tảng tuyển dụng thông minh với AI giúp kết nối nhà tuyển dụng và ứng viên hiệu quả hơn.
            </p>
          </div>

          {/* For Candidates */}
          <div>
            <h4 className="text-white font-semibold mb-4">Dành cho ứng viên</h4>
            <ul className="space-y-2">
              <li>
                <Link to="/jobs" className="text-text-secondary hover:text-white transition-colors text-sm">
                  Tìm việc làm
                </Link>
              </li>
              <li>
                <Link to="/dang-ky-ung-vien" className="text-text-secondary hover:text-white transition-colors text-sm">
                  Tạo hồ sơ
                </Link>
              </li>
              <li>
                <Link to="/ung-vien/ket-qua" className="text-text-secondary hover:text-white transition-colors text-sm">
                  Kết quả phỏng vấn
                </Link>
              </li>
            </ul>
          </div>

          {/* For Employers */}
          <div>
            <h4 className="text-white font-semibold mb-4">Dành cho NTD</h4>
            <ul className="space-y-2">
              <li>
                <Link to="/dang-ky" className="text-text-secondary hover:text-white transition-colors text-sm">
                  Đăng ký tuyển dụng
                </Link>
              </li>
              <li>
                <Link to="/quan-ly" className="text-text-secondary hover:text-white transition-colors text-sm">
                  Quản lý tuyển dụng
                </Link>
              </li>
              <li>
                <Link to="/quan-ly/bao-cao" className="text-text-secondary hover:text-white transition-colors text-sm">
                  Báo cáo & Phân tích
                </Link>
              </li>
            </ul>
          </div>

          {/* Company */}
          <div>
            <h4 className="text-white font-semibold mb-4">Công ty</h4>
            <ul className="space-y-2">
              <li>
                <a href="#" className="text-text-secondary hover:text-white transition-colors text-sm">
                  Về chúng tôi
                </a>
              </li>
              <li>
                <a href="#" className="text-text-secondary hover:text-white transition-colors text-sm">
                  Liên hệ
                </a>
              </li>
              <li>
                <a href="#" className="text-text-secondary hover:text-white transition-colors text-sm">
                  Chính sách bảo mật
                </a>
              </li>
              <li>
                <a href="#" className="text-text-secondary hover:text-white transition-colors text-sm">
                  Điều khoản sử dụng
                </a>
              </li>
            </ul>
          </div>
        </div>

        {/* Bottom */}
        <div className="mt-12 pt-8 border-t border-white/10 text-center">
          <p className="text-text-tertiary text-sm">
            © 2026 ARISP. Tất cả quyền được bảo lưu.
          </p>
        </div>
      </div>
    </footer>
  );
}
