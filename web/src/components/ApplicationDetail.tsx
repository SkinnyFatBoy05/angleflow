import { LoaderCircle } from "lucide-react";
import { eventLabel, formatCurrency, formatDateTime, shortReference, statusLabel } from "../format";
import type { FinanceApplication, TimelineEvent } from "../types";
import { StatusMark } from "./StatusMark";

interface ApplicationDetailProps {
  application: FinanceApplication | null;
  timeline: TimelineEvent[];
  timelineLoading: boolean;
  withdrawing: boolean;
  onWithdraw: () => void;
}

const terminalStatuses = new Set(["Approved", "Declined", "Withdrawn"]);

export function ApplicationDetail({ application, timeline, timelineLoading, withdrawing, onWithdraw }: ApplicationDetailProps) {
  if (!application) {
    return (
      <aside className="detail-panel empty-detail" aria-label="Application detail">
        <p>Select an application to inspect its workflow.</p>
      </aside>
    );
  }

  return (
    <aside className="detail-panel" aria-label={`Application ${shortReference(application.id)} detail`}>
      <div className="detail-heading">
        <h2>Application {shortReference(application.id)}</h2>
        {!terminalStatuses.has(application.status) ? (
          <button className="button button-secondary" type="button" onClick={onWithdraw} disabled={withdrawing}>
            {withdrawing ? "Withdrawing…" : "Withdraw"}
          </button>
        ) : null}
      </div>

      <section className="detail-section" aria-labelledby="summary-heading">
        <h3 id="summary-heading">Summary</h3>
        <div className="finance-summary">
          <div><span>Requested</span><strong>{formatCurrency(application.requestedAmount)}</strong></div>
          <div><span>Term</span><strong>{application.termMonths} months</strong></div>
          <div><span>Asset value</span><strong>{formatCurrency(application.assetValue)}</strong></div>
        </div>
        <dl className="metadata-list">
          <div><dt>Applicant</dt><dd>{shortReference(application.id)}</dd></div>
          <div><dt>Asset</dt><dd>{application.assetType}</dd></div>
          <div><dt>Status</dt><dd><span className="status"><StatusMark status={application.status} />{statusLabel(application.status)}</span></dd></div>
          <div><dt>Scenario</dt><dd className="capitalize">{application.scenario.replaceAll("-", " ")}</dd></div>
          <div><dt>Submitted</dt><dd>{formatDateTime(application.createdAt)}</dd></div>
        </dl>
      </section>

      <section className="detail-section timeline-section" aria-labelledby="timeline-heading">
        <h3 id="timeline-heading">Workflow timeline</h3>
        {timelineLoading ? <div className="timeline-loading"><LoaderCircle className="spin" aria-hidden="true" /> Loading timeline</div> : (
          <ol className="timeline">
            {timeline.map((event, index) => (
              <li key={event.sequence} className={index === timeline.length - 1 ? "current" : "complete"}>
                <span className="timeline-node" aria-hidden="true" />
                <div>
                  <strong>{eventLabel(event.eventType)}</strong>
                  <span>{formatDateTime(event.occurredAt)}</span>
                  <small>{event.reasonCode.replaceAll("_", " ").toLowerCase()}</small>
                </div>
              </li>
            ))}
          </ol>
        )}
      </section>
    </aside>
  );
}
