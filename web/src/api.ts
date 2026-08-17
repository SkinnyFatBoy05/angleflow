import type { ApplicationPage, CreateApplicationRequest, FinanceApplication, TimelineEvent } from "./types";

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, init);
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { title?: string; detail?: string } | null;
    throw new Error(problem?.detail ?? problem?.title ?? `Request failed with ${response.status}`);
  }
  return response.json() as Promise<T>;
}

export function getApplications(): Promise<ApplicationPage> {
  return request<ApplicationPage>("/api/applications?page=1&pageSize=50");
}

export function getTimeline(id: string): Promise<TimelineEvent[]> {
  return request<TimelineEvent[]>(`/api/applications/${id}/timeline`);
}

export function createApplication(payload: CreateApplicationRequest): Promise<FinanceApplication> {
  return request<FinanceApplication>("/api/applications", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Idempotency-Key": globalThis.crypto?.randomUUID?.() ?? `angleflow-${Date.now()}`,
    },
    body: JSON.stringify(payload),
  });
}

export function withdrawApplication(id: string): Promise<FinanceApplication> {
  return request<FinanceApplication>(`/api/applications/${id}/withdraw`, { method: "POST" });
}
