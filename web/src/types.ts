export type ApplicationStatus =
  | "Submitted"
  | "ChecksRunning"
  | "ManualReview"
  | "Approved"
  | "Declined"
  | "Withdrawn"
  | "StartFailed";

export interface FinanceApplication {
  id: string;
  status: ApplicationStatus;
  assetType: string;
  assetValue: number;
  requestedAmount: number;
  termMonths: number;
  scenario: string;
  createdAt: string;
  updatedAt: string;
}

export interface TimelineEvent {
  sequence: number;
  eventType: string;
  status: ApplicationStatus;
  reasonCode: string;
  occurredAt: string;
}

export interface ApplicationPage {
  items: FinanceApplication[];
  page: number;
  pageSize: number;
  total: number;
}

export interface CreateApplicationRequest {
  applicantName: string;
  email: string;
  annualIncome: number;
  monthlyExpenses: number;
  assetType: string;
  assetValue: number;
  requestedAmount: number;
  termMonths: number;
  scenario: string;
}
