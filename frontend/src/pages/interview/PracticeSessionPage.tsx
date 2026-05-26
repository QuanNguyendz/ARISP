import { useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { ErrorAlert } from '@components/common';

export default function PracticeSessionPage() {
  const { applicationId } = useParams<{ applicationId: string }>();

  if (!applicationId) {
    return <ErrorAlert message="Invalid application" />;
  }

  return (
    <Box sx={{ textAlign: 'center' }}>
      <Typography variant="h5">Phỏng vấn thử</Typography>
      <Typography color="text.secondary" sx={{ mt: 2 }}>
        Application ID: {applicationId}
      </Typography>
    </Box>
  );
}
