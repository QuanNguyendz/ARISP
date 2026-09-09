import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { DoorOpen, LogIn, Users, Clock, Briefcase } from 'lucide-react'
import { PageHeader, EmptyState, ErrorAlert, LoadingSpinner } from '@ari/shared/ui'
import { interviewService, type WaitingRoom } from '@ari/shared/fservices/interview'

/**
 * Phòng phỏng vấn thật đang mở — màn của Hiring Manager (ADR-067).
 *
 * Luật nghiệp vụ mà màn này phục vụ: <b>buổi phỏng vấn chỉ bắt đầu khi HM đã vào phòng cùng AI, và
 * ứng viên vào được là do HM cho vào.</b> Hai nút, đúng hai thao tác đó, theo đúng thứ tự — nút cho
 * vào chỉ sáng sau khi đã vào phòng, vì đó là điều kiện chứ không phải gợi ý.
 *
 * Tự làm mới mỗi 10 giây: ứng viên nhập mã ở quầy lễ tân là một sự kiện xảy ra bên ngoài trình
 * duyệt này, không có gì đẩy về. Mười giây là đủ nhanh cho một người đang ngồi chờ mở cửa, và đủ
 * chậm để không phải mở thêm một kênh realtime chỉ cho một màn hình.
 */
export default function InterviewRoomsPage() {
  const { t } = useTranslation('modules/hm/interviewRooms')
  const queryClient = useQueryClient()

  const { data, isLoading, error } = useQuery({
    queryKey: ['interview-waiting-rooms'],
    queryFn: () => interviewService.getWaitingRooms(),
    refetchInterval: 10_000,
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['interview-waiting-rooms'] })

  const join = useMutation({
    mutationFn: (sessionId: string) => interviewService.joinInterviewRoom(sessionId),
    onSuccess: refresh,
  })

  const admit = useMutation({
    mutationFn: (sessionId: string) => interviewService.admitCandidate(sessionId),
    onSuccess: refresh,
  })

  const actionError =
    (join.error as { response?: { data?: { message?: string } } })?.response?.data?.message ??
    (admit.error as { response?: { data?: { message?: string } } })?.response?.data?.message

  const rooms = data ?? []

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <div className="space-y-6">
        <PageHeader title={t('title')} description={t('description')} />

        {error && <ErrorAlert message={t('loadError')} />}
        {actionError && <ErrorAlert message={actionError} />}

        {isLoading ? (
          <LoadingSpinner message={t('loading')} />
        ) : rooms.length === 0 ? (
          <EmptyState
            icon={<DoorOpen className="h-8 w-8 text-ink-400" />}
            title={t('emptyTitle')}
            description={t('emptyDescription')}
          />
        ) : (
          <ul className="space-y-4">
            {rooms.map((room: WaitingRoom) => {
              const inRoom = !!room.hmJoinedAt
              const live = room.status === 'active'
              const busy = join.isPending || admit.isPending

              return (
                <li
                  key={room.sessionId}
                  className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card dark:border-white/10 dark:bg-white/5"
                >
                  <div className="flex flex-wrap items-start justify-between gap-4">
                    <div className="min-w-0">
                      <h3 className="font-semibold text-ink-900 dark:text-white">
                        {room.candidateName ?? t('unknownCandidate')}
                      </h3>
                      <dl className="mt-2 space-y-1 text-sm text-ink-600 dark:text-ink-400">
                        <div className="flex items-center gap-2">
                          <Briefcase className="h-4 w-4 text-brand-600 dark:text-brand-400" />
                          <span className="truncate">{room.jobTitle ?? '—'}</span>
                        </div>
                        <div className="flex items-center gap-2">
                          <Users className="h-4 w-4 text-brand-600 dark:text-brand-400" />
                          <span>{t('round', { number: room.roundNumber })}</span>
                        </div>
                        <div className="flex items-center gap-2">
                          <Clock className="h-4 w-4 text-brand-600 dark:text-brand-400" />
                          <span>
                            {t('arrivedAt', {
                              time: new Date(room.createdAt).toLocaleTimeString('vi-VN', {
                                hour: '2-digit',
                                minute: '2-digit',
                              }),
                            })}
                          </span>
                        </div>
                      </dl>
                    </div>

                    <div className="flex shrink-0 flex-col items-end gap-2">
                      <span
                        className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                          live
                            ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300'
                            : 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-300'
                        }`}
                      >
                        {live ? t('statusLive') : t('statusWaiting')}
                      </span>

                      {!live && (
                        <div className="flex gap-2">
                          <button
                            type="button"
                            disabled={busy || inRoom}
                            onClick={() => join.mutate(room.sessionId)}
                            className="inline-flex items-center gap-1.5 rounded-xl border border-ink-200 px-3 py-1.5 text-sm font-medium text-ink-700 hover:bg-ink-50 disabled:cursor-not-allowed disabled:opacity-50 dark:border-white/10 dark:text-ink-200 dark:hover:bg-white/5"
                          >
                            <LogIn className="h-4 w-4" />
                            {inRoom ? t('inRoom') : t('joinRoom')}
                          </button>
                          <button
                            type="button"
                            disabled={busy || !inRoom}
                            onClick={() => admit.mutate(room.sessionId)}
                            className="inline-flex items-center gap-1.5 rounded-xl bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:cursor-not-allowed disabled:opacity-50"
                          >
                            <DoorOpen className="h-4 w-4" /> {t('admit')}
                          </button>
                        </div>
                      )}
                    </div>
                  </div>

                  {/* Nói rõ vì sao nút "Cho vào" đang tắt — người dùng không phải đoán thứ tự. */}
                  {!live && !inRoom && (
                    <p className="mt-3 border-t border-ink-100 pt-3 text-xs text-ink-500 dark:border-white/10 dark:text-ink-400">
                      {t('joinFirstHint')}
                    </p>
                  )}
                </li>
              )
            })}
          </ul>
        )}
      </div>
    </div>
  )
}
