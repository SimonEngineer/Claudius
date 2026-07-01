export interface StoredAuthUser {
  token: string;
  userId: string;
  email: string;
}

const STORAGE_KEY = "weaver.auth";

export function loadStoredUser(): StoredAuthUser | null {
  const raw = localStorage.getItem(STORAGE_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as StoredAuthUser;
  } catch {
    return null;
  }
}

export function saveStoredUser(user: StoredAuthUser) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(user));
}

export function clearStoredUser() {
  localStorage.removeItem(STORAGE_KEY);
}

export function getStoredToken(): string | null {
  return loadStoredUser()?.token ?? null;
}
