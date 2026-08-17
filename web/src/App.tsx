import { useCallback, useEffect, useMemo, useState } from "react";
import { createApplication, getApplications, getTimeline, withdrawApplication } from "./api";
import { ApplicationDetail } from "./components/ApplicationDetail";
import { ApplicationTable } from "./components/ApplicationTable";
import { AppHeader } from "./components/AppHeader";
import { NewApplicationDrawer } from "./components/NewApplicationDrawer";
import type { ApplicationStatus, CreateApplicationRequest, FinanceApplication, TimelineEvent } from "./types";
import "./styles.css";

type Filter = "all" | "progress" | "review" | "completed";

const filterLabels: Array<{ value: Filter; label: string }> = [
  { value: "all", label: "All" },
  { value: "progress", label: "In progress" },
  { value: "review", label: "Manual review" },
  { value: "completed", label: "Completed" },
];

const progressStatuses = new Set<ApplicationStatus>(["Submitted", "ChecksRunning", "StartFailed"]);
const completedStatuses = new Set<ApplicationStatus>(["Approved", "Declined", "Withdrawn"]);

export default function App() {
  const [applications, setApplications] = useState<FinanceApplication[]>([]);
  const [selected, setSelected] = useState<FinanceApplication | null>(null);
  const [timeline, setTimeline] = useState<TimelineEvent[]>([]);
  const [filter, setFilter] = useState<Filter>("all");
  const [loading, setLoading] = useState(true);
  const [timelineLoading, setTimelineLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [withdrawing, setWithdrawing] = useState(false);

  const loadApplications = useCallback(async () => {
    setLoading(true);
    try {
      const page = await getApplications();
      setApplications(page.items);
      setSelected((current) => page.items.find((item) => item.id === current?.id) ?? page.items[0] ?? null);
      setError(null);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Unable to load applications.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void loadApplications(); }, [loadApplications]);

  useEffect(() => {
    if (!selected) {
      setTimeline([]);
      return;
    }
    let active = true;
    setTimelineLoading(true);
    void getTimeline(selected.id)
      .then((events) => { if (active) setTimeline(events); })
      .catch((requestError: unknown) => { if (active) setError(requestError instanceof Error ? requestError.message : "Unable to load timeline."); })
      .finally(() => { if (active) setTimelineLoading(false); });
    return () => { active = false; };
  }, [selected]);

  const counts = useMemo(() => ({
    all: applications.length,
    progress: applications.filter((item) => progressStatuses.has(item.status)).length,
    review: applications.filter((item) => item.status === "ManualReview").length,
    completed: applications.filter((item) => completedStatuses.has(item.status)).length,
  }), [applications]);

  const filtered = useMemo(() => applications.filter((application) => {
    if (filter === "progress") return progressStatuses.has(application.status);
    if (filter === "review") return application.status === "ManualReview";
    if (filter === "completed") return completedStatuses.has(application.status);
    return true;
  }), [applications, filter]);

  const handleCreate = async (request: CreateApplicationRequest) => {
    setSaving(true);
    setFormError(null);
    try {
      const created = await createApplication(request);
      await loadApplications();
      setSelected(created);
      setDrawerOpen(false);
    } catch (requestError) {
      setFormError(requestError instanceof Error ? requestError.message : "Unable to create application.");
    } finally {
      setSaving(false);
    }
  };

  const handleWithdraw = async () => {
    if (!selected) return;
    setWithdrawing(true);
    try {
      const updated = await withdrawApplication(selected.id);
      setApplications((current) => current.map((item) => item.id === updated.id ? updated : item));
      setSelected(updated);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Unable to withdraw application.");
    } finally {
      setWithdrawing(false);
    }
  };

  return (
    <div className="app-shell">
      <AppHeader onNewApplication={() => setDrawerOpen(true)} />
      <main id="main" className="workspace">
        <section id="applications" className="queue-panel" aria-labelledby="applications-heading">
          <div className="queue-heading">
            <div>
              <h1 id="applications-heading">Applications</h1>
            </div>
            <button className="button button-primary mobile-new" type="button" onClick={() => setDrawerOpen(true)}>New application</button>
          </div>
          <div className="filter-tabs" role="tablist" aria-label="Application status">
            {filterLabels.map((item) => (
              <button
                key={item.value}
                className={filter === item.value ? "active" : undefined}
                type="button"
                role="tab"
                aria-selected={filter === item.value}
                onClick={() => setFilter(item.value)}
              >
                {item.label}<span>{counts[item.value]}</span>
              </button>
            ))}
          </div>
          {error ? <div className="error-banner" role="alert"><span>{error}</span><button type="button" onClick={() => void loadApplications()}>Retry</button></div> : null}
          {loading ? <div className="loading-state" aria-live="polite">Loading applications…</div> : (
            <ApplicationTable applications={filtered} selectedId={selected?.id ?? null} onSelect={setSelected} />
          )}
        </section>
        <ApplicationDetail application={selected} timeline={timeline} timelineLoading={timelineLoading} withdrawing={withdrawing} onWithdraw={() => void handleWithdraw()} />
      </main>
      <NewApplicationDrawer open={drawerOpen} saving={saving} error={formError} onClose={() => setDrawerOpen(false)} onCreate={handleCreate} />
    </div>
  );
}
