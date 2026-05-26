import { useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { LoadingSpinner, ErrorAlert } from '@components/common';
import { useInterviewRoom } from '@hooks/interview';

export default function InterviewRoomPage() {
  const { sessionId } = useParams<{ sessionId: string }>();

  if (!sessionId) {
    return <ErrorAlert message="Invalid session" />;
  }

  const { room, connectionStatus, error } = useInterviewRoom(sessionId);

  if (connectionStatus === 'connecting') {
    return (
      <Box sx={{ textAlign: 'center' }}>
        <LoadingSpinner size={60} />
        <Typography variant="h6" sx={{ mt: 2 }}>
          Đang kết nối với phòng phỏng vấn...
        </Typography>
      </Box>
    );
  }

  if (connectionStatus === 'error' || error) {
    return <ErrorAlert message={error || 'Failed to connect'} />;
  }

  return (
    <Box
      sx={{
        width: '100%',
        maxWidth: 1200,
        display: 'flex',
        flexDirection: 'column',
        gap: 3,
      }}
    >
      <Typography variant="h5" sx={{ textAlign: 'center' }}>
        Phòng phỏng vấn AI - Round {room?.currentRound}
      </Typography>
      <Box
        sx={{
          aspectRatio: '16/9',
          bgcolor: 'grey.800',
          borderRadius: 2,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <Typography color="text.secondary">Avatar AI sẽ hiển thị ở đây</Typography>
      </Box>
      <Box sx={{ textAlign: 'center' }}>
        <Typography variant="h6">
          Trạng thái: {room?.status === 'in_progress' ? 'Đang phỏng vấn' : room?.status}
        </Typography>
      </Box>
    </Box>
  );
}
