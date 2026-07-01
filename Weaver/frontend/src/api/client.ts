import axios from "axios";
import { clearStoredUser, getStoredToken } from "../auth/tokenStorage";

export const apiBaseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5080";

export const apiClient = axios.create({
  baseURL: apiBaseUrl,
});

apiClient.interceptors.request.use((config) => {
  const token = getStoredToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401 && !window.location.pathname.startsWith("/login")) {
      clearStoredUser();
      window.location.assign("/login");
    }
    return Promise.reject(error);
  },
);
