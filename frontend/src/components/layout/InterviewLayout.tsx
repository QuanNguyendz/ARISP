import { Outlet } from 'react-router-dom';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';

export default function InterviewLayout() {
  return (
    <Box
      sx={{
        minHeight: '100vh',
        bgcolor: 'grey.900',
        color: 'white',
        display: 'flex',
        flexDirection: 'column',
        '& .MuiBox-root': { flex: 1 },
      }}
    >
      <Box sx={{ p: 2, borderBottom: '1px solid rgba(255,255,255,0.1)', flexShrink: 0 }}>
        <Typography variant="h6" sx={{ fontWeight: 'bold', color: 'primary.light' }}>
          ARISP - Interview Room
        </Typography>
      </Box>
      <Box sx={{ flex: 1, display: 'flex', alignItems: 'stretch', minHeight: 0 }}>
        <Outlet />
      </Box>
    </Box>
  );
}
