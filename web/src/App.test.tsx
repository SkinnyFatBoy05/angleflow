import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "./App";

const application = {
  id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  status: "ManualReview",
  assetType: "Vehicle · Toyota RAV4",
  assetValue: 65000,
  requestedAmount: 45000,
  termMonths: 48,
  scenario: "manual-review",
  createdAt: "2026-08-17T08:41:00Z",
  updatedAt: "2026-08-17T09:12:00Z",
};

describe("AngleFlow dashboard", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.endsWith("/timeline")) {
        return Response.json([
          { sequence: 1, eventType: "APPLICATION_SUBMITTED", status: "Submitted", reasonCode: "SUBMITTED", occurredAt: application.createdAt },
          { sequence: 2, eventType: "MANUAL_REVIEW_REQUIRED", status: "ManualReview", reasonCode: "POLICY_REVIEW", occurredAt: application.updatedAt },
        ]);
      }
      if (url.endsWith("/withdraw") && init?.method === "POST") {
        return Response.json({ ...application, status: "Withdrawn" });
      }
      if (url === "/api/applications" && init?.method === "POST") {
        return Response.json({ ...application, id: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", status: "ChecksRunning" }, { status: 202 });
      }
      return Response.json({ items: [application], page: 1, pageSize: 50, total: 1 });
    }));
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it("loads the application queue and selected workflow timeline", async () => {
    render(<App />);

    expect((await screen.findAllByText("Vehicle · Toyota RAV4")).length).toBeGreaterThan(0);
    expect(await screen.findByText("Manual review required")).toBeInTheDocument();
    expect(screen.getByText("Requested")).toBeInTheDocument();
  });

  it("opens the real submission form and posts an idempotent application request", async () => {
    const user = userEvent.setup();
    render(<App />);
    await screen.findAllByText("Vehicle · Toyota RAV4");

    await user.click(screen.getByRole("button", { name: "New application" }));
    await user.type(screen.getByLabelText("Applicant"), "Synthetic Applicant");
    await user.type(screen.getByLabelText("Email"), "synthetic@example.test");
    await user.click(screen.getByRole("button", { name: "Create application" }));

    await waitFor(() => expect(fetch).toHaveBeenCalledWith(
      "/api/applications",
      expect.objectContaining({ method: "POST", headers: expect.objectContaining({ "Idempotency-Key": expect.any(String) }) }),
    ));
  });

  it("withdraws the selected application through the API", async () => {
    const user = userEvent.setup();
    render(<App />);
    await screen.findAllByText("Vehicle · Toyota RAV4");

    await user.click(screen.getByRole("button", { name: "Withdraw" }));

    await waitFor(() => expect(fetch).toHaveBeenCalledWith(
      "/api/applications/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/withdraw",
      expect.objectContaining({ method: "POST" }),
    ));
  });
});
