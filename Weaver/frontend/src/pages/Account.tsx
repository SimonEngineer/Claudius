import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { ApiKeysApi, AuthApi, CredentialsApi, MetaApi, NotificationSettingsApi } from "../api/endpoints";
import { useAuth } from "../auth/AuthContext";
import type { CreatedApiKey, NotificationSettings } from "../types";
import { usePageTitle } from "../utils/usePageTitle";

export default function Account() {
  usePageTitle("Account");
  const { user } = useAuth();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);
  const [busy, setBusy] = useState(false);

  const queryClient = useQueryClient();
  const overview = useQuery({ queryKey: ["account-overview"], queryFn: MetaApi.accountOverview });
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

  const credentials = useQuery({ queryKey: ["credentials"], queryFn: CredentialsApi.list });
  const [newCredName, setNewCredName] = useState("");
  const [newCredValue, setNewCredValue] = useState("");
  const [credError, setCredError] = useState<string | null>(null);
  const createCredMutation = useMutation({
    mutationFn: () => CredentialsApi.create(newCredName.trim(), newCredValue),
    onSuccess: () => {
      setNewCredName("");
      setNewCredValue("");
      setCredError(null);
      queryClient.invalidateQueries({ queryKey: ["credentials"] });
    },
    onError: (e) => {
      const message = (e as { response?: { data?: string } }).response?.data;
      setCredError(typeof message === "string" ? message : "Could not create credential.");
    },
  });
  const updateCredMutation = useMutation({
    mutationFn: ({ id, value }: { id: string; value: string }) => CredentialsApi.updateValue(id, value),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["credentials"] }),
  });
  const removeCredMutation = useMutation({
    mutationFn: CredentialsApi.remove,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["credentials"] }),
  });

  const notificationSettings = useQuery({ queryKey: ["notification-settings"], queryFn: NotificationSettingsApi.get });
  const [notifForm, setNotifForm] = useState<NotificationSettings>({
    notifyOnScrapeFailure: false,
    notifyOnWorkflowFailure: false,
    emailEnabled: false,
    webhookUrl: null,
  });
  const [notifSaved, setNotifSaved] = useState(false);
  useEffect(() => {
    if (notificationSettings.data) setNotifForm(notificationSettings.data);
  }, [notificationSettings.data]);
  const saveNotifMutation = useMutation({
    mutationFn: () => NotificationSettingsApi.update(notifForm),
    onSuccess: () => {
      setNotifSaved(true);
      setTimeout(() => setNotifSaved(false), 1500);
      queryClient.invalidateQueries({ queryKey: ["notification-settings"] });
    },
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
        {overview.data && (
          <p className="muted" style={{ fontSize: 12 }}>
            Member since {new Date(overview.data.createdAt).toLocaleDateString()} · {overview.data.projects} project(s),{" "}
            {overview.data.workflows} workflow(s), {overview.data.credentials} credential(s), {overview.data.apiKeys} API key(s)
          </p>
        )}

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

      <div className="card" style={{ maxWidth: 600 }}>
        <h3 style={{ marginTop: 0, fontSize: 14 }}>Credentials</h3>
        <p className="muted">
          Named secrets you can reference from any workflow node config as{" "}
          <code>{"{{secrets.NAME}}"}</code> -- the value is encrypted at rest and substituted only at
          execution time, so it never appears in the workflow itself or its export file. Values are
          write-only: they can be overwritten but never read back.
        </p>

        <div style={{ display: "flex", gap: 8, marginBottom: 12 }}>
          <input placeholder="Name, e.g. MY_API_KEY" className="mono" value={newCredName} onChange={(e) => setNewCredName(e.target.value)} />
          <input placeholder="Secret value" type="password" value={newCredValue} onChange={(e) => setNewCredValue(e.target.value)} />
          <button
            className="primary"
            disabled={!newCredName.trim() || !newCredValue || createCredMutation.isPending}
            onClick={() => createCredMutation.mutate()}
          >
            + Add
          </button>
        </div>
        {credError && <p style={{ color: "var(--danger)", fontSize: 12 }}>{credError}</p>}

        {credentials.data?.length ? (
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Reference</th>
                <th>Updated</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {credentials.data.map((c) => (
                <tr key={c.id}>
                  <td>{c.name}</td>
                  <td className="mono muted">{`{{secrets.${c.name}}}`}</td>
                  <td className="muted">{new Date(c.updatedAt).toLocaleString()}</td>
                  <td style={{ display: "flex", gap: 6 }}>
                    <button
                      disabled={updateCredMutation.isPending}
                      onClick={() => {
                        const value = prompt(`New value for "${c.name}" (the old value can't be shown):`);
                        if (value) updateCredMutation.mutate({ id: c.id, value });
                      }}
                    >
                      Replace value
                    </button>
                    <button
                      className="danger"
                      onClick={() => {
                        if (confirm(`Delete "${c.name}"? Nodes referencing it will keep the literal placeholder text.`)) {
                          removeCredMutation.mutate(c.id);
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
          <p className="muted">No credentials yet.</p>
        )}
      </div>

      <div className="card" style={{ maxWidth: 600 }}>
        <h3 style={{ marginTop: 0, fontSize: 14 }}>Failure notifications</h3>
        <p className="muted">Get notified when a scrape or workflow run fails.</p>

        <label style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 8 }}>
          <input
            type="checkbox"
            checked={notifForm.notifyOnScrapeFailure}
            onChange={(e) => setNotifForm({ ...notifForm, notifyOnScrapeFailure: e.target.checked })}
          />
          Notify on scrape failures
        </label>
        <label style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 8 }}>
          <input
            type="checkbox"
            checked={notifForm.notifyOnWorkflowFailure}
            onChange={(e) => setNotifForm({ ...notifForm, notifyOnWorkflowFailure: e.target.checked })}
          />
          Notify on workflow failures
        </label>
        <label style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 8 }}>
          <input
            type="checkbox"
            checked={notifForm.emailEnabled}
            onChange={(e) => setNotifForm({ ...notifForm, emailEnabled: e.target.checked })}
          />
          Email me at {user?.email}
        </label>
        <div className="field">
          <label>Webhook URL (optional — receives a JSON POST; stored encrypted)</label>
          <input
            className="mono"
            placeholder="https://hooks.slack.com/services/…"
            value={notifForm.webhookUrl ?? ""}
            onChange={(e) => setNotifForm({ ...notifForm, webhookUrl: e.target.value === "" ? null : e.target.value })}
          />
        </div>

        <button className="primary" disabled={saveNotifMutation.isPending} onClick={() => saveNotifMutation.mutate()}>
          {notifSaved ? "Saved!" : "Save notification settings"}
        </button>
      </div>
    </div>
  );
}
