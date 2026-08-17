import type { ApplicationStatus } from "./types";

const currency = new Intl.NumberFormat("en-AU", {
  style: "currency",
  currency: "AUD",
  maximumFractionDigits: 0,
});

const dateTime = new Intl.DateTimeFormat("en-AU", {
  day: "numeric",
  month: "short",
  year: "numeric",
  hour: "numeric",
  minute: "2-digit",
});

export const formatCurrency = (value: number) => currency.format(value);
export const formatDateTime = (value: string) => dateTime.format(new Date(value));
export const shortReference = (id: string) => `AF-${id.replaceAll("-", "").slice(0, 6).toUpperCase()}`;

export function statusLabel(status: ApplicationStatus): string {
  const labels: Record<ApplicationStatus, string> = {
    Submitted: "Submitted",
    ChecksRunning: "In progress",
    ManualReview: "Manual review",
    Approved: "Approved",
    Declined: "Declined",
    Withdrawn: "Withdrawn",
    StartFailed: "Start failed",
  };
  return labels[status];
}

export function eventLabel(eventType: string): string {
  return eventType
    .toLowerCase()
    .split("_")
    .map((part, index) => index === 0 ? `${part.charAt(0).toUpperCase()}${part.slice(1)}` : part)
    .join(" ");
}
