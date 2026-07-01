import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { ApiKeysApi, AuthApi } from "../api/endpoints";
import { useAuth } from "../auth/AuthContext";
import type { CreatedApiKey } from "../types";

export default function Account() {
  const { user } = useAuth();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);
  const [busy, setBusy] = useState(false);

  const queryClient = useQueryClient();
  const apiKeys = useQuery({ queryKey: ["api-keys"], queryFn: ApiKeysApi.list });
  const [newKeyName, setNewKeyName] = useState("");
  const [justCreated, setJustCreated] = useState<CreatedApiKey | null>(null);
  const createKeyMutation = useMutation({
    mutationFn: () => ApiKeysApi.create(newKeyName, null),
    onSuccess: (created) => {
      setJustCreated(created);
      setNewKeyName("");
      queryClient.invalidateQueries({ queryKey: ["api-keys"] });
    },
  });
  const removeKeyMutation = useMutation({
    mutationFn: ApiKeysApi.remove,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["api-keys"] }),
  });

  const submit = async () => {
    setError(null);
    setSuccess(false);

    if (newPassword.length < 8) {
      setError("New password must be at least 8 characters.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("New password and confirmation don't match.");
      return;
    }

    setBusy(true);
    try {
      await AuthApi.changePassword(currentPassword, newPassword);
      setSuccess(true);
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
    } catch (e) {
      const message = (e as { response?: { data?: string } }).response?.data;
      setError(typeof message === "string" ? message : "Something went wrong. Please try again.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <div className="page-header">
        <h2>Account</h2>
      </div>

      <div className="card" style={{ maxWidth: 400 }}>
        <p className="muted">Signed in as {user?.email}</p>

        <h3 style={{ fontSize: 14 }}>Change password</h3>
        <div className="field">
          <label>Current password</label>
          <input type="password" value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} />
        </div>
        <div className="field">
          <label>New password</label>
          <input type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} />
        </div>
        <div className="field">
          <label>Confirm new password</label>
          <input type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} />
        </div>

        {error && <p style={{ color: "var(--danger)" }}>{error}</p>}
        {success && <p style={{ color: "var(--accent-2)" }}>Password changed.</p>}

        <button
          className="primary"
          disabled={busy || !currentPassword || !newPassword || !confirmPassword}
          onClick={submit}
        >
          {busy ? "Saving…" : "Change password"}
        </button>
      </div>

      <div className="card" style={{ maxWidth: 600 }}>
        <h3 style={{ marginTop: 0, fontSize: 14 }}>Personal API keys</h3>
        <p className="muted">
          Use a key as a Bearer token to trigger scrapes or workflows from an external script or cron job, without an
          interactive login.
        </p>

        {justCreated && (
          <div className="card" style={{ background: "var(--panel-2)", margin: "0 0 12px" }}>
            <p style={{ marginTop: 0 }}>
              <strong>{justCreated.name}</strong> created. Copy this key now -- it won't be shown again:
            </p>
            <div style={{ display: "flex", gap: 8 }}>
              <input className="mono" readOnly value={justCreated.key} />
              <button onClick={() => navigator.clipboard.writeText(justCreated.key)}>Copy</button>
            </div>
            <button style={{ marginTop: 8 }} onClick={() => setJustCreated(null)}>
              Done
            </button>
          </div>
        )}

        <div style={{ display: "flex", gap: 8, marginBottom: 12 }}>
          <input placeholder="Key name, e.g. 'CI cron job'" value={newKeyName} onChange={(e) => setNewKeyName(e.target.value)} />
          <button
            className="primary"
            disabled={!newKeyName.trim() || createKeyMutation.isPending}
            onClick={() => createKeyMutation.mutate()}
          >
            + Create key
          </button>
        </div>

        {apiKeys.data?.length ? (
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Key</th>
                <th>Created</th>
                <th>Last used</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {apiKeys.data.map((k) => (
                <tr key={k.id}>
                  <td>{k.name}</td>
                  <td className="mono muted">{k.keyPrefix}…</td>
                  <td className="muted">{new Date(k.createdAt).toLocaleDateString()}</td>
                  <td className="muted">{k.lastUsedAt ? new Date(k.lastUsedAt).toLocaleString() : "never"}</td>
                  <td>
                    <button
                      className="danger"
                      onClick={() => {
                        if (confirm(`Revoke "${k.name}"? Anything using this key will stop working immediately.`)) {
                          removeKeyMutation.mutate(k.id);
                        }
                      }}
                    >
                      Revoke
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <p className="muted">No API keys yet.</p>
        )}
      </div>
    </div>
  );
}
