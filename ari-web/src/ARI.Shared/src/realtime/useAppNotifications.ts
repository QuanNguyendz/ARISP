import { useEffect, useRef } from 'react'
import * as signalR from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useAuthStore } from '@ari/shared/store/auth'
import {
  STAFF_NOTIF_REFRESH_EVENT,
  CANDIDATE_DATA_REFRESH_EVENT,
  STAFF_ONLINE_TEST_REFRESH_EVENT,
} from '@ari/shared/fservices/notification/notificationService'

import { API_BASE_URL } from '@ari/shared/config/constants'

/** Yêu cầu layout nhân sự tải lại chuông thông báo tức thời. */
const refreshStaffBell = () => window.dispatchEvent(new Event(STAFF_NOTIF_REFRESH_EVENT))

/** Yêu cầu các trang candidate dùng state cục bộ (ApplicationsPage...) refetch nền. */
const refreshCandidateData = () => window.dispatchEvent(new Event(CANDIDATE_DATA_REFRESH_EVENT))

/** Yêu cầu trang bảng điểm trắc nghiệm (state cục bộ) tải lại khi có ứng viên nộp bài. */
const refreshOnlineTestResults = () =>
  window.dispatchEvent(new Event(STAFF_ONLINE_TEST_REFRESH_EVENT))

// Remove trailing "/api" if present and append hub path
const HUB_URL = API_BASE_URL.replace(/\/api\/?$/, '') + '/hubs/app-notifications'

