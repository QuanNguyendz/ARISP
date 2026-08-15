import { useQuery, useQueryClient } from '@tanstack/react-query'
import { profileService } from '@ari/shared/fservices/profile/profileService'
import { resolveAssetUrl, ROLES } from '@ari/shared/config/constants'
import { useAuthStore } from '@ari/shared/store/auth'

export const CANDIDATE_AVATAR_QUERY_KEY = ['candidate-avatar'] as const

/**
 * Ảnh đại diện cho header. Phải lấy từ server chứ không đọc từ `authStore` đã persist:
 * bản dựng production để file trên R2 nên URL là presigned và **hết hạn** sau ít phút
 * (`Storage:UrlExpiryMinutes`) — URL nằm lì trong localStorage sẽ thành ảnh vỡ. Store chỉ dùng làm
 * ảnh tạm trong lúc query chưa về, và được cập nhật ngay sau khi tải ảnh mới lên.
 */
export function useCandidateAvatar(): string {
  const { user, isAuthenticated } = useAuthStore()
  const isCandidate = (user?.role || '').toLowerCase() === ROLES.Candidate.toLowerCase()

  const { data } = useQuery({
    queryKey: CANDIDATE_AVATAR_QUERY_KEY,
    queryFn: () => profileService.getProfile(),
    enabled: !!isAuthenticated && isCandidate,
    staleTime: 5 * 60 * 1000,
  })

  // Query đã về thì nó là nguồn sự thật — kể cả khi ứng viên vừa gỡ ảnh (avatarUrl = null).
  return resolveAssetUrl((data ? data.avatarUrl : user?.avatarUrl) || '')
}

/** Buộc header lấy lại ảnh sau khi ứng viên đổi ảnh đại diện. */
export function useRefreshCandidateAvatar(): () => void {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: CANDIDATE_AVATAR_QUERY_KEY })
  }
}
