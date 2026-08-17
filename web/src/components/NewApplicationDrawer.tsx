import { ChevronDown, X } from "lucide-react";
import { useState, type FormEvent } from "react";
import type { CreateApplicationRequest } from "../types";

interface NewApplicationDrawerProps {
  open: boolean;
  saving: boolean;
  error: string | null;
  onClose: () => void;
  onCreate: (request: CreateApplicationRequest) => Promise<void>;
}

const initialForm: CreateApplicationRequest = {
  applicantName: "",
  email: "",
  annualIncome: 120000,
  monthlyExpenses: 3500,
  assetType: "Vehicle · Toyota RAV4",
  assetValue: 65000,
  requestedAmount: 45000,
  termMonths: 48,
  scenario: "auto-approve",
};

export function NewApplicationDrawer({ open, saving, error, onClose, onCreate }: NewApplicationDrawerProps) {
  const [form, setForm] = useState<CreateApplicationRequest>(initialForm);

  if (!open) return null;

  const update = (field: keyof CreateApplicationRequest, value: string) => {
    setForm((current) => ({
      ...current,
      [field]: ["applicantName", "email", "assetType", "scenario"].includes(field)
        ? value
        : Number(value),
    }));
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    await onCreate(form);
  };

  return (
    <div className="drawer-backdrop" role="presentation">
      <section className="application-drawer" aria-labelledby="new-application-heading">
        <div className="drawer-heading">
          <div>
            <h2 id="new-application-heading">New application</h2>
            <p>Use synthetic applicant data only.</p>
          </div>
          <button className="icon-button" type="button" onClick={onClose} aria-label="Close new application form"><X /></button>
        </div>
        <form onSubmit={submit}>
          <div className="form-grid">
            <label>Applicant<input required value={form.applicantName} onChange={(event) => update("applicantName", event.target.value)} placeholder="Full name" /></label>
            <label>Email<input required type="email" value={form.email} onChange={(event) => update("email", event.target.value)} placeholder="name@example.test" /></label>
            <label>Income<span className="money-input"><span>$</span><input required type="number" min="1" value={form.annualIncome} onChange={(event) => update("annualIncome", event.target.value)} /></span></label>
            <label>Expenses<span className="money-input"><span>$</span><input required type="number" min="0" value={form.monthlyExpenses} onChange={(event) => update("monthlyExpenses", event.target.value)} /></span></label>
            <label>Asset type<input required value={form.assetType} onChange={(event) => update("assetType", event.target.value)} /></label>
            <label>Asset value<span className="money-input"><span>$</span><input required type="number" min="1" value={form.assetValue} onChange={(event) => update("assetValue", event.target.value)} /></span></label>
            <label>Requested amount<span className="money-input"><span>$</span><input required type="number" min="1" value={form.requestedAmount} onChange={(event) => update("requestedAmount", event.target.value)} /></span></label>
            <label>Term<select value={form.termMonths} onChange={(event) => update("termMonths", event.target.value)}><option value="36">36 months</option><option value="48">48 months</option><option value="60">60 months</option></select><ChevronDown className="select-icon" aria-hidden="true" /></label>
            <label>Scenario<select value={form.scenario} onChange={(event) => update("scenario", event.target.value)}><option value="auto-approve">Auto approve</option><option value="manual-review">Manual review</option><option value="decline">Decline</option><option value="transient-failure">Retry recovery</option><option value="incident">Create incident</option></select><ChevronDown className="select-icon" aria-hidden="true" /></label>
          </div>
          {error ? <p className="form-error" role="alert">{error}</p> : null}
          <div className="drawer-actions">
            <button className="button button-secondary" type="button" onClick={onClose}>Cancel</button>
            <button className="button button-primary" type="submit" disabled={saving}>{saving ? "Creating…" : "Create application"}</button>
          </div>
        </form>
      </section>
    </div>
  );
}
