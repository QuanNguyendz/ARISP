import { Link } from 'react-router-dom';
import { Brain, Mail, Phone, MapPin } from 'lucide-react';

const footerLinks = {
  việc_làm: [
    { label: 'Tìm việc', href: '/jobs' },
    { label: 'Việc làm theo ngành', href: '/jobs' },
    { label: 'Việc làm theo địa điểm', href: '/jobs' },
    { label: 'Việc làm Remote', href: '/jobs' },
  ],
  công_ty: [
    { label: 'Danh sách công ty', href: '/jobs' },
    { label: 'Review công ty', href: '/jobs' },
    { label: 'Salary Guide', href: '/jobs' },
  ],
  ứng_viên: [
    { label: 'Tạo hồ sơ', href: '/auth/candidate-register' },
    { label: 'Hồ sơ của tôi', href: '/candidate/dashboard' },
    { label: 'Việc đã ứng tuyển', href: '/candidate/applications' },
    { label: 'Phỏng vấn', href: '/candidate/interview' },
  ],
  về_arisp: [
    { label: 'Giới thiệu', href: '/nha-tuyen-dung' },
    { label: 'Blog', href: '/jobs' },
    { label: 'Liên hệ', href: '/jobs' },
    { label: 'Chính sách', href: '/jobs' },
  ],
};

export default function CandidateFooter() {
  return (
    <footer className="bg-bg-secondary border-t border-white/5">
      <div className="max-w-7xl mx-auto px-6 py-12">
        <div className="grid grid-cols-2 md:grid-cols-5 gap-8">
          {/* Brand */}
          <div className="col-span-2 md:col-span-1">
            <Link to="/" className="flex items-center gap-2.5 mb-4">
              <div className="w-9 h-9 rounded-lg bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
                <Brain className="w-5 h-5 text-white" />
              </div>
              <div>
                <span className="text-base font-semibold text-white">ARISP</span>
                <span className="block text-xs text-text-tertiary">Tìm việc</span>
              </div>
            </Link>
            <p className="text-sm text-text-secondary mb-4">
              Nền tảng kết nối ứng viên với nhà tuyển dụng hàng đầu Việt Nam.
            </p>
            <div className="flex items-center gap-3">
              <span className="w-9 h-9 rounded-lg bg-white/5 hover:bg-white/10 flex items-center justify-center text-text-secondary hover:text-white transition-colors cursor-pointer">
                f
              </span>
              <span className="w-9 h-9 rounded-lg bg-white/5 hover:bg-white/10 flex items-center justify-center text-text-secondary hover:text-white transition-colors cursor-pointer">
                in
              </span>
              <span className="w-9 h-9 rounded-lg bg-white/5 hover:bg-white/10 flex items-center justify-center text-text-secondary hover:text-white transition-colors cursor-pointer">
                X
              </span>
            </div>
          </div>

          {/* Links */}
          <div>
            <h4 className="text-sm font-semibold text-white mb-4">Việc làm</h4>
            <ul className="space-y-2">
              {footerLinks.việc_làm.map((link) => (
                <li key={link.label}>
                  <Link to={link.href} className="text-sm text-text-secondary hover:text-white transition-colors">
                    {link.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>

          <div>
            <h4 className="text-sm font-semibold text-white mb-4">Công ty</h4>
            <ul className="space-y-2">
              {footerLinks.công_ty.map((link) => (
                <li key={link.label}>
                  <Link to={link.href} className="text-sm text-text-secondary hover:text-white transition-colors">
                    {link.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>

          <div>
            <h4 className="text-sm font-semibold text-white mb-4">Ứng viên</h4>
            <ul className="space-y-2">
              {footerLinks.ứng_viên.map((link) => (
                <li key={link.label}>
                  <Link to={link.href} className="text-sm text-text-secondary hover:text-white transition-colors">
                    {link.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>

          <div>
            <h4 className="text-sm font-semibold text-white mb-4">Liên hệ</h4>
            <ul className="space-y-3">
              <li className="flex items-center gap-2 text-sm text-text-secondary">
                <MapPin className="w-4 h-4" />
                TP. Hồ Chí Minh, Việt Nam
              </li>
              <li className="flex items-center gap-2 text-sm text-text-secondary">
                <Mail className="w-4 h-4" />
                contact@arisp.vn
              </li>
              <li className="flex items-center gap-2 text-sm text-text-secondary">
                <Phone className="w-4 h-4" />
                1900 1234
              </li>
            </ul>
          </div>
        </div>

        {/* Bottom */}
        <div className="mt-12 pt-6 border-t border-white/5 flex flex-col md:flex-row items-center justify-between gap-4">
          <p className="text-sm text-text-tertiary">
            © 2026 ARISP. Tất cả quyền được bảo lưu.
          </p>
          <div className="flex items-center gap-6">
            <Link to="/jobs" className="text-sm text-text-tertiary hover:text-white transition-colors">
              Chính sách bảo mật
            </Link>
            <Link to="/jobs" className="text-sm text-text-tertiary hover:text-white transition-colors">
              Điều khoản sử dụng
            </Link>
          </div>
        </div>
      </div>
    </footer>
  );
}
