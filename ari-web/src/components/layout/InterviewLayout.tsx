import { Outlet } from 'react-router-dom'

export default function InterviewLayout() {
  return (
    <div className="flex min-h-screen flex-col bg-gray-900 text-white">
      <div className="flex-shrink-0 border-b border-white/10 p-4">
        <h1 className="text-lg font-bold text-indigo-400">ARISP - Interview Room</h1>
      </div>
      <div className="flex min-h-0 flex-1 items-stretch">
        <Outlet />
      </div>
    </div>
  )
}
