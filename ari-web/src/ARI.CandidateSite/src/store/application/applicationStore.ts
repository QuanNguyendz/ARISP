import { create } from 'zustand';
import type { Application, ApplicationDetail } from '../../types/application';

interface ApplicationState {
  applications: Application[];
  currentApplication: ApplicationDetail | null;
  isLoading: boolean;
  error: string | null;
  setApplications: (apps: Application[]) => void;
  setCurrentApplication: (app: ApplicationDetail | null) => void;
  updateApplication: (id: string, partial: Partial<Application>) => void;
  setLoading: (loading: boolean) => void;
  setError: (error: string | null) => void;
}

export const useApplicationStore = create<ApplicationState>((set) => ({
  applications: [],
  currentApplication: null,
  isLoading: false,
  error: null,

  setApplications: (applications) => set({ applications }),

  setCurrentApplication: (app) => set({ currentApplication: app }),

  updateApplication: (id, partial) =>
    set((state) => ({
      applications: state.applications.map((app) =>
        app.id === id ? { ...app, ...partial } : app
      ),
    })),

  setLoading: (loading) => set({ isLoading: loading }),

  setError: (error) => set({ error }),
}));
