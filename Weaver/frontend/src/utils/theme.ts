const STORAGE_KEY = "weaver-theme";

export type Theme = "dark" | "light";

export function getTheme(): Theme {
  return (localStorage.getItem(STORAGE_KEY) as Theme) ?? "dark";
}

export function applyTheme(theme: Theme) {
  document.documentElement.dataset.theme = theme;
  localStorage.setItem(STORAGE_KEY, theme);
}

/** Call once at startup so the stored preference applies before first paint. */
export function initTheme() {
  document.documentElement.dataset.theme = getTheme();
}
