import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { RateLimitPoliciesApi, ScrapingProjectsApi } from "../api/endpoints";
import PagePicker, { type PickedElement } from "../components/PagePicker";
import StatusPill from "../components/StatusPill";
import type { FieldAttribute, FieldSelector, PaginationStrategy, RenderMode, ScrapeMode, UpsertScrapingProjectRequest } from "../types";

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
    refetchInterval: 3000,
  });
  const items = useQuery({
    queryKey: ["scraping-project-items", id],
    queryFn: () => ScrapingProjectsApi.items(id!),
    enabled: !isNew,
  });

  const [form, setForm] = useState<UpsertScrapingProjectRequest>(emptyForm);
  const [previewUrl, setPreviewUrl] = useState("");
  const [pickTarget, setPickTarget] = useState<PickTarget>(null);

  useEffect(() => {
    if (existing.data) {
      const { id: _omit, createdAt: _c, updatedAt: _u, ...rest } = existing.data;
      setForm(rest);
      setPreviewUrl(rest.startUrl);
    }
  }, [existing.data]);

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

  const latestRunId = runs.data?.[0]?.id;
  const itemColumns = useMemo(() => {
    const cols = new Set<string>();
    items.data?.forEach((i) => Object.keys(i.data).forEach((k) => cols.add(k)));
    return Array.from(cols);
  }, [items.data]);

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
            </div>
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
                    <td>
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
              <h3 style={{ marginTop: 0, fontSize: 14 }}>Recent runs</h3>
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

          {!isNew && items.data && items.data.length > 0 && (
            <div className="card">
              <h3 style={{ marginTop: 0, fontSize: 14 }}>
                Latest scraped items {latestRunId ? "" : ""}
              </h3>
              <table>
                <thead>
                  <tr>
                    {itemColumns.map((c) => (
                      <th key={c}>{c}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {items.data.slice(0, 20).map((item) => (
                    <tr key={item.id}>
                      {itemColumns.map((c) => (
                        <td key={c} style={{ maxWidth: 220, overflow: "hidden", textOverflow: "ellipsis" }}>
                          {item.data[c] ?? ""}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
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
    </div>
  );
}
