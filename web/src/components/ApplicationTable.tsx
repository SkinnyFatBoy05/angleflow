import { ArrowDown, Inbox } from "lucide-react";
import { formatCurrency, formatDateTime, shortReference, statusLabel } from "../format";
import type { FinanceApplication } from "../types";
import { StatusMark } from "./StatusMark";

interface ApplicationTableProps {
  applications: FinanceApplication[];
  selectedId: string | null;
  onSelect: (application: FinanceApplication) => void;
}

export function ApplicationTable({ applications, selectedId, onSelect }: ApplicationTableProps) {
  if (applications.length === 0) {
    return (
      <div className="empty-state">
        <Inbox aria-hidden="true" />
        <h2>No applications yet</h2>
        <p>Create a synthetic application to start the Camunda workflow.</p>
      </div>
    );
  }

  return (
    <div className="table-scroll">
      <table className="application-table">
        <thead>
          <tr>
            <th>Applicant</th>
            <th>Asset</th>
            <th>Amount</th>
            <th>Status</th>
            <th className="updated-heading">Updated <ArrowDown aria-hidden="true" /></th>
          </tr>
        </thead>
        <tbody>
          {applications.map((application) => (
            <tr
              key={application.id}
              className={selectedId === application.id ? "selected" : undefined}
              onClick={() => onSelect(application)}
            >
              <td>
                <button className="row-select" type="button" onClick={() => onSelect(application)}>
                  <span className="application-reference">{shortReference(application.id)}</span>
                  <span className="scenario-label">{application.scenario.replaceAll("-", " ")}</span>
                </button>
              </td>
              <td>{application.assetType}</td>
              <td className="tabular">{formatCurrency(application.requestedAmount)}</td>
              <td><span className="status"><StatusMark status={application.status} />{statusLabel(application.status)}</span></td>
              <td className="updated-cell">{formatDateTime(application.updatedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
