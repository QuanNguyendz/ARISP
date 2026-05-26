import Box from '@mui/material/Box';
import CircularProgress from '@mui/material/CircularProgress';

interface LoadingSpinnerProps {
  size?: number | string;
  fullScreen?: boolean;
}

export default function LoadingSpinner({ size = 40, fullScreen = false }: LoadingSpinnerProps) {
  if (fullScreen) {
    return (
      <Box
        sx={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          bgcolor: 'rgba(0,0,0,0.5)',
          zIndex: 9999,
        }}
      >
        <CircularProgress size={size} sx={{ color: 'white' }} />
      </Box>
    );
  }

  return (
    <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
      <CircularProgress size={size} />
    </Box>
  );
}
