import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Building2, Plus, Loader2, Check, X, Users } from 'lucide-react'
import { PageHeader, EmptyState, ErrorAlert } from '@ari/shared/ui'
import {
  departmentService,
  type Department,
  type DepartmentInput,
} from '@ari/shared/fservices/department'

/**
 * Quản lý đội/bộ phận (ADR-065) — màn riêng của Super Admin.
 *
 * Vì sao cần: phòng ban trước đây là chuỗi tự do ở khắp nơi, và nhân viên còn tự sửa được phòng ban
 * của chính mình — nên Hiring Manager đội A lập được phiếu ghi đội B. Có danh sách chuẩn thì đội của
 * mỗi tài khoản mới xác định được, và ô "Đội" trên phiếu mới khoá cứng có nghĩa.
 *
 * Đội giải thể thì **tắt**, không xoá: phiếu và tài khoản cũ vẫn phải tra được tên.
 */

const inputCls =
  'w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2.5 text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400 focus:border-brand-400'
const labelCls = 'mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300'

const emptyInput = (): DepartmentInput => ({ name: '', code: '', description: '', isActive: true })

export default function DepartmentsPage() {
  const { t } = useTranslation('modules/super-admin/departments')

  const [items, setItems] = useState<Department[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [editing, setEditing] = useState<Department | null>(null)
  const [creating, setCreating] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      setItems(await departmentService.list())
    } catch (e: any) {
      setError(e?.response?.data?.message || t('errors.loadFailed'))
    } finally {
      setLoading(false)
    }
  }, [t])

  useEffect(() => {
    void load()
  }, [load])

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <PageHeader
        title={t('title')}
        description={t('description')}
        actions={[{ label: t('create'), icon: <Plus className="h-4 w-4" />, onClick: () => setCreating(true) }]}
      />

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      {loading ? (
        <div className="flex items-center gap-2 py-16 text-sm text-ink-500">
          <Loader2 className="h-4 w-4 animate-spin" /> {t('loading')}
        </div>
      ) : items.length === 0 ? (
        <EmptyState
          icon={<Building2 className="h-8 w-8 text-ink-400" />}
          title={t('empty.title')}
          description={t('empty.description')}
          action={{ label: t('create'), onClick: () => setCreating(true) }}
        />
      ) : (
        <div className="space-y-3">
          {items.map((d) => (
            <button
              key={d.id}
              onClick={() => setEditing(d)}
              className={`w-full rounded-2xl border bg-white p-4 text-left shadow-card transition hover:border-brand-300 dark:bg-white/5 ${
                d.isActive ? 'border-ink-200 dark:border-white/10' : 'border-dashed border-ink-300 opacity-70 dark:border-white/20'
              }`}
            >
              <div className="flex flex-wrap items-center gap-2">
                <h3 className="text-base font-semibold text-ink-900 dark:text-white">{d.name}</h3>
                {d.code && (
                  <code className="rounded bg-ink-100 px-2 py-0.5 text-xs text-ink-500 dark:bg-white/10 dark:text-ink-400">
                    {d.code}
                  </code>
                )}
                {!d.isActive && (
                  <span className="rounded-full bg-ink-100 px-2.5 py-1 text-xs font-medium text-ink-600 dark:bg-white/10 dark:text-ink-300">
                    {t('inactive')}
                  </span>
                )}
                <span className="ml-auto inline-flex items-center gap-1.5 whitespace-nowrap text-xs text-ink-400">
                  <Users className="h-3 w-3" /> {t('memberCount', { count: d.memberCount })}
                </span>
              </div>
              {d.description && (
                <p className="mt-1 text-sm text-ink-500 dark:text-ink-400">{d.description}</p>
              )}
            </button>
          ))}
        </div>
      )}

      {(creating || editing) && (
        <DepartmentModal
          department={editing}
          onClose={() => {
            setCreating(false)
            setEditing(null)
          }}
          onSaved={() => {
            setCreating(false)
            setEditing(null)
            void load()
          }}
        />
      )}
    </div>
  )
}

function DepartmentModal({
  department,
  onClose,
  onSaved,
}: {
  department: Department | null
  onClose: () => void
  onSaved: () => void
}) {
  const { t } = useTranslation('modules/super-admin/departments')
  const [form, setForm] = useState<DepartmentInput>(
    department
      ? {
          name: department.name,
          code: department.code ?? '',
          description: department.description ?? '',
          isActive: department.isActive,
        }
      : emptyInput()
  )
  const [submitting, setSubmitting] = useState(false)
  const [err, setErr] = useState('')

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setErr('')
    setSubmitting(true)
    try {
      const payload = { ...form, name: form.name.trim() }
      if (department) await departmentService.update(department.id, payload)
      else await departmentService.create(payload)
      onSaved()
    } catch (e: any) {
      setErr(e?.response?.data?.message || t('errors.saveFailed'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative w-full max-w-md rounded-2xl border border-ink-200 bg-white p-6 shadow-xl dark:border-white/10 dark:bg-ink-900">
        <div className="mb-5 flex items-start justify-between">
          <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
            {department ? t('modal.editTitle') : t('modal.createTitle')}
          </h3>
          <button
            onClick={onClose}
            className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {err && <ErrorAlert message={err} onDismiss={() => setErr('')} />}

        <form onSubmit={submit} className="space-y-4">
          <div>
            <label className={labelCls}>{t('modal.name')} *</label>
            <input
              required
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
              placeholder={t('modal.namePlaceholder')}
              className={inputCls}
            />
          </div>
          <div>
            <label className={labelCls}>{t('modal.code')}</label>
            <input
              value={form.code ?? ''}
              onChange={(e) => setForm({ ...form, code: e.target.value })}
              placeholder={t('modal.codePlaceholder')}
              className={inputCls}
            />
          </div>
          <div>
            <label className={labelCls}>{t('modal.description')}</label>
            <input
              value={form.description ?? ''}
              onChange={(e) => setForm({ ...form, description: e.target.value })}
              className={inputCls}
            />
          </div>

          <label className="flex items-start gap-2 rounded-xl border border-ink-200 p-3 dark:border-white/10">
            <input
              type="checkbox"
              checked={form.isActive}
              onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
              className="mt-0.5 h-4 w-4 accent-brand-600"
            />
            <span className="text-sm">
              <span className="font-medium text-ink-800 dark:text-ink-100">{t('modal.active')}</span>
              {/* Nói rõ vì sao không có nút xoá: đội giải thể vẫn phải tra được tên cho phiếu cũ. */}
              <span className="mt-0.5 block text-xs text-ink-400">{t('modal.activeHint')}</span>
            </span>
          </label>

          <div className="flex gap-3 pt-1">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 rounded-xl border border-ink-200 px-4 py-2.5 text-sm font-medium text-ink-700 dark:border-white/10 dark:text-ink-200"
            >
              {t('modal.cancel')}
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="flex flex-1 items-center justify-center gap-2 rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-medium text-white disabled:opacity-60"
            >
              {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
              {t('modal.submit')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
