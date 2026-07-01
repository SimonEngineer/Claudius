import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";
import { apiClient } from "../api/client";
import { clearStoredUser, loadStoredUser, saveStoredUser, type StoredAuthUser } from "./tokenStorage";

interface AuthContextValue {
  user: StoredAuthUser | null;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<StoredAuthUser | null>(() => loadStoredUser());

  const persist = useCallback((next: StoredAuthUser) => {
    saveStoredUser(next);
    setUser(next);
  }, []);

  const login = useCallback(
    async (email: string, password: string) => {
      const { data } = await apiClient.post<{ token: string; userId: string; email: string }>("/api/auth/login", {
        email,
        password,
      });
      persist({ token: data.token, userId: data.userId, email: data.email });
    },
    [persist],
  );

  const register = useCallback(
    async (email: string, password: string) => {
      const { data } = await apiClient.post<{ token: string; userId: string; email: string }>("/api/auth/register", {
        email,
        password,
      });
      persist({ token: data.token, userId: data.userId, email: data.email });
    },
    [persist],
  );

  const logout = useCallback(() => {
    clearStoredUser();
    setUser(null);
  }, []);

  const value = useMemo(() => ({ user, login, register, logout }), [user, login, register, logout]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within an AuthProvider");
  return ctx;
}
