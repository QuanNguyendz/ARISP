import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';

export default function NotFoundPage() {
  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        textAlign: 'center',
        p: 3,
      }}
    >
      <Typography variant="h1" sx={{ fontSize: { xs: '4rem', md: '8rem' }, fontWeight: 'bold', color: 'grey.300' }}>
        404
      </Typography>
      <Typography variant="h4" sx={{ mb: 4 }}>
        Trang không tìm thấy
      </Typography>
      <Button variant="contained" href="/">
        Quay về trang chủ
      </Button>
    </Box>
  );
}
