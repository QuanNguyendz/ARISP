import { useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { useAuthStore } from '../store/auth/authStore';

import { API_BASE_URL } from '@config/constants';

// Remove trailing "/api" if present and append hub path
const HUB_URL = API_BASE_URL.replace(/\/api\/?$/, '') + '/hubs/app-notifications';

export const useAppNotifications = () => {
  const queryClient = useQueryClient();
  const token = useAuthStore(state => state.tokens?.accessToken);
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  useEffect(() => {
    // Create SignalR connection
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, token ? { accessTokenFactory: () => token } : {})
      .withAutomaticReconnect()
      .build();

    connectionRef.current = connection;

    // Start connection
    connection.start()
      .then(() => {
        console.log('[SignalR] Connected to AppNotifications hub');
      })
      .catch(err => {
        console.error('[SignalR] Connection error:', err);
      });

    // Listen to SystemEvents
    connection.on('ReceiveSystemEvent', (eventType: string, payload: any) => {
      console.log(`[SignalR] Received Event: ${eventType}`, payload);

      switch (eventType) {
        case 'ReceiveNewApplication':
          // Refresh applications list
          queryClient.invalidateQueries({ queryKey: ['applications'] });
          queryClient.invalidateQueries({ queryKey: ['job', payload?.jobPostingId, 'applications'] });
          queryClient.invalidateQueries({ queryKey: ['my-jobs'] }); // Update applicant count on dashboards
          queryClient.invalidateQueries({ queryKey: ['hr-dashboard'] });
          break;
          
        case 'ReceivePublicJobUpdate':
          // A job was approved/closed/archived, refresh public job board
          queryClient.invalidateQueries({ queryKey: ['public-jobs'] });
          break;

        case 'ReceiveJobPostingUpdate':
          // Job status was updated (approved, rejected, pending, closed)
          queryClient.invalidateQueries({ queryKey: ['admin-jobs'] });
          queryClient.invalidateQueries({ queryKey: ['my-jobs'] });
          queryClient.invalidateQueries({ queryKey: ['hr-dashboard'] });
          if (payload?.jobId || payload?.JobId) {
            queryClient.invalidateQueries({ queryKey: ['job', payload?.jobId || payload?.JobId] });
          }
          break;
          
        case 'ReceiveApplicationStatusUpdate':
          // Refresh candidate's application details
          queryClient.invalidateQueries({ queryKey: ['applications'] });
          queryClient.invalidateQueries({ queryKey: ['application', payload?.id] });
          break;

        case 'ReceiveNewAccountRequest':
        case 'ReceiveAccountRequest':
        case 'ReceiveAccountRequestUpdate':
          // Refresh the pending users list for Super Admins and HR Leader's team page
          queryClient.invalidateQueries({ queryKey: ['pending-users'] });
          queryClient.invalidateQueries({ queryKey: ['pending-account-requests'] });
          queryClient.invalidateQueries({ queryKey: ['my-account-requests'] });
          break;

        case 'ReceiveUserNotification':
          // Refresh user's bell notifications
          queryClient.invalidateQueries({ queryKey: ['notifications'] });
          break;

        case 'ReceiveSystemEvent':
          // Generic system events (e.g. Schedule Slot Booked, AI Evaluation Complete)
          if (payload?.type === 'SlotBooked' || payload?.Type === 'SlotBooked') {
            queryClient.invalidateQueries({ queryKey: ['open-slots', payload?.applicationId || payload?.ApplicationId] });
            queryClient.invalidateQueries({ queryKey: ['candidate-schedule'] });
            // Notification for Recruiter
            queryClient.invalidateQueries({ queryKey: ['notifications'] }); 
          }
          else if (payload?.type === 'AiEvaluationComplete' || payload?.Type === 'AiEvaluationComplete') {
             // Notification for HR Admin
             queryClient.invalidateQueries({ queryKey: ['evaluations'] });
             queryClient.invalidateQueries({ queryKey: ['notifications'] }); 
          }
          break;
          
        default:
          console.warn(`[SignalR] Unknown event type: ${eventType}`);
      }
    });

    return () => {
      if (connectionRef.current) {
        connectionRef.current.stop().then(() => {
            console.log('[SignalR] Disconnected from AppNotifications hub');
        });
      }
    };
  }, [token, queryClient]);
};
