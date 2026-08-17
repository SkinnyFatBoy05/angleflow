# AngleFlow dashboard implementation spec

The accepted visual reference is `angleflow-dashboard-concept.png` (1536 × 1024).

## Design system

- True-white content canvas with a deep ink (`#06142b`) top bar and cobalt (`#0866f5`) primary accent.
- Success is mint (`#17b890`); manual review is amber (`#ee9b00`); borders are cool hairlines (`#d9e0ea`).
- Typography uses a crisp system grotesk stack, 13–15 px control/table text, 28–32 px page title, and tabular numerals.
- Spacing follows a 4/8 px scale. Controls use 8 px corners; rows and detail sections remain open rather than nested in cards.
- Icons are outline icons at 18–20 px with consistent optical weight.

## Component inventory

- App shell: quiet brand/navigation bar and one primary action.
- Application queue: summary filter tabs, accessible data table, selected row, empty/loading/error states.
- Application detail rail: status, finance summary, metadata, withdrawal action, vertical audit timeline.
- Submission drawer: nine labeled fields, scenario selector, cancel/create actions, pending/error/success feedback.

## Responsive behavior

At tablet widths, navigation labels compact. Below 900 px, the split view becomes a single column with the selected detail after the queue. The table can scroll horizontally without clipping controls; the form changes from four columns to one.

## Copy lock

Above the fold: `AngleFlow`, `Applications`, `Workflow`, `Operations`, `New application`, `All`, `In progress`, `Manual review`, `Completed`, `Applicant`, `Asset`, `Amount`, `Status`, `Updated`, `Summary`, `Withdraw`, `Workflow timeline`.

Applicant names and email are intentionally not returned by the unauthenticated portfolio API. The implementation uses the public application reference in their place; this is a privacy-preserving deviation from the visual concept.
