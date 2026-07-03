import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { usePageTitle } from "../utils/usePageTitle";

export default function Login() {
  const { login, register } = useAuth();
  const navigate = useNavigate();
  const [mode, setMode] = useState<"login" | "register">("login");
  usePageTitle(mode === "login" ? "Log in" : "Register");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  const submit = async () => {
    setError(null);
    setBusy(true);
    try {
      if (mode === "login") {
        await login(email, password);
      } else {
        await register(email, password);
      }
      navigate("/", { replace: true });
    } catch (e) {
      const message =
        (e as { response?: { data?: string } }).response?.data ?? "Something went wrong. Please try again.";
      setError(typeof message === "string" ? message : "Something went wrong. Please try again.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div style={{ display: "flex", alignItems: "center", justifyContent: "center", height: "100vh", width: "100vw" }}>
      <div className="card" style={{ width: 340 }}>
        <h2 style={{ marginTop: 0 }}>
          <span style={{ color: "var(--accent-2)" }}>Weaver</span>
        </h2>
        <p className="muted">{mode === "login" ? "Log in to your account" : "Create an account"}</p>

        <div className="field">
          <label>Email</label>
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
        </div>
        <div className="field">
          <label>Password</label>
          <div style={{ display: "flex", gap: 6 }}>
            <input
              type={showPassword ? "text" : "password"}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && !busy && email && password && submit()}
              style={{ flex: 1 }}
            />
            <button type="button" title={showPassword ? "Hide password" : "Show password"} onClick={() => setShowPassword((s) => !s)}>
              {showPassword ? "Hide" : "Show"}
            </button>
          </div>
        </div>

        {error && <p style={{ color: "var(--danger)" }}>{error}</p>}

        <button
          className="primary"
          style={{ width: "100%" }}
          disabled={busy || !email || !password}
          onClick={submit}
        >
          {mode === "login" ? "Log in" : "Register"}
        </button>

        <p className="muted" style={{ textAlign: "center", marginBottom: 0 }}>
          {mode === "login" ? "No account yet?" : "Already have an account?"}{" "}
          <a onClick={() => setMode(mode === "login" ? "register" : "login")}>
            {mode === "login" ? "Register" : "Log in"}
          </a>
        </p>
      </div>
    </div>
  );
}
