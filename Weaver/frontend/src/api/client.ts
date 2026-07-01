import axios from "axios";

export const apiBaseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5080";

export const apiClient = axios.create({
  baseURL: apiBaseUrl,
});
