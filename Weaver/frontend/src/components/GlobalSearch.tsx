import { useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ScrapingProjectsApi, WorkflowsApi } from "../api/endpoints";

export default function GlobalSearch() {
  const navigate = useNavigate();
  const [term, setTerm] = useState("");
  const [focused, setFocused] = useState(false);

  // Reuses whatever's already cached from the list pages -- no dedicated search endpoint needed
  // for a dataset this small.
  const projects = useQuery({ queryKey: ["scraping-projects"], queryFn: ScrapingProjectsApi.list });
  const workflows = useQuery({ queryKey: ["workflows"], queryFn: WorkflowsApi.list });

  const results = useMemo(() => {
    const needle = term.trim().toLowerCase();
    if (!needle) return { projects: [], workflows: [] };
    return {
      projects: (projects.data ?? []).filter((p) => p.name.toLowerCase().includes(needle)).slice(0, 6),
      workflows: (workflows.data ?? []).filter((w) => w.name.toLowerCase().includes(needle)).slice(0, 6),
    };
  }, [term, projects.data, workflows.data]);

  const hasResults = results.projects.length > 0 || results.workflows.length > 0;
  const showDropdown = focused && term.trim().length > 0;

  const goTo = (path: string) => {
    setTerm("");
    setFocused(false);
    navigate(path);
  };

  return (
    <div style={{ position: "relative", marginBottom: 16 }}>
      <input
        placeholder="Search projects & workflows…"
        value={term}
        onChange={(e) => setTerm(e.target.value)}
        onFocus={() => setFocused(true)}
        onBlur={() => setTimeout(() => setFocused(false), 150)}
        onKeyDown={(e) => e.key === "Escape" && setTerm("")}
        style={{ width: "100%" }}
      />
      {showDropdown && (
        <div className="global-search-dropdown">
          {hasResults ? (
            <>
              {results.projects.length > 0 && (
                <div>
                  <div className="global-search-group-label">Scraping Projects</div>
                  {results.projects.map((p) => (
                    <div key={p.id} className="global-search-result" onMouseDown={() => goTo(`/scraping-projects/${p.id}`)}>
                      {p.name}
                    </div>
                  ))}
                </div>
              )}
              {results.workflows.length > 0 && (
                <div>
                  <div className="global-search-group-label">Workflows</div>
                  {results.workflows.map((w) => (
                    <div key={w.id} className="global-search-result" onMouseDown={() => goTo(`/workflows/${w.id}`)}>
                      {w.name}
                    </div>
                  ))}
                </div>
              )}
            </>
          ) : (
            <div className="global-search-result muted">No matches.</div>
          )}
        </div>
      )}
    </div>
  );
}
