import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import { SxProps, Theme } from '@mui/material/styles';

interface LoadingButtonProps {
  children: React.ReactNode;
  loading?: boolean;
  disabled?: boolean;
  variant?: 'text' | 'outlined' | 'contained';
  color?: 'primary' | 'secondary' | 'error' | 'warning' | 'info' | 'success';
  size?: 'small' | 'medium' | 'large';
  type?: 'button' | 'submit' | 'reset';
  onClick?: () => void;
  fullWidth?: boolean;
  sx?: SxProps<Theme>;
  className?: string;
}

export default function LoadingButton({
  children,
  loading = false,
  disabled = false,
  variant = 'contained',
  color = 'primary',
  size = 'medium',
  type = 'button',
  onClick,
  fullWidth = false,
  sx,
  className,
}: LoadingButtonProps) {
  return (
    <Button
      type={type}
      variant={variant}
      color={color}
      size={size}
      disabled={disabled || loading}
      onClick={onClick}
      fullWidth={fullWidth}
      sx={sx}
      className={className}
      startIcon={loading ? <CircularProgress size={20} color="inherit" /> : null}
    >
      {children}
    </Button>
  );
}
