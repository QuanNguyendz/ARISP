import { createContext, useContext, useState, useEffect, type ReactNode } from 'react';

export type UserRole = 'candidate' | 'employer' | null;

interface User {
  id: string;
  name: string;
  email: string;
  role: 'candidate' | 'employer';
  avatar?: string;
}

interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  login: (email: string, password: string, role: 'candidate' | 'employer') => Promise<boolean>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);

  useEffect(() => {
    const savedUser = localStorage.getItem('arisp_user');
    if (savedUser) {
      try {
        setUser(JSON.parse(savedUser));
      } catch {
        localStorage.removeItem('arisp_user');
      }
    }
  }, []);

  const login = async (email: string, _password: string, role: 'candidate' | 'employer'): Promise<boolean> => {
    if (!email || !_password) return false;
    await new Promise(resolve => setTimeout(resolve, 1000));

    const newUser: User = {
      id: '1',
      name: role === 'candidate' ? 'Nguyễn Văn An' : 'HR Manager',
      email: email,
      role: role,
    };

    setUser(newUser);
    localStorage.setItem('arisp_user', JSON.stringify(newUser));
    return true;
  };

  const logout = () => {
    setUser(null);
    localStorage.removeItem('arisp_user');
  };

  return (
    <AuthContext.Provider value={{ user, isAuthenticated: !!user, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
