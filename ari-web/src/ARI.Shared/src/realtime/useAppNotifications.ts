import { useEffect, useRef } from 'react'
import * as signalR from '@microsoft/signalr'
import { useQueryClient, type QueryClient } from '@tanstack/react-query'
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

/**
 * Payload của sự kiện `ReceiveDbChange` (ADR-057) — do CHÍNH database phát qua trigger + NOTIFY,
 * không phải do command chủ động push. Chỉ chứa khoá, không bao giờ chứa nội dung bản ghi.
 *
 * `op`: I = thêm, U = sửa, D = xoá, S = đổi hàng loạt, `resync` = listener vừa nối lại sau khi mất
 * kết nối DB (những NOTIFY phát trong lúc đó đã mất) nên client phải nạp lại toàn bộ.
 */
type DbChangePayload = {
  t?: string
  op?: string
  id?: string
  jobPostingId?: string
  applicationId?: string
}

/**
 * Ánh xạ "bảng nào vừa đổi" → cache nào phải tải lại. Đây là nhánh realtime bắt được MỌI đường ghi
 * (EF, sửa SQL tay, rag-service, job nền), khác với các case bên dưới vốn chỉ chạy khi command nhớ
 * gọi Publish. Hai nhánh chồng nhau là bình thường: react-query gộp các lần refetch trùng khoá.
 */
