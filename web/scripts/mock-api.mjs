import { createServer } from "node:http";

const baseDate = "2026-08-17T";
let applications = [
  ["11111111-1111-1111-1111-111111111111", "ChecksRunning", "Vehicle · Mazda CX-5", 52000, 38000, "auto-approve", "10:12:00Z"],
  ["22222222-2222-2222-2222-222222222222", "ChecksRunning", "Vehicle · Hyundai Tucson", 47000, 32500, "transient-failure", "09:45:00Z"],
  ["24081700-aaaa-4aaa-8aaa-aaaaaaaaaaaa", "ManualReview", "Vehicle · Toyota RAV4", 65000, 45000, "manual-review", "09:12:00Z"],
  ["44444444-4444-4444-4444-444444444444", "Approved", "Equipment · Ford Ranger", 72000, 55000, "auto-approve", "08:33:00Z"],
  ["55555555-5555-5555-5555-555555555555", "Declined", "Vehicle · Toyota Corolla", 31000, 24000, "decline", "08:18:00Z"],
  ["66666666-6666-6666-6666-666666666666", "Approved", "Equipment · Isuzu D-Max", 79000, 60000, "auto-approve", "07:07:00Z"],
].map(([id, status, assetType, assetValue, requestedAmount, scenario, time]) => ({
  id, status, assetType, assetValue, requestedAmount, scenario,
  termMonths: 48,
  createdAt: `${baseDate}08:41:00Z`,
  updatedAt: `${baseDate}${time}`,
}));

const json = (response, status, body) => {
  response.writeHead(status, { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" });
  response.end(JSON.stringify(body));
};

createServer((request, response) => {
  const url = new URL(request.url, "http://localhost:8080");
  const id = url.pathname.split("/")[3];

  if (request.method === "GET" && url.pathname === "/api/applications") {
    json(response, 200, { items: applications, page: 1, pageSize: 50, total: applications.length });
    return;
  }
  if (request.method === "GET" && url.pathname.endsWith("/timeline")) {
    const selected = applications.find((item) => item.id === id);
    const review = selected?.status === "ManualReview";
    json(response, 200, [
      { sequence: 1, eventType: "APPLICATION_SUBMITTED", status: "Submitted", reasonCode: "SUBMITTED", occurredAt: `${baseDate}08:41:00Z` },
      { sequence: 2, eventType: "CHECKS_COMPLETED", status: "ChecksRunning", reasonCode: "CHECKS_PASSED", occurredAt: `${baseDate}09:03:00Z` },
      ...(review ? [{ sequence: 3, eventType: "MANUAL_REVIEW_REQUIRED", status: "ManualReview", reasonCode: "POLICY_REVIEW", occurredAt: `${baseDate}09:12:00Z` }] : []),
    ]);
    return;
  }
  if (request.method === "POST" && url.pathname.endsWith("/withdraw")) {
    const current = applications.find((item) => item.id === id);
    const updated = { ...current, status: "Withdrawn", updatedAt: new Date().toISOString() };
    applications = applications.map((item) => item.id === id ? updated : item);
    json(response, 200, updated);
    return;
  }
  if (request.method === "POST" && url.pathname === "/api/applications") {
    let raw = "";
    request.on("data", (chunk) => { raw += chunk; });
    request.on("end", () => {
      const body = JSON.parse(raw);
      const created = {
        id: crypto.randomUUID(), status: "ChecksRunning", assetType: body.assetType,
        assetValue: body.assetValue, requestedAmount: body.requestedAmount,
        termMonths: body.termMonths, scenario: body.scenario,
        createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(),
      };
      applications = [created, ...applications];
      json(response, 202, created);
    });
    return;
  }
  json(response, 404, { title: "Not found" });
}).listen(8080, "127.0.0.1", () => console.log("AngleFlow mock API listening on http://127.0.0.1:8080"));