export const useAppNotifications = () => {
  const queryClient = useQueryClient()
  const token = useAuthStore((state) => state.tokens?.accessToken)
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated)
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  useEffect(() => {
    // Hub yêu cầu [Authorize] — chưa đăng nhập thì không kết nối (tránh 401 + reconnect spam
    // trên các trang public như Job Board / màn đăng nhập).
    if (!token || !isAuthenticated) return

    // Create SignalR connection.
    // accessTokenFactory đọc token MỚI NHẤT từ store mỗi lần (re)connect — sau khi apiClient
    // refresh token, reconnect sẽ dùng đúng access token mới thay vì token cũ trong closure.
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, {
        accessTokenFactory: () => useAuthStore.getState().tokens?.accessToken ?? '',
      })
      .withAutomaticReconnect()
      .build()

    connectionRef.current = connection

    // Start connection
    connection
      .start()
      .then(() => {
        console.log('[SignalR] Connected to AppNotifications hub')
      })
      .catch((err) => {
        // 401 = token cũ/hết hạn/không hợp lệ (vd phiên còn sót trong localStorage). Đây là
        // trạng thái mong đợi, KHÔNG phải lỗi thật: apiClient (REST) sở hữu luồng refresh→logout,
        // hub chỉ cần im lặng bỏ qua thay vì spam đỏ. Không tự logout ở đây (access token có thể
        // chỉ hết hạn trong khi refresh token vẫn hợp lệ).
        const isUnauthorized =
          (err as { statusCode?: number })?.statusCode === 401 || String(err).includes('401')
        if (isUnauthorized) {
          console.warn(
            '[SignalR] Bỏ qua kết nối notifications — phiên chưa đăng nhập/đã hết hạn (401).'
          )
        } else {
          console.warn('[SignalR] Không kết nối được AppNotifications hub:', err)
        }
      })

    // Listen to SystemEvents
    connection.on('ReceiveSystemEvent', (eventType: string, payload: any) => {
      console.log(`[SignalR] Received Event: ${eventType}`, payload)

      switch (eventType) {
        case 'ReceiveNewApplication':
          // Refresh applications list
          queryClient.invalidateQueries({ queryKey: ['applications'] })
          queryClient.invalidateQueries({
            queryKey: ['job', payload?.jobPostingId, 'applications'],
          })
          queryClient.invalidateQueries({ queryKey: ['my-jobs'] }) // Update applicant count on dashboards
          queryClient.invalidateQueries({ queryKey: ['hr-dashboard'] })
          refreshStaffBell() // Chuông nhân sự: ứng viên mới ứng tuyển
          break

        case 'ReceiveOnlineTestSubmitted':
          // Ứng viên hoàn thành bài thi trắc nghiệm → cập nhật bảng điểm + danh sách + chuông nhân sự.
          queryClient.invalidateQueries({ queryKey: ['applications'] })
          queryClient.invalidateQueries({
            queryKey: ['job', payload?.jobPostingId, 'applications'],
          })
          queryClient.invalidateQueries({ queryKey: ['my-jobs'] })
          queryClient.invalidateQueries({ queryKey: ['hr-dashboard'] })
          refreshOnlineTestResults() // Bảng điểm trắc nghiệm tải lại tức thời
          refreshStaffBell() // Chuông nhân sự: ứng viên nộp bài thi trắc nghiệm
          break

        case 'ReceivePublicJobUpdate':
          // A job was approved/closed/archived, refresh public job board
          queryClient.invalidateQueries({ queryKey: ['public-jobs'] })
          break

        case 'ReceiveJobPostingUpdate':
          // Job status was updated (approved, rejected, pending, closed)
          queryClient.invalidateQueries({ queryKey: ['admin-jobs'] })
          queryClient.invalidateQueries({ queryKey: ['my-jobs'] })
          queryClient.invalidateQueries({ queryKey: ['hr-dashboard'] })
          if (payload?.jobId || payload?.JobId) {
            queryClient.invalidateQueries({ queryKey: ['job', payload?.jobId || payload?.JobId] })
          }
          refreshStaffBell() // Chuông nhân sự: tin được duyệt/từ chối/chờ duyệt
          break

        case 'ReceiveApplicationStatusUpdate':
          // Refresh candidate's application details
          queryClient.invalidateQueries({ queryKey: ['applications'] })
          queryClient.invalidateQueries({ queryKey: ['application', payload?.id] })
          break

        case 'ReceiveNewAccountRequest':
        case 'ReceiveAccountRequest':
        case 'ReceiveAccountRequestUpdate':
          // Refresh the pending users list for Super Admins and HR Leader's team page
          queryClient.invalidateQueries({ queryKey: ['pending-users'] })
          queryClient.invalidateQueries({ queryKey: ['pending-account-requests'] })
          queryClient.invalidateQueries({ queryKey: ['my-account-requests'] })
          break

        case 'ReceiveUserNotification':
          // Refresh user's bell notifications
          queryClient.invalidateQueries({ queryKey: ['notifications'] })
          // Đồng thời cập nhật dữ liệu hồ sơ ứng tuyển (mã phỏng vấn mới, đổi trạng thái...):
          // react-query cho trang dùng cache + DOM event cho trang dùng state cục bộ.
          queryClient.invalidateQueries({ queryKey: ['applications'] })
          refreshCandidateData()
          break

        case 'ReceiveSystemEvent':
          // Generic system events (e.g. Schedule Slot Booked, AI Evaluation Complete)
          if (payload?.type === 'SlotBooked' || payload?.Type === 'SlotBooked') {
            queryClient.invalidateQueries({
              queryKey: ['open-slots', payload?.applicationId || payload?.ApplicationId],
            })
            queryClient.invalidateQueries({ queryKey: ['candidate-schedule'] })
            // Notification for Recruiter
            queryClient.invalidateQueries({ queryKey: ['notifications'] })
          } else if (
            payload?.type === 'AiEvaluationComplete' ||
            payload?.Type === 'AiEvaluationComplete'
          ) {
            // Notification for HR Admin
            queryClient.invalidateQueries({ queryKey: ['evaluations'] })
            queryClient.invalidateQueries({ queryKey: ['notifications'] })
            refreshStaffBell() // Chuông nhân sự: có đánh giá AI chờ xác nhận
          }
          break

        default:
          console.warn(`[SignalR] Unknown event type: ${eventType}`)
      }
    })

    return () => {
      if (connectionRef.current) {
        connectionRef.current.stop().then(() => {
          console.log('[SignalR] Disconnected from AppNotifications hub')
        })
      }
    }
  }, [token, isAuthenticated, queryClient])
}
