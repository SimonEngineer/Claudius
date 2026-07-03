import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { RateLimitPoliciesApi, ScrapingProjectsApi } from "../api/endpoints";
import PagePicker, { type PickedElement } from "../components/PagePicker";
import StatusPill from "../components/StatusPill";
import ItemHistoryModal from "../components/ItemHistoryModal";
import ItemDetailModal from "../components/ItemDetailModal";
import CronPreviewHint from "../components/CronPreviewHint";
import { emptyProxyConfig, type FieldAttribute, type FieldSelector, type PaginationStrategy, type RenderMode, type ScrapedItem, type ScrapeMode, type UpsertScrapingProjectRequest } from "../types";
import { formatDuration } from "../utils/duration";
import { onRunStatusChanged } from "../realtime/runStatusConnection";
import { usePageTitle } from "../utils/usePageTitle";

const emptyForm: UpsertScrapingProjectRequest = {
  name: "",
  description: "",
  startUrl: "",
  mode: "List",
  renderMode: "Http",
  itemSelector: null,
  paginationStrategy: "None",
  nextPageSelector: null,
  pageUrlTemplate: null,
  maxPages: 20,
  customHeaders: {},
  proxy: emptyProxyConfig,
  scheduleCron: null,
  startUrls: [],
  respectRobotsTxt: false,
  dataRetentionDays: null,
  rateLimitPolicyId: null,
  isEnabled: true,
  fields: [],
};

type PickTarget = "itemSelector" | "nextPageSelector" | { fieldIndex: number } | null;

function guessAttribute(tagName: string): FieldAttribute {
  if (tagName === "a") return "Href";
  if (tagName === "img") return "Src";
  return "Text";
}

