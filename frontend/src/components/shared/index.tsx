import { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Plus, Bell } from 'lucide-react';
import { motion } from 'framer-motion';

interface ActionButton {
  label: string;
  href?: string;
  onClick?: () => void;
  icon?: ReactNode;
  variant?: 'primary' | 'secondary';
}

interface PageHeaderProps {
  title: string;
  description?: string;
  actions?: ActionButton[];
  badge?: {
    text: string;
    color?: string;
  };
}

export function PageHeader({ title, description, actions, badge }: PageHeaderProps) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      className="flex items-center justify-between mb-8"
    >
      <div>
        <div className="flex items-center gap-3 mb-1">
          <h1 className="text-2xl font-semibold text-white">{title}</h1>
          {badge && (
            <span className={`px-2.5 py-0.5 rounded-full text-xs font-medium ${badge.color || 'bg-accent-primary/20 text-accent-primary'}`}>
              {badge.text}
            </span>
          )}
        </div>
        {description && <p className="text-sm text-white/40">{description}</p>}
      </div>
      {actions && actions.length > 0 && (
        <div className="flex items-center gap-3">
          {actions.map((action, index) => (
            action.href ? (
              <Link
                key={index}
                to={action.href}
                className={`flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium transition-opacity ${
                  action.variant === 'secondary'
                    ? 'bg-white/5 border border-white/10 text-white/70 hover:text-white hover:bg-white/10'
                    : 'bg-gradient-to-r from-accent-primary to-violet text-white hover:opacity-90'
                }`}
              >
                {action.icon || <Plus className="w-4 h-4" />}
                {action.label}
              </Link>
            ) : (
              <button
                key={index}
                onClick={action.onClick}
                className={`flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium transition-opacity ${
                  action.variant === 'secondary'
                    ? 'bg-white/5 border border-white/10 text-white/70 hover:text-white hover:bg-white/10'
                    : 'bg-gradient-to-r from-accent-primary to-violet text-white hover:opacity-90'
                }`}
              >
                {action.icon || <Plus className="w-4 h-4" />}
                {action.label}
              </button>
            )
          ))}
        </div>
      )}
    </motion.div>
  );
}

interface StatsCardProps {
  label: string;
  value: string | number;
  change?: string;
  color?: string;
}

export function StatsCard({ label, value, change, color = 'text-blue-400' }: StatsCardProps) {
  return (
    <div className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-5 overflow-hidden group hover:bg-white/[0.05] transition-colors">
      <div className="flex items-center justify-between mb-3">
        <span className={`text-sm font-medium ${color}`}>{label}</span>
        {change && <span className="text-xs text-emerald-400 font-medium">{change}</span>}
      </div>
      <p className={`text-2xl font-semibold ${color}`}>{value}</p>
    </div>
  );
}

interface StatsGridProps {
  stats: StatsCardProps[];
}

export function StatsGrid({ stats }: StatsGridProps) {
  return (
    <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
      {stats.map((stat, index) => (
        <motion.div
          key={stat.label}
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: index * 0.05 }}
        >
          <StatsCard {...stat} />
        </motion.div>
      ))}
    </div>
  );
}

interface EmptyStateProps {
  icon: ReactNode;
  title: string;
  description: string;
  action?: {
    label: string;
    href?: string;
    onClick?: () => void;
  };
}

export function EmptyState({ icon, title, description, action }: EmptyStateProps) {
  return (
    <div className="bg-white/[0.02] border border-white/[0.05] rounded-2xl p-12 text-center">
      <div className="w-16 h-16 rounded-2xl bg-white/5 flex items-center justify-center mx-auto mb-4">
        {icon}
      </div>
      <h3 className="text-lg font-semibold text-white mb-2">{title}</h3>
      <p className="text-text-secondary text-sm mb-6 max-w-md mx-auto">{description}</p>
      {action && (
        action.href ? (
          <Link
            to={action.href}
            className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium hover:opacity-90 transition-opacity"
          >
            <Plus className="w-4 h-4" />
            {action.label}
          </Link>
        ) : (
          <button
            onClick={action.onClick}
            className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium hover:opacity-90 transition-opacity"
          >
            <Plus className="w-4 h-4" />
            {action.label}
          </button>
        )
      )}
    </div>
  );
}

interface LoadingSpinnerProps {
  message?: string;
}

export function LoadingSpinner({ message = 'Đang tải dữ liệu...' }: LoadingSpinnerProps) {
  return (
    <div className="flex flex-col items-center justify-center py-20 gap-3">
      <div className="w-10 h-10 rounded-full border-2 border-accent-primary/30 border-t-accent-primary animate-spin" />
      <p className="text-sm text-white/40">{message}</p>
    </div>
  );
}

interface ErrorAlertProps {
  message: string;
  onDismiss?: () => void;
}

export function ErrorAlert({ message, onDismiss }: ErrorAlertProps) {
  return (
    <div className="p-4 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 text-sm mb-6 flex items-start justify-between gap-3">
      <span>{message}</span>
      {onDismiss && (
        <button
          type="button"
          onClick={onDismiss}
          className="text-red-300/70 hover:text-red-200 transition-colors"
        >
          Đóng
        </button>
      )}
    </div>
  );
}

interface NoticeAlertProps {
  message: string;
  onDismiss?: () => void;
}

export function NoticeAlert({ message, onDismiss }: NoticeAlertProps) {
  return (
    <div className="p-4 rounded-xl bg-amber-500/10 border border-amber-500/20 text-amber-300 text-sm mb-6 flex items-start justify-between gap-3">
      <span>{message}</span>
      {onDismiss && (
        <button
          type="button"
          onClick={onDismiss}
          className="text-amber-200/70 hover:text-amber-100 transition-colors"
        >
          Đóng
        </button>
      )}
    </div>
  );
}

export function PlusIcon(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width="24"
      height="24"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <path d="M5 12h14" />
      <path d="M12 5v14" />
    </svg>
  );
}
