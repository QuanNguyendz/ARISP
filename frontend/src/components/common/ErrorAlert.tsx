import Alert from '@mui/material/Alert';
import AlertTitle from '@mui/material/AlertTitle';
import Collapse from '@mui/material/Collapse';
import IconButton from '@mui/material/IconButton';
import CloseIcon from '@mui/icons-material/Close';
import { useState } from 'react';

interface ErrorAlertProps {
  message: string;
  title?: string;
  severity?: 'error' | 'warning' | 'info' | 'success';
}

export default function ErrorAlert({ message, title, severity = 'error' }: ErrorAlertProps) {
  const [open, setOpen] = useState(true);

  return (
    <Collapse in={open}>
      <Alert
        severity={severity}
        action={
          <IconButton aria-label="close" color="inherit" size="small" onClick={() => setOpen(false)}>
            <CloseIcon fontSize="inherit" />
          </IconButton>
        }
      >
        {title && <AlertTitle>{title}</AlertTitle>}
        {message}
      </Alert>
    </Collapse>
  );
}
