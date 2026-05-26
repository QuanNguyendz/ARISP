import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import { interviewService } from '@services/interview';

export default function KioskPage() {
  const [code, setCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const result = await interviewService.validateInterviewCode(code.toUpperCase());
      if (result.valid && result.sessionId) {
        navigate(`/interview/room/${result.sessionId}`);
      } else {
        setError('Mã không hợp lệ hoặc đã hết hạn');
      }
    } catch {
      setError('Đã xảy ra lỗi. Vui lòng thử lại.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box
      sx={{
        width: '100%',
        maxWidth: 500,
        p: 4,
        bgcolor: 'grey.900',
        borderRadius: 2,
        textAlign: 'center',
      }}
    >
      <Typography variant="h4" sx={{ mb: 4, color: 'primary.light', fontWeight: 'bold' }}>
        ARISP Kiosk
      </Typography>
      <Typography variant="body1" sx={{ mb: 4, color: 'grey.300' }}>
        Vui lòng nhập mã phỏng vấn để bắt đầu
      </Typography>

      {error && <Alert severity="error" sx={{ mb: 3 }}>{error}</Alert>}

      <Box component="form" onSubmit={handleSubmit}>
        <TextField
          value={code}
          onChange={(e) => setCode(e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, ''))}
          placeholder="XXXXXX"
          fullWidth
          inputProps={{
            maxLength: 8,
            style: {
              textAlign: 'center',
              fontSize: '2rem',
              letterSpacing: '0.5rem',
              fontWeight: 'bold',
            },
          }}
          sx={{ mb: 3 }}
        />
        <Button
          type="submit"
          variant="contained"
          color="primary"
          size="large"
          fullWidth
          disabled={loading || code.length < 6}
        >
          {loading ? 'Đang xác thực...' : 'Bắt đầu phỏng vấn'}
        </Button>
      </Box>
    </Box>
  );
}
