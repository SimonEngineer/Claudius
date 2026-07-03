import { useEffect } from "react";

/** Sets the browser tab title for the current page, restoring the app default on unmount. */
export function usePageTitle(title: string | null | undefined) {
  useEffect(() => {
    document.title = title ? `${title} – Weaver` : "Weaver";
    return () => {
      document.title = "Weaver";
    };
  }, [title]);
}