const handleDbChange = (queryClient: QueryClient, payload: DbChangePayload) => {
  if (payload?.op === 'resync') {
    queryClient.invalidateQueries()
    refreshStaffBell()
    refreshCandidateData()
    refreshOnlineTestResults()
    return
  }

  const { t, id, jobPostingId, applicationId } = payload ?? {}

  switch (t) {
    // Bảng dùng chung cho chuông của cả ứng viên lẫn nhân sự.
    case 'notifications':
      queryClient.invalidateQueries({ queryKey: ['notifications'] })
      refreshStaffBell()
      refreshCandidateData()
      break

    case 'applications':
      queryClient.invalidateQueries({ queryKey: ['applications'] })
      queryClient.invalidateQueries({ queryKey: ['application', applicationId ?? id] })
      queryClient.invalidateQueries({ queryKey: ['job', jobPostingId, 'applications'] })
      queryClient.invalidateQueries({ queryKey: ['my-jobs'] })
      queryClient.invalidateQueries({ queryKey: ['hr-dashboard'] })
      refreshCandidateData()
      break

    case 'job_postings':
      queryClient.invalidateQueries({ queryKey: ['public-jobs'] })
      queryClient.invalidateQueries({ queryKey: ['admin-jobs'] })
      queryClient.invalidateQueries({ queryKey: ['my-jobs'] })
      queryClient.invalidateQueries({ queryKey: ['hr-dashboard'] })
      if (jobPostingId ?? id) queryClient.invalidateQueries({ queryKey: ['job', jobPostingId ?? id] })
      break

    case 'interview_bookings':
      queryClient.invalidateQueries({ queryKey: ['applications'] })
      queryClient.invalidateQueries({ queryKey: ['application', applicationId] })
      queryClient.invalidateQueries({ queryKey: ['my-schedule'] })
      queryClient.invalidateQueries({ queryKey: ['candidate-schedule'] })
      // Màn Phỏng vấn của nhân sự: booking đổi là số chỗ của ca đổi theo. Payload của trigger chỉ
      // mang bộ khoá cố định (không có availability_slot_id) nên phải huỷ hiệu lực theo TIỀN TỐ.
      // Rẻ: react-query chỉ nạp lại query đang active, mà mỗi lúc chỉ có vài ca đang mở.
      queryClient.invalidateQueries({ queryKey: ['interview-jobs'] })
      queryClient.invalidateQueries({ queryKey: ['job-slots'] })
      queryClient.invalidateQueries({ queryKey: ['slot-candidates'] })
      refreshCandidateData()
      break

    // Sức chứa / khung giờ đổi ở màn cấu hình lịch → màn Phỏng vấn và modal "Dời lịch" phải thấy
    // ngay. Đây chính là lỗi người dùng báo: tăng sức chứa xong mà modal vẫn hiện số cũ.
    // Đội tuyển dụng của tin (ADR-061): người vừa được thêm/gỡ phải thấy danh sách tin của mình
    // đổi ngay — với Hiring Manager thì đó là TOÀN BỘ phạm vi dữ liệu của họ.
    case 'job_hiring_team_members':
      queryClient.invalidateQueries({
        queryKey: jobPostingId ? ['hiring-team', jobPostingId] : ['hiring-team'],
      })
      queryClient.invalidateQueries({ queryKey: ['hm-jobs'] })
      queryClient.invalidateQueries({ queryKey: ['admin-jobs'] })
      queryClient.invalidateQueries({ queryKey: ['my-jobs'] })
      break

    case 'availability_slots':
      queryClient.invalidateQueries({ queryKey: ['interview-jobs'] })
      queryClient.invalidateQueries({
        queryKey: jobPostingId ? ['job-slots', jobPostingId] : ['job-slots'],
      })
      queryClient.invalidateQueries({
        queryKey: jobPostingId ? ['schedule-slots', jobPostingId] : ['schedule-slots'],
      })
      break

    // Phiên phỏng vấn thật: huy hiệu "Đang thực hiện" trên màn Phỏng vấn trước đây không bao giờ
    // tự đổi vì bảng này chưa được định tuyến.
    case 'interview_sessions':
      queryClient.invalidateQueries({ queryKey: ['slot-candidates'] })
      queryClient.invalidateQueries({ queryKey: ['evaluations'] })
      queryClient.invalidateQueries({ queryKey: ['applications'] })
      break

    case 'online_test_submissions':
      queryClient.invalidateQueries({ queryKey: ['applications'] })
      queryClient.invalidateQueries({ queryKey: ['job', jobPostingId, 'applications'] })
      refreshOnlineTestResults()
      refreshCandidateData()
      break

    case 'evaluations':
      queryClient.invalidateQueries({ queryKey: ['evaluations'] })
      queryClient.invalidateQueries({ queryKey: ['applications'] })
      refreshStaffBell()
      break

    case 'interview_codes':
      queryClient.invalidateQueries({ queryKey: ['applications'] })
      queryClient.invalidateQueries({ queryKey: ['application', applicationId] })
      refreshCandidateData()
      break

    // Thư mời nhận việc (ADR-061). Ứng viên cũng nằm trong nhóm nhận sự kiện này, nên trang
    // "Thư mời" của họ tự cập nhật khi nhân sự gửi hoặc thu hồi — payload chỉ có khoá, KHÔNG có
    // mức lương, nên việc làm mới vẫn phải đi qua endpoint đã kiểm quyền.
    case 'offers':
      queryClient.invalidateQueries({ queryKey: ['offers'] })
      queryClient.invalidateQueries({ queryKey: ['candidate-offer'] })
      queryClient.invalidateQueries({ queryKey: ['applications'] })
      refreshCandidateData()
      refreshStaffBell()
      break

    // Lịch sử email của một hồ sơ. Cố ý KHÔNG gửi cho ứng viên (họ nhận thư thật rồi).
    case 'email_logs':
      queryClient.invalidateQueries({
        queryKey: applicationId ? ['application-emails', applicationId] : ['application-emails'],
      })
      break

    case 'account_requests':
    case 'users':
      queryClient.invalidateQueries({ queryKey: ['pending-users'] })
      queryClient.invalidateQueries({ queryKey: ['pending-account-requests'] })
      queryClient.invalidateQueries({ queryKey: ['my-account-requests'] })
      break

    // Bảng chưa cần phản ánh lên UI — im lặng bỏ qua (khác nhánh eventType bên dưới, ở đây việc
    // không map là chuyện bình thường vì trigger gắn trên toàn bộ bảng).
    default:
      break
  }
}

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
        case 'ReceiveDbChange':
          // ADR-057: sự kiện phát ra từ chính database, không phụ thuộc command có nhớ push hay không.
          handleDbChange(queryClient, payload as DbChangePayload)
          break

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

        case 'ReceiveScheduleResponse':
          // Ứng viên xác nhận/báo bận lịch phỏng vấn → cập nhật hồ sơ + chuông nhân sự.
          queryClient.invalidateQueries({ queryKey: ['applications'] })
          queryClient.invalidateQueries({ queryKey: ['application', payload?.applicationId] })
          queryClient.invalidateQueries({
            queryKey: ['job', payload?.jobPostingId, 'applications'],
          })
          queryClient.invalidateQueries({ queryKey: ['hr-dashboard'] })
          refreshStaffBell() // Chuông nhân sự: ứng viên phản hồi lịch phỏng vấn
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

        case 'JobReassigned':
          // HR Lead bàn giao tin cho recruiter khác — cả người nhận lẫn người giao đều được đẩy.
          // Trước đây không có case này nên event rơi vào `default`, chuông hai bên chỉ sáng sau khi F5.
          queryClient.invalidateQueries({ queryKey: ['my-jobs'] })
          queryClient.invalidateQueries({ queryKey: ['admin-jobs'] })
          queryClient.invalidateQueries({ queryKey: ['hr-dashboard'] })
          queryClient.invalidateQueries({ queryKey: ['notifications'] })
          if (payload?.jobId) queryClient.invalidateQueries({ queryKey: ['job', payload.jobId] })
          refreshStaffBell()
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
          queryClient.invalidateQueries({ queryKey: ['my-schedule'] }) // Trang lịch phỏng vấn (xếp/xếp lại)
          refreshCandidateData()
          break

        case 'ReceiveSystemEvent':
          // Generic system events (e.g. Schedule Slot Booked, AI Evaluation Complete)
          if (payload?.type === 'SlotBooked' || payload?.Type === 'SlotBooked') {
            // (Đã gỡ khoá ['open-slots', …]: không component nào đăng ký nó — giữ lại chỉ là lời
            //  hứa suông về việc màn hình sẽ tự làm mới.)
            queryClient.invalidateQueries({ queryKey: ['candidate-schedule'] })
            queryClient.invalidateQueries({ queryKey: ['job-slots'] })
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