export default function ScrapingProjectEditor() {
  const { id } = useParams();
  const isNew = !id;
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const existing = useQuery({
    queryKey: ["scraping-project", id],
    queryFn: () => ScrapingProjectsApi.get(id!),
    enabled: !isNew,
  });

  const policies = useQuery({ queryKey: ["rate-limit-policies"], queryFn: RateLimitPoliciesApi.list });
  const runs = useQuery({
    queryKey: ["scraping-project-runs", id],
    queryFn: () => ScrapingProjectsApi.runs(id!),
    enabled: !isNew,
    // SignalR pushes an update the moment a run's status actually changes; this is just the
    // fallback for a dropped/reconnecting connection, so it can be much less frequent than before.
    refetchInterval: 20000,
  });

  useEffect(() => {
    if (isNew) return;
    return onRunStatusChanged((event) => {
      if (event.kind === "scrape") {
        queryClient.invalidateQueries({ queryKey: ["scraping-project-runs", id] });
        queryClient.invalidateQueries({ queryKey: ["scraping-project-items", id] });
        queryClient.invalidateQueries({ queryKey: ["scraping-projects"] });
      }
    });
  }, [isNew, id, queryClient]);
  const [itemsPage, setItemsPage] = useState(1);
  const [itemsPageSize, setItemsPageSize] = useState(20);
  const items = useQuery({
    queryKey: ["scraping-project-items", id, itemsPage, itemsPageSize],
    queryFn: () => ScrapingProjectsApi.items(id!, itemsPage, itemsPageSize),
    enabled: !isNew,
  });
  const [itemSearchInput, setItemSearchInput] = useState("");
  const [itemSearchTerm, setItemSearchTerm] = useState("");
  useEffect(() => {
    const handle = setTimeout(() => setItemSearchTerm(itemSearchInput.trim()), 300);
    return () => clearTimeout(handle);
  }, [itemSearchInput]);
  useEffect(() => setItemsPage(1), [itemSearchTerm]);
  const itemSearch = useQuery({
    queryKey: ["scraping-project-items-search", id, itemSearchTerm, itemsPage, itemsPageSize],
    queryFn: () => ScrapingProjectsApi.searchItems(id!, itemSearchTerm, itemsPage, itemsPageSize),
    enabled: !isNew && itemSearchTerm.length > 0,
  });
  const displayedItems = itemSearchTerm ? itemSearch.data : items.data;
  const [exportFormat, setExportFormat] = useState<"csv" | "json">("csv");
  const [exportError, setExportError] = useState<string | null>(null);
  const [historyItemKey, setHistoryItemKey] = useState<string | null>(null);

  const [form, setForm] = useState<UpsertScrapingProjectRequest>(emptyForm);
  usePageTitle(isNew ? "New Project" : form.name || "Project");
  const [previewUrl, setPreviewUrl] = useState("");
  const [pickTarget, setPickTarget] = useState<PickTarget>(null);
  const [headerRows, setHeaderRows] = useState<{ key: string; value: string }[]>([]);

  useEffect(() => {
    if (existing.data) {
      const { id: _omit, createdAt: _c, updatedAt: _u, lastRunStatus: _lrs, lastRunAt: _lra, ...rest } = existing.data;
      setForm(rest);
      setPreviewUrl(rest.startUrl);
      setHeaderRows(Object.entries(rest.customHeaders).map(([key, value]) => ({ key, value })));
    }
  }, [existing.data]);

  const updateHeaderRows = (rows: { key: string; value: string }[]) => {
    setHeaderRows(rows);
    setForm((f) => ({
      ...f,
      customHeaders: Object.fromEntries(rows.filter((r) => r.key.trim() !== "").map((r) => [r.key, r.value])),
    }));
  };

  const saveMutation = useMutation({
    mutationFn: () => (isNew ? ScrapingProjectsApi.create(form) : ScrapingProjectsApi.update(id!, form)),
    onSuccess: (saved) => {
      queryClient.invalidateQueries({ queryKey: ["scraping-projects"] });
      if (isNew) navigate(`/scraping-projects/${saved.id}`);
      else queryClient.invalidateQueries({ queryKey: ["scraping-project", id] });
    },
  });

  const runMutation = useMutation({
    mutationFn: () => ScrapingProjectsApi.run(id!),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["scraping-project-runs", id] }),
  });

  const clearRunsMutation = useMutation({
    mutationFn: () => ScrapingProjectsApi.clearRuns(id!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["scraping-project-runs", id] });
      queryClient.invalidateQueries({ queryKey: ["scraping-project-items", id] });
      queryClient.invalidateQueries({ queryKey: ["scraping-projects"] });
    },
  });

  const cancelRunMutation = useMutation({
    mutationFn: (runId: string) => ScrapingProjectsApi.cancelRun(id!, runId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["scraping-project-runs", id] }),
  });

  const [detailItem, setDetailItem] = useState<ScrapedItem | null>(null);
  const deleteItemMutation = useMutation({
    mutationFn: (itemId: string) => ScrapingProjectsApi.deleteItem(id!, itemId),
    onSuccess: () => {
      setDetailItem(null);
      queryClient.invalidateQueries({ queryKey: ["scraping-project-items", id] });
      queryClient.invalidateQueries({ queryKey: ["scraping-project-items-search", id] });
    },
  });

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "s") {
        e.preventDefault();
        if (!saveMutation.isPending) saveMutation.mutate();
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [saveMutation.isPending, form]);

  const testExtractMutation = useMutation({
    mutationFn: () =>
      ScrapingProjectsApi.testExtract({
        url: previewUrl || form.startUrl,
        mode: form.mode,
        itemSelector: form.itemSelector,
        fields: form.fields,
        rateLimitPolicyId: form.rateLimitPolicyId,
        scrapingProjectId: id ?? null,
        renderMode: form.renderMode,
        customHeaders: form.customHeaders,
        proxy: form.proxy,
      }),
  });

  const addField = () => {
    const next: FieldSelector = {
      id: null,
      name: "",
      selector: "",
      attribute: "Text",
      attributeName: null,
      resolveUrl: false,
      isKey: false,
      required: false,
      order: form.fields.length,
    };
    setForm({ ...form, fields: [...form.fields, next] });
  };

  const updateField = (index: number, patch: Partial<FieldSelector>) => {
    setForm({
      ...form,
      fields: form.fields.map((f, i) => (i === index ? { ...f, ...patch } : f)),
    });
  };

  const removeField = (index: number) => {
    setForm({ ...form, fields: form.fields.filter((_, i) => i !== index) });
  };

  const moveField = (index: number, direction: -1 | 1) => {
    const target = index + direction;
    if (target < 0 || target >= form.fields.length) return;

    const reordered = [...form.fields];
    [reordered[index], reordered[target]] = [reordered[target], reordered[index]];
    // Extraction runs fields in Order, so the array position and the persisted Order must
    // stay in lockstep -- otherwise a reorder here wouldn't actually change extraction order.
    setForm({ ...form, fields: reordered.map((f, i) => ({ ...f, order: i })) });
  };

  const handlePick = useCallback(
    (picked: PickedElement) => {
      if (!pickTarget) return;

      if (pickTarget === "itemSelector") {
        setForm((f) => ({ ...f, itemSelector: picked.repeatingContainerSelector ?? picked.exactSelector }));
      } else if (pickTarget === "nextPageSelector") {
        setForm((f) => ({ ...f, nextPageSelector: picked.exactSelector }));
      } else {
        const selector = picked.relativeSelector ?? picked.exactSelector ?? "";
        setForm((f) => ({
          ...f,
          fields: f.fields.map((field, i) =>
            i === pickTarget.fieldIndex
              ? { ...field, selector, attribute: guessAttribute(picked.element.tagName) }
              : field,
          ),
        }));
      }
      setPickTarget(null);
    },
    [pickTarget],
  );

  const itemColumns = useMemo(() => {
    const cols = new Set<string>();
    displayedItems?.items.forEach((i) => Object.keys(i.data).forEach((k) => cols.add(k)));
    return Array.from(cols);
  }, [displayedItems]);

  return (
    <div>
      <div className="page-header">
        <h2>{isNew ? "New Scraping Project" : form.name || "Scraping Project"}</h2>
        <div style={{ display: "flex", gap: 8 }}>
          {!isNew && (
            <button disabled={runMutation.isPending} onClick={() => runMutation.mutate()}>
              Run now
            </button>
          )}
          <button
            className="primary"
            disabled={saveMutation.isPending || (!isNew && (existing.isLoading || existing.isError))}
            title={!isNew && existing.isLoading ? "Waiting for the existing project to load…" : undefined}
            onClick={() => saveMutation.mutate()}
          >
            {!isNew && existing.isLoading ? "Loading…" : "Save"}
          </button>
        </div>
      </div>

      {!isNew && existing.isError && (
        <p style={{ color: "var(--danger)" }}>
          Failed to load this project -- editing and saving is disabled until it loads successfully. Try reloading the page.
        </p>
      )}

      <div className="split">
        <div style={{ overflow: "auto", paddingRight: 8 }}>
          <div className="card">
            <div className="row">
              <div className="field">
                <label>Name</label>
                <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
              </div>
              <div className="field">
                <label>Mode</label>
                <select
                  value={form.mode}
                  onChange={(e) => setForm({ ...form, mode: e.target.value as ScrapeMode })}
                >
                  <option value="List">List (repeating items)</option>
                  <option value="SingleItem">Single item (one page = one record)</option>
                </select>
              </div>
              <div className="field">
                <label title="Renders the page in headless Chromium first, for sites that build their content with JavaScript">
                  Rendering
                </label>
                <select
                  value={form.renderMode}
                  onChange={(e) => setForm({ ...form, renderMode: e.target.value as RenderMode })}
                >
                  <option value="Http">Plain HTTP (fast)</option>
                  <option value="Playwright">Rendered browser (for JS-heavy sites)</option>
                </select>
              </div>
            </div>
            <div className="field">
              <label>Description</label>
              <input
                value={form.description ?? ""}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
              />
            </div>
            <div className="field">
              <label>Start URL</label>
              <div style={{ display: "flex", gap: 8 }}>
                <input value={form.startUrl} onChange={(e) => setForm({ ...form, startUrl: e.target.value })} />
                <button onClick={() => setPreviewUrl(form.startUrl)}>Load in preview →</button>
              </div>
            </div>

            {form.mode === "List" && (
              <div className="field">
                <label>Item (row) selector</label>
                <div style={{ display: "flex", gap: 8 }}>
                  <input
                    className="mono"
                    value={form.itemSelector ?? ""}
                    onChange={(e) => setForm({ ...form, itemSelector: e.target.value })}
                    placeholder="e.g. .product-card"
                  />
                  <button
                    className={pickTarget === "itemSelector" ? "primary" : ""}
                    onClick={() => setPickTarget(pickTarget === "itemSelector" ? null : "itemSelector")}
                  >
                    {pickTarget === "itemSelector" ? "Click an item…" : "Pick"}
                  </button>
                </div>
              </div>
            )}

            <div className="row">
              <div className="field">
                <label>Pagination</label>
                <select
                  value={form.paginationStrategy}
                  onChange={(e) => setForm({ ...form, paginationStrategy: e.target.value as PaginationStrategy })}
                >
                  <option value="None">None</option>
                  <option value="NextLinkSelector">Follow a "next page" link</option>
                  <option value="UrlPattern">URL pattern with {"{page}"}</option>
                </select>
              </div>
              <div className="field">
                <label>Max pages</label>
                <input
                  type="number"
                  value={form.maxPages}
                  onChange={(e) => setForm({ ...form, maxPages: Number(e.target.value) })}
                />
              </div>
            </div>

            {form.paginationStrategy === "NextLinkSelector" && (
              <div className="field">
                <label>Next page link selector</label>
                <div style={{ display: "flex", gap: 8 }}>
                  <input
                    className="mono"
                    value={form.nextPageSelector ?? ""}
                    onChange={(e) => setForm({ ...form, nextPageSelector: e.target.value })}
                    placeholder="e.g. a.next"
                  />
                  <button
                    className={pickTarget === "nextPageSelector" ? "primary" : ""}
                    onClick={() => setPickTarget(pickTarget === "nextPageSelector" ? null : "nextPageSelector")}
                  >
                    {pickTarget === "nextPageSelector" ? "Click the link…" : "Pick"}
                  </button>
                </div>
              </div>
            )}

            {form.paginationStrategy === "UrlPattern" && (
              <div className="field">
                <label>Page URL template</label>
                <input
                  value={form.pageUrlTemplate ?? ""}
                  onChange={(e) => setForm({ ...form, pageUrlTemplate: e.target.value })}
                  placeholder="https://example.com/list?page={page}"
                />
              </div>
            )}

            <div className="row">
              <div className="field">
                <label>Rate limit policy</label>
                <select
                  value={form.rateLimitPolicyId ?? ""}
                  onChange={(e) => setForm({ ...form, rateLimitPolicyId: e.target.value || null })}
                >
                  <option value="">None (unthrottled)</option>
                  {policies.data?.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name} ({p.permitLimit}/{p.windowSeconds}s, {p.keyScope})
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label>Enabled</label>
                <select
                  value={form.isEnabled ? "true" : "false"}
                  onChange={(e) => setForm({ ...form, isEnabled: e.target.value === "true" })}
                >
                  <option value="true">Enabled</option>
                  <option value="false">Disabled</option>
                </select>
              </div>
              <div className="field">
                <label>Data retention (days)</label>
                <input
                  type="number"
                  min={1}
                  placeholder="Keep forever"
                  value={form.dataRetentionDays ?? ""}
                  onChange={(e) =>
                    setForm({ ...form, dataRetentionDays: e.target.value === "" ? null : Number(e.target.value) })
                  }
                />
              </div>
            </div>
          </div>

          <div className="card">
            <h3 style={{ marginTop: 0, fontSize: 14 }}>Schedule & crawl behavior</h3>
            <div className="row">
              <div className="field">
                <label>Schedule (cron, UTC — optional)</label>
                <input
                  className="mono"
                  placeholder="*/30 * * * *"
                  value={form.scheduleCron ?? ""}
                  onChange={(e) => setForm({ ...form, scheduleCron: e.target.value === "" ? null : e.target.value })}
                />
                <CronPreviewHint expression={form.scheduleCron ?? ""} />
              </div>
              <div className="field">
                <label>Extra start URLs (one per line)</label>
                <textarea
                  className="mono"
                  rows={3}
                  value={form.startUrls.join("\n")}
                  onChange={(e) =>
                    setForm({ ...form, startUrls: e.target.value.split("\n").map((u) => u.trim()).filter((u) => u !== "") })
                  }
                />
                <p className="muted" style={{ fontSize: 12 }}>
                  Crawled after the main start URL, each with the same pagination rules.
                </p>
              </div>
            </div>
            <label style={{ display: "flex", alignItems: "center", gap: 6 }}>
              <input
                type="checkbox"
                checked={form.respectRobotsTxt}
                onChange={(e) => setForm({ ...form, respectRobotsTxt: e.target.checked })}
              />
              <span className="muted">Respect robots.txt (skip URLs the site disallows for scrapers)</span>
            </label>
          </div>

          <div className="card">
            <div className="page-header">
              <h3 style={{ margin: 0, fontSize: 14 }}>Custom HTTP headers</h3>
              <button onClick={() => updateHeaderRows([...headerRows, { key: "", value: "" }])}>+ Add header</button>
            </div>
            <p style={{ color: "var(--text-dim)", fontSize: 12, marginTop: 0 }}>
              Sent with every request this project makes -- useful for an API key, an auth token, or a specific
              Accept-Language. A header named <code>Cookie</code> is handled specially: in Playwright render mode
              it's applied as real browser cookies (<code>name=value; name2=value2</code>) so a login session
              survives page navigation, not just as a raw header.
            </p>
            {headerRows.length > 0 && (
              <table>
                <thead>
                  <tr>
                    <th>Header name</th>
                    <th>Value</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {headerRows.map((row, i) => (
                    <tr key={i}>
                      <td>
                        <input
                          className="mono"
                          placeholder="e.g. Cookie"
                          value={row.key}
                          onChange={(e) =>
                            updateHeaderRows(headerRows.map((r, ri) => (ri === i ? { ...r, key: e.target.value } : r)))
                          }
                        />
                      </td>
                      <td>
                        <input
                          className="mono"
                          value={row.value}
                          onChange={(e) =>
                            updateHeaderRows(headerRows.map((r, ri) => (ri === i ? { ...r, value: e.target.value } : r)))
                          }
                        />
                      </td>
                      <td>
                        <button className="danger" onClick={() => updateHeaderRows(headerRows.filter((_, ri) => ri !== i))}>
                          ×
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>

          <div className="card">
            <div className="page-header">
              <h3 style={{ margin: 0, fontSize: 14 }}>Outbound proxy</h3>
              <label style={{ display: "flex", alignItems: "center", gap: 6 }}>
                <input
                  type="checkbox"
                  checked={form.proxy.enabled}
                  onChange={(e) => setForm({ ...form, proxy: { ...form.proxy, enabled: e.target.checked } })}
                />
                <span className="muted">enabled</span>
              </label>
            </div>
            <p style={{ color: "var(--text-dim)", fontSize: 12, marginTop: 0 }}>
              Routes every request this project makes through an HTTP or SOCKS5 proxy. The password is encrypted at rest.
            </p>
            {form.proxy.enabled && (
              <div className="row">
                <div className="field">
                  <label>Protocol</label>
                  <select
                    value={form.proxy.protocol}
                    onChange={(e) => setForm({ ...form, proxy: { ...form.proxy, protocol: e.target.value as "http" | "socks5" } })}
                  >
                    <option value="http">HTTP</option>
                    <option value="socks5">SOCKS5</option>
                  </select>
                </div>
                <div className="field">
                  <label>Host</label>
                  <input
                    value={form.proxy.host}
                    placeholder="proxy.example.com"
                    onChange={(e) => setForm({ ...form, proxy: { ...form.proxy, host: e.target.value } })}
                  />
                </div>
                <div className="field">
                  <label>Port</label>
                  <input
                    type="number"
                    min={1}
                    max={65535}
                    value={form.proxy.port || ""}
                    onChange={(e) => setForm({ ...form, proxy: { ...form.proxy, port: Number(e.target.value) } })}
                  />
                </div>
                <div className="field">
                  <label>Username (optional)</label>
                  <input
                    value={form.proxy.username ?? ""}
                    onChange={(e) => setForm({ ...form, proxy: { ...form.proxy, username: e.target.value } })}
                  />
                </div>
                <div className="field">
                  <label>Password (optional)</label>
                  <input
                    type="password"
                    value={form.proxy.password ?? ""}
                    onChange={(e) => setForm({ ...form, proxy: { ...form.proxy, password: e.target.value } })}
                  />
                </div>
              </div>
            )}
          </div>

          <div className="card">
            <div className="page-header">
              <h3 style={{ margin: 0, fontSize: 14 }}>Fields to extract</h3>
              <div style={{ display: "flex", gap: 8 }}>
                <button disabled={testExtractMutation.isPending} onClick={() => testExtractMutation.mutate()}>
                  {testExtractMutation.isPending ? "Testing…" : "Test extraction"}
                </button>
                <button onClick={addField}>+ Add field</button>
              </div>
            </div>
            {form.mode === "List" && form.fields.length > 0 && !form.fields.some((f) => f.isKey) && (
              <p style={{ color: "var(--warn)", fontSize: 12, marginTop: 0 }}>
                No field is marked as a Key. Without one, Weaver can't reliably tell "this item changed"
                from "this is a new item" across runs -- mark a field with a stable value per item (e.g. a
                detail URL or SKU) as Key so change-detection and price-drop style automations work.
              </p>
            )}
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Selector</th>
                  <th>Attribute</th>
                  <th>Key</th>
                  <th>Req.</th>
                  <th>URL</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {form.fields.map((field, i) => (
                  <tr key={i}>
                    <td>
                      <input value={field.name} onChange={(e) => updateField(i, { name: e.target.value })} />
                    </td>
                    <td style={{ display: "flex", gap: 4 }}>
                      <input
                        className="mono"
                        value={field.selector}
                        onChange={(e) => updateField(i, { selector: e.target.value })}
                      />
                      <button
                        className={
                          pickTarget && typeof pickTarget === "object" && pickTarget.fieldIndex === i ? "primary" : ""
                        }
                        onClick={() =>
                          setPickTarget(
                            pickTarget && typeof pickTarget === "object" && pickTarget.fieldIndex === i
                              ? null
                              : { fieldIndex: i },
                          )
                        }
                      >
                        Pick
                      </button>
                    </td>
                    <td>
                      <select
                        value={field.attribute}
                        onChange={(e) => updateField(i, { attribute: e.target.value as FieldAttribute })}
                      >
                        <option value="Text">Text</option>
                        <option value="Html">HTML</option>
                        <option value="Href">href</option>
                        <option value="Src">src</option>
                        <option value="Attribute">Attribute…</option>
                      </select>
                      {field.attribute === "Attribute" && (
                        <input
                          placeholder="attr name"
                          value={field.attributeName ?? ""}
                          onChange={(e) => updateField(i, { attributeName: e.target.value })}
                        />
                      )}
                    </td>
                    <td>
                      <input type="checkbox" checked={field.isKey} onChange={(e) => updateField(i, { isKey: e.target.checked })} />
                    </td>
                    <td>
                      <input
                        type="checkbox"
                        checked={field.required}
                        onChange={(e) => updateField(i, { required: e.target.checked })}
                      />
                    </td>
                    <td>
                      <input
                        type="checkbox"
                        checked={field.resolveUrl}
                        onChange={(e) => updateField(i, { resolveUrl: e.target.checked })}
                      />
                    </td>
                    <td style={{ display: "flex", gap: 4 }}>
                      <button disabled={i === 0} onClick={() => moveField(i, -1)} title="Move up">
                        ↑
                      </button>
                      <button disabled={i === form.fields.length - 1} onClick={() => moveField(i, 1)} title="Move down">
                        ↓
                      </button>
                      <button className="danger" onClick={() => removeField(i)}>
                        ×
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {form.fields.length === 0 && <p className="muted">No fields yet -- add one, then click "Pick" and click the element on the page.</p>}

            {testExtractMutation.data && (
              <div style={{ marginTop: 12 }}>
                {testExtractMutation.data.errorMessage ? (
                  <p style={{ color: "var(--danger)" }}>{testExtractMutation.data.errorMessage}</p>
                ) : (
                  <>
                    <p className="muted">{testExtractMutation.data.itemsFound} item(s) extracted from this page (preview only, not saved):</p>
                    <table>
                      <thead>
                        <tr>
                          {form.fields.map((f) => (
                            <th key={f.name}>{f.name}</th>
                          ))}
                        </tr>
                      </thead>
                      <tbody>
                        {testExtractMutation.data.items.map((item, i) => (
                          <tr key={i}>
                            {form.fields.map((f) => (
                              <td key={f.name} style={{ maxWidth: 200, overflow: "hidden", textOverflow: "ellipsis" }}>
                                {item[f.name] ?? ""}
                              </td>
                            ))}
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </>
                )}
              </div>
            )}
          </div>

          {!isNew && (
            <div className="card">
              <div className="page-header">
                <h3 style={{ margin: 0, fontSize: 14 }}>Recent runs</h3>
                {runs.data && runs.data.length > 0 && (
                  <div style={{ display: "flex", gap: 8 }}>
                    <button
                      className="danger"
                      disabled={clearRunsMutation.isPending}
                      onClick={() => {
                        if (confirm("Delete ALL runs and scraped items for this project? This can't be undone."))
                          clearRunsMutation.mutate();
                      }}
                    >
                      Clear history
                    </button>
                    <button onClick={() => ScrapingProjectsApi.exportRuns(id!, "csv")}>Export CSV</button>
                    <button onClick={() => ScrapingProjectsApi.exportRuns(id!, "json")}>Export JSON</button>
                  </div>
                )}
              </div>
              {runs.data?.length ? (
                <table>
                  <thead>
                    <tr>
                      <th>Status</th>
                      <th>Trigger</th>
                      <th>Pages</th>
                      <th>Items</th>
                      <th>Changed</th>
                      <th>Started</th>
                      <th>Duration</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {runs.data.map((r) => (
                      <tr key={r.id}>
                        <td>
                          <StatusPill status={r.status} />
                        </td>
                        <td className="muted">{r.triggeredBy}</td>
                        <td>{r.pagesCrawled}</td>
                        <td>{r.itemsFound}</td>
                        <td>{r.itemsChanged}</td>
                        <td className="muted">{r.startedAt ? new Date(r.startedAt).toLocaleString() : "-"}</td>
                        <td className="muted">{formatDuration(r.startedAt, r.completedAt) ?? "-"}</td>
                        <td>
                          {(r.status === "Running" || r.status === "Pending") && (
                            <button
                              disabled={cancelRunMutation.isPending}
                              onClick={() => cancelRunMutation.mutate(r.id)}
                            >
                              Cancel
                            </button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                <p className="muted">No runs yet.</p>
              )}
              {runs.data?.[0]?.errorMessage && (
                <p style={{ color: "var(--danger)" }}>{runs.data[0].errorMessage}</p>
              )}
            </div>
          )}

          {!isNew && items.data && items.data.totalCount > 0 && (
            <div className="card">
              <div className="page-header">
                <h3 style={{ margin: 0, fontSize: 14 }}>
                  Scraped items ({itemSearchTerm ? `${displayedItems?.totalCount ?? 0} matching` : items.data.totalCount})
                </h3>
                <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                  <select
                    value={itemsPageSize}
                    title="Items per page"
                    onChange={(e) => {
                      setItemsPageSize(Number(e.target.value));
                      setItemsPage(1);
                    }}
                  >
                    <option value={20}>20 / page</option>
                    <option value={50}>50 / page</option>
                    <option value={100}>100 / page</option>
                  </select>
                  <select value={exportFormat} onChange={(e) => setExportFormat(e.target.value as "csv" | "json")}>
                    <option value="csv">CSV</option>
                    <option value="json">JSON</option>
                  </select>
                  <button
                    onClick={() =>
                      ScrapingProjectsApi.exportItems(id!, exportFormat).catch(() =>
                        setExportError("Export failed. Please try again."),
                      )
                    }
                  >
                    Export all
                  </button>
                </div>
              </div>
              <input
                placeholder="Search field values across all runs…"
                value={itemSearchInput}
                onChange={(e) => setItemSearchInput(e.target.value)}
                style={{ marginBottom: 12, width: "100%", maxWidth: 360 }}
              />
              {exportError && <p style={{ color: "var(--danger)", fontSize: 12 }}>{exportError}</p>}
              {displayedItems?.items.length ? (
                <table>
                  <thead>
                    <tr>
                      {itemColumns.map((c) => (
                        <th key={c}>{c}</th>
                      ))}
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {displayedItems.items.map((item) => (
                      <tr key={item.id}>
                        {itemColumns.map((c) => (
                          <td key={c} style={{ maxWidth: 220, overflow: "hidden", textOverflow: "ellipsis" }}>
                            {item.data[c] ?? ""}
                          </td>
                        ))}
                        <td style={{ display: "flex", gap: 6 }}>
                          <button onClick={() => setDetailItem(item)}>View</button>
                          <button onClick={() => setHistoryItemKey(item.itemKey)}>History</button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                itemSearchTerm && <div className="empty-state">No items match "{itemSearchTerm}".</div>
              )}
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: 8 }}>
                <button disabled={itemsPage <= 1} onClick={() => setItemsPage((p) => Math.max(1, p - 1))}>
                  ← Prev
                </button>
                <span className="muted" style={{ fontSize: 12 }}>
                  Page {itemsPage} of {Math.max(1, Math.ceil((displayedItems?.totalCount ?? 0) / itemsPageSize))}
                </span>
                <button
                  disabled={itemsPage * itemsPageSize >= (displayedItems?.totalCount ?? 0)}
                  onClick={() => setItemsPage((p) => p + 1)}
                >
                  Next →
                </button>
              </div>
            </div>
          )}
        </div>

        <div>
          <div className="picker-toolbar">
            <input
              value={previewUrl}
              onChange={(e) => setPreviewUrl(e.target.value)}
              placeholder="URL to preview"
            />
            <button onClick={() => setPreviewUrl(previewUrl)}>Reload</button>
          </div>
          <PagePicker
            url={previewUrl}
            containerSelector={form.itemSelector}
            onPick={handlePick}
            rateLimitPolicyId={form.rateLimitPolicyId}
            scrapingProjectId={id}
            renderMode={form.renderMode}
          />
        </div>
      </div>

      {historyItemKey && (
        <ItemHistoryModal scrapingProjectId={id!} itemKey={historyItemKey} onClose={() => setHistoryItemKey(null)} />
      )}
      {detailItem && (
        <ItemDetailModal
          item={detailItem}
          onDelete={() => deleteItemMutation.mutate(detailItem.id)}
          onClose={() => setDetailItem(null)}
        />
      )}
    </div>
  );
}
