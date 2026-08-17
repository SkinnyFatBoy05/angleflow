import type { ApplicationStatus } from "../types";

export function StatusMark({ status }: { status: ApplicationStatus }) {
  return <span className={`status-mark status-${status.toLowerCase()}`} aria-hidden="true" />;
}
