import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { RateLimitPoliciesApi } from "../api/endpoints";
import type { RateLimitKeyScope, UpsertRateLimitPolicyRequest } from "../types";

const emptyForm: UpsertRateLimitPolicyRequest = {
  name: "",
  keyScope: "PerHost",
  customKeyTemplate: null,
  permitLimit: 60,
  windowSeconds: 60,
  burstCapacity: 60,
};

export default function RateLimitPolicies() {
  const queryClient = useQueryClient();
  const policies = useQuery({ queryKey: ["rate-limit-policies"], queryFn: RateLimitPoliciesApi.list });
  const [form, setForm] = useState<UpsertRateLimitPolicyRequest>(emptyForm);

  const createMutation = useMutation({
    mutationFn: RateLimitPoliciesApi.create,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["rate-limit-policies"] });
      setForm(emptyForm);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: RateLimitPoliciesApi.remove,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["rate-limit-policies"] }),
  });

  return (
    <div>
      <div className="page-header">
        <h2>Rate Limit Policies</h2>
      </div>

      <div className="card">
        <h3 style={{ marginTop: 0, fontSize: 14 }}>New policy</h3>
        <div className="row">
          <div className="field">
            <label>Name</label>
            <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          </div>
          <div className="field">
            <label>Key scope</label>
            <select
              value={form.keyScope}
              onChange={(e) => setForm({ ...form, keyScope: e.target.value as RateLimitKeyScope })}
            >
              <option value="PerHost">Per host (shared across projects hitting the same site)</option>
              <option value="PerUrl">Per exact URL</option>
              <option value="PerProject">Per scraping project</option>
              <option value="Custom">Custom template</option>
            </select>
          </div>
        </div>
        {form.keyScope === "Custom" && (
          <div className="field">
            <label>Custom key template ({"{host}"}, {"{path}"}, {"{project}"})</label>
            <input
              value={form.customKeyTemplate ?? ""}
              onChange={(e) => setForm({ ...form, customKeyTemplate: e.target.value })}
            />
          </div>
        )}
        <div className="row">
          <div className="field">
            <label>Permit limit</label>
            <input
              type="number"
              value={form.permitLimit}
              onChange={(e) => setForm({ ...form, permitLimit: Number(e.target.value) })}
            />
          </div>
          <div className="field">
            <label>Window (seconds)</label>
            <input
              type="number"
              value={form.windowSeconds}
              onChange={(e) => setForm({ ...form, windowSeconds: Number(e.target.value) })}
            />
          </div>
          <div className="field">
            <label>Burst capacity</label>
            <input
              type="number"
              value={form.burstCapacity}
              onChange={(e) => setForm({ ...form, burstCapacity: Number(e.target.value) })}
            />
          </div>
        </div>
        <button
          className="primary"
          disabled={!form.name || createMutation.isPending}
          onClick={() => createMutation.mutate(form)}
        >
          Create policy
        </button>
      </div>

      <div className="card">
        {policies.data?.length ? (
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Scope</th>
                <th>Limit</th>
                <th>Window</th>
                <th>Burst</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {policies.data.map((p) => (
                <tr key={p.id}>
                  <td>{p.name}</td>
                  <td className="muted">{p.keyScope}{p.customKeyTemplate ? ` (${p.customKeyTemplate})` : ""}</td>
                  <td>{p.permitLimit}</td>
                  <td>{p.windowSeconds}s</td>
                  <td>{p.burstCapacity}</td>
                  <td>
                    <button
                      className="danger"
                      onClick={() => {
                        if (confirm(`Delete "${p.name}"? Scraping projects using it will fall back to unthrottled requests.`)) {
                          deleteMutation.mutate(p.id);
                        }
                      }}
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <div className="empty-state">No rate limit policies yet.</div>
        )}
      </div>
    </div>
  );
}
