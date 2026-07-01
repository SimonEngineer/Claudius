import { useEffect, useRef } from "react";
import { proxyUrl } from "../api/endpoints";
import type { RenderMode } from "../types";

export interface PickedElement {
  exactSelector: string | null;
  repeatingContainerSelector: string | null;
  relativeSelector: string | null;
  element: { tagName: string; text: string; attributes: Record<string, string> };
}

interface Props {
  url: string;
  containerSelector: string | null;
  onPick: (picked: PickedElement) => void;
  rateLimitPolicyId?: string | null;
  scrapingProjectId?: string;
  renderMode?: RenderMode;
}

/**
 * Iframes the server-rendered proxy of the target page (see PageProxyService) and turns
 * postMessage events from its injected overlay script into selector picks. containerSelector
 * is pushed into the iframe so field picks can be reported relative to the item container.
 * rateLimitPolicyId/scrapingProjectId let this preview traffic share the project's own rate
 * limit bucket instead of always falling back to the conservative interactive default.
 */
export default function PagePicker({ url, containerSelector, onPick, rateLimitPolicyId, scrapingProjectId, renderMode }: Props) {
  const iframeRef = useRef<HTMLIFrameElement>(null);

  useEffect(() => {
    const handler = (event: MessageEvent) => {
      if (event.data?.type === "weaver:elementPicked") {
        onPick(event.data as PickedElement);
      } else if (event.data?.type === "weaver:ready") {
        iframeRef.current?.contentWindow?.postMessage(
          { type: "weaver:setContainerSelector", selector: containerSelector },
          "*",
        );
      }
    };
    window.addEventListener("message", handler);
    return () => window.removeEventListener("message", handler);
  }, [onPick, containerSelector]);

  useEffect(() => {
    iframeRef.current?.contentWindow?.postMessage(
      { type: "weaver:setContainerSelector", selector: containerSelector },
      "*",
    );
  }, [containerSelector]);

  if (!url) {
    return <div className="empty-state">Enter a URL above and click Load to preview the page.</div>;
  }

  return (
    <div className="picker-frame-wrap">
      <iframe
        ref={iframeRef}
        key={`${url}::${renderMode ?? "Http"}`}
        src={proxyUrl(url, rateLimitPolicyId, scrapingProjectId, renderMode)}
        sandbox="allow-scripts allow-same-origin"
      />
    </div>
  );
}
