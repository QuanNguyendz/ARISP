import { useParams } from 'react-router-dom'
import { ErrorAlert } from '@components/common'

export default function PracticeSessionPage() {
  const { applicationId } = useParams<{ applicationId: string }>()

  if (!applicationId) {
    return <ErrorAlert message="Invalid application" />
  }

  return (
    <div className="text-center">
      <h2 className="text-xl font-semibold">Phỏng vấn thử</h2>
      <p className="mt-2 text-gray-500">Application ID: {applicationId}</p>
    </div>
  )
}
