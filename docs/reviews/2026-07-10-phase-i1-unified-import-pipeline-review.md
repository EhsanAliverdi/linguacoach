---
title: Phase I1 — Unified Import/Publish Pipeline (Implementation)
date: 2026-07-10
related: I-track (unified content pipeline & legacy retirement), builds on I0 (docs/reviews/2026-07-10-phase-i0-resourcebankitem-physical-consolidation-review.md)
status: complete
---

# Phase I1 — Unified Import/Publish Pipeline

**Date:** 2026-07-10
**Type:** implementation phase, second of the I-track.

**HEAD before work:** `b2cbd757` (Phase I0)

## What changed

Three separate admin pages — Resource Sources, Resource Import Runs, Resource Candidates — are
merged into one: **Import Content** (`/admin/content/import`). This is the pipeline the user
described: paste or upload unstructured content, pick a type, review/approve/publish, all on one
page.

**`AdminContentImportComponent`** (rewritten) now has two sections:
1. **Import** — the existing paste-based form (source name, resource type, default metadata),
   plus a new file-upload mode. File upload needs a `sourceId` (not a name), so a source picker
   was added with an inline "+ New source" option that creates-and-auto-approves a source without
   leaving the page.
2. **Pipeline/review** — shows the candidates for the current or a selected past import run
   (`?importRunId=` query param, or a "recent runs" picker), with inline actions per candidate:
   Preview (ported from the old candidates page's preview drawer), Analyze (AI-advisory +
   re-validate), **Approve & Publish** (new merged action, primary/prominent), Reject.

**New backend action:** `POST api/admin/resource-candidates/{id}/approve-and-publish` — approves
(idempotent) then immediately publishes. `PublishAsync` already re-validates every other gate
live, so approval was the only precondition a separate click used to gate; this collapses two
admin actions into one without skipping any check.

**Source defaults fixed:** `ContentImportService`'s auto-created sources now default
`AllowsStudentDisplay`/`AllowsCommercialUse` to `true` (previously `false`), closing a workflow
trap both the H9B and I0 audits flagged: publish was silently blocked until someone manually
edited the source's license flags on a separate page. Admin-uploaded content is first-party and
still gated by per-candidate approve+publish review, so the old "false by default" caution (meant
for external licensed datasets) didn't apply here.

**Deleted:** `admin-resource-sources/`, `admin-resource-import-runs/`, `admin-resource-candidates/`
component folders; their 3 nav items removed from "Content Ops" (both desktop and mobile-drawer
nav trees). Their routes now redirect to `/admin/content/import` via Angular's `RedirectFunction`
(same pattern as the H9A-era typed-bank-route redirects).

**Backend controllers were NOT physically merged.** `AdminResourceImportController`/
`AdminResourceCandidateController`/`AdminResourceSourceController` all still exist as separate
classes — the frontend's one page now calls all three under the hood. A full controller merge was
scoped out as unnecessary risk for no additional user-visible benefit; the "one unified pipeline"
the user asked for is a one-page admin experience, which this delivers, not a requirement that the
HTTP API surface itself be a single class.

## Validation

- `dotnet build --configuration Release` — clean, 0 errors.
- `dotnet test --configuration Release` — 3,858/3,858 passing (5 architecture, 2,306 unit, 1,547
  integration; +4 new tests for `approve-and-publish`: 401/403/404 + happy path).
- `npm run build -- --configuration production` — no new TS/Angular errors.
- Grep sweep confirmed no dangling references to the deleted pages outside the new redirects.

## What was NOT touched in this phase

`AdminResourceImportController`/`AdminResourceCandidateController`/`AdminResourceSourceController`
(backend classes kept, not merged — see above); `Activity templates`, `Review queue`,
`Placement items`, `Onboarding` nav items (unrelated to the import pipeline, I3's scope);
`ActivityTemplate`/`PracticeActivityCache`/`StudentActivityReadinessItem`/legacy fallback (I2's
scope); Karma unit tests (bundle remains blocked by pre-existing `TODO-H8-2`, unrelated to this
phase — no new `.spec.ts` files were added for the rewritten component for this reason).

## Next steps

I2 (delete the legacy fallback runtime — user has confirmed this proceeds even though it narrows
Today/Practice Gym content to `gap_fill`/`multiple_choice_single` vocab/grammar only until I5
expands coverage) and I3 (final nav consolidation: promote Onboarding/Placement into Content
Studio, remove Activity Templates/Review Queue, land on the 7-page target — Import/Bank/Learn/
Activities/Modules/Onboarding/Placement) remain.

## Explicit confirmations

- No EF migration in this phase (pure application-layer + frontend change).
- No `ActivityTemplate`/`PracticeActivityCache`/`StudentActivityReadinessItem`/runtime session
  entity removal.
- No Today/Practice Gym fallback removal.
- No PG-v2, external datasets, Persian/bilingual content, or direct final-table seeding.
- Committed locally only; not pushed; not deployed; branch unchanged (`main`).
