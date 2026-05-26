import { useEffect, useState } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import CircularProgress from '@mui/material/CircularProgress';
import { authService } from '@services/auth';
import { useAuthStore } from '@store/auth';

export default function MagicLinkCallbackPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const setAuth = useAuthStore((state) => state.setAuth);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const token = searchParams.get('token');

    if (!token) {
      setError('Invalid magic link');
      return;
    }

    authService
      .verifyMagicLink(token)
      .then((response) => {
        setAuth(response.user, response.tokens);
        navigate('/candidate/dashboard');
      })
      .catch(() => {
        setError('Failed to verify magic link. Please request a new one.');
      });
  }, [searchParams, navigate, setAuth]);

  if (error) {
    return (
      <Box sx={{ textAlign: 'center' }}>
        <Typography color="error">{error}</Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ textAlign: 'center' }}>
      <CircularProgress />
      <Typography sx={{ mt: 2 }}>Đang xác thực...</Typography>
    </Box>
  );
}
