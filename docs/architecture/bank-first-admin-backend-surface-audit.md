---
status: current
lastUpdated: 2026-07-09 (Phase G0)
owner: architecture
supersedes:
supersededBy:
---

# Bank-First Admin/Backend Surface Audit (Phase G0)

Related roadmap: `docs/roadmap/road-map.md` §1, §19 Decision Log, §19a Phase Sequence.
Related decision: Plan-Sync-G0 (2026-07-09, `7be2c326`).
Related architecture: `docs/architecture/readiness-pool.md`,
`docs/architecture/learning-activity-engine.md`,
`docs/architecture/english-resource-bank-import-platform.md`.

---

## 1. Executive summary

Phase G0 is an **architecture/product audit**, not an implementation cleanup phase. It inventories
every admin page, backend API/controller, background job, and backend lifecycle concept after the
bank-first migration (Resource Banks / Resource Candidates / Activity Templates, Phase E0-E7 plus
the earlier Phase 1-10 and Clean-A/A2 architecture work), and classifies each surface so that
Phase G1/G2/G3 can act on concrete, pre-classified findings rather than re-deriving them.

**No cleanup implementation happened in G0 beyond this audit document and the roadmap/doc updates
that record it.** No routes were renamed, no code surfaces were moved, no pages were deleted, no
migrations were written. The one concrete regression this audit found that would qualify as a
"tiny safe fix" (the Phase E7 reading-passages admin page is routable but missing from the sidebar
nav) is documented as a **G1 safe quick win** rather than fixed in G0, to keep this phase strictly
docs/audit-only.

The audit confirms the Plan-Sync-G0 framing: the per-student readiness/assignment/delivery
lifecycle is **real, load-bearing, and kept** — three background jobs, one pool service, the
student Practice Gym "suggestions" surface, and the admin readiness/repair tooling all depend on
it. What is stale is the **language** around it ("readiness pool", "pool health", "AI-generated
cache" framing), plus the information architecture that still presents AI generation as if it were
the primary content model rather than a fallback/composition/evaluation layer sitting behind the
banks.

Headline counts: **31 admin routes** across 6 nav sections audited; **~20 admin/backend
controllers** relevant to this track audited; **8 background jobs + ~6 core services** audited;
**11 terminology terms** flagged with preferred replacements.

---

## 2. Core decision (restated, unchanged from Plan-Sync-G0)

- **The per-student readiness/assignment lifecycle stays.** `StudentActivityReadinessItem` /
  `IStudentActivityReadinessPoolService` / `ReadinessPoolReplenishmentJob` and the state machine
  (selected → assigned → ready → reserved → completed → expired/stale/failed) are **not deleted**
  and **not retired** in the G-track. They are the delivery/assignment substrate for Today and
  Practice Gym.
- **The old "AI-generated activity cache" framing goes.** Forward-facing docs, admin labels, and
  page titles should describe this surface as a **Student Activity Assignment / Delivery Queue**,
  and describe its health pages as **Today Delivery Health / Assignment Health / Delivery Queue
  Health**.
- **Resource Banks + Resource Candidates + Activity Templates are the primary content model.**
- **AI generation remains** for fallback generation, bank-first composition, evaluation, and
  cost/diagnostics visibility only — not as the primary content source.
- **Legacy generation/cache code paths are not deleted in the G-track.** Their retirement stays in
  Phase F, per-pattern/per-surface, and only after each replacement is proven. G0 does not change
  Phase F scope.

---

## 3. Admin surface inventory

Nav sections today (from `admin-app-layout.component.html`): **Overview, Students, AI System,
Analytics, Content, System**. The "Content" section currently mixes true bank-first content
surfaces (Resource *, Activity templates, Review queue) with lesson-delivery/generation controls
(Lessons) and capability config (Exercise Types) — this mixing is the single biggest IA finding.

Classification legend: **keep** · **rename/reframe** · **move-to-diagnostics** · **merge** ·
**remove-later** · **defer** (until PG-v2 / Phase F). Priority: **P0** misleading/dangerous ·
**P1** confusing not dangerous · **P2** cleanup/naming. Phase: **G1** admin IA · **G2** backend
legacy · **G3** diagnostics consolidation · **F** legacy retirement · **PG-v2** follow-up.

| Route | Current label | Current purpose | Bank-first target purpose | Classification | Priority | Phase |
|---|---|---|---|---|---|---|
| `/admin` | Dashboard | Admin overview/KPIs | Keep; may surface bank-coverage/delivery-queue KPIs later | keep | P2 | G1 |
| `/admin/students` | Students | Student roster | Unchanged | keep | — | — |
| `/admin/students/:id` | (student detail) | Student detail incl. readiness/practice panels | Keep; readiness panel relabelled "Assignment / Delivery Queue" | rename/reframe | P1 | G1 |
| `/admin/create-student` | (create) | Create student | Unchanged | keep | — | — |
| `/admin/ai-config` | AI Config | Provider/model per-feature config | Keep — AI as fallback/composition/eval engine | keep | P2 | G3 |
| `/admin/ai-operations` | AI Operations | Read-only AI ops dashboard (speaking/writing eval, generation quality, usage, readiness-pool/review-scaffold) | Keep; belongs in a consolidated diagnostics area | move-to-diagnostics | P1 | G3 |
| `/admin/prompts` | Prompts | AI prompt registry | Keep — AI engine config | keep | P2 | G3 |
| `/admin/usage` | AI Usage | AI usage/cost tracking | Keep — cost visibility | keep | P2 | G3 |
| `/admin/usage-policies` | Usage Policies | AI usage governance policies | Keep — cost/governance | keep | P2 | G3 |
| `/admin/usage-analytics` | Usage & Analytics | Aggregate usage analytics | Keep | keep | P2 | G3 |
| `/admin/lessons` | Lessons | **Lesson buffer mgmt + generation controls + "readiness pool health"** (subtitle verbatim) | Split: delivery-queue health → diagnostics ("Today Delivery Health"); manual generate-lessons → AI ops; buffer settings → delivery config | rename/reframe + move-to-diagnostics + merge | **P0** | G1 + G3 |
| `/admin/curriculum` | Curriculum | Curriculum objectives | Keep | keep | P2 | — |
| `/admin/exercise-types` | Exercise Types | Exercise/pattern capability registry | Keep, reframe as internal **capability registry** (not student-facing content model) | rename/reframe | P1 | PG-v2 |
| `/admin/onboarding` `/onboarding/:id` | Onboarding | Form.io onboarding templates | Keep (already bank-first/Form.io-native) | keep | — | — |
| `/admin/placement-items` `/:itemId` | Placement items | Form.io placement item bank | Keep (strongest bank-first example) | keep | — | — |
| `/admin/activity-templates` `/:templateId` | Activity templates | Activity Template bank + review/publish | Keep — **primary content model** | keep | — | — |
| `/admin/review-queue` | Review queue | Cross-entity review triage (templates, placement items) | Keep; may extend to resource candidates | keep | P2 | G1 |
| `/admin/resource-sources` | Resource sources | CEFR resource source registry | Keep — **primary content model** | keep | — | — |
| `/admin/resource-import-runs` | Resource import runs | Import staging runs | Keep — **primary content model** | keep | — | — |
| `/admin/resource-candidates` | Resource candidates | Candidate staging/analysis/preview/publish | Keep — **primary content model** | keep | — | — |
| `/admin/resource-banks/vocabulary` | Vocabulary bank | Published vocab bank browse | Keep — **primary content model** | keep | — | — |
| `/admin/resource-banks/grammar` | Grammar bank | Published grammar bank browse | Keep — **primary content model** | keep | — | — |
| `/admin/resource-banks/reading-references` | Reading reference bank | Published short-excerpt bank browse | Keep — **primary content model** | keep | — | — |
| `/admin/resource-banks/reading-passages` | *(no nav item — E7 route only)* | Published full-passage bank browse (Phase E7) | Keep — **primary content model**; **add missing nav item** | keep + **safe quick win** | P1 | G1 |
| `/admin/notifications` | Notifications | Notification platform admin | Unchanged | keep | — | — |
| `/admin/integrations` | Integrations | Storage/integration config | Unchanged | keep | — | — |
| `/admin/diagnostics` | Diagnostics | System status/events/correlation-id lookup | Keep; **anchor of the consolidated diagnostics area** for G3 | keep | P2 | G3 |
| `/admin/security` | Security | Security admin | Unchanged | keep | — | — |
| `/admin/settings/feature-gates` | Feature Gates | Runtime feature-gate/flag config | Keep — hosts the `PracticeGymFormIoPilot` kill switch etc. | keep | P2 | — |

**Admin IA findings (for G1):**

1. **P0 — "Lessons" page conflates three concerns** under a page whose own subtitle says "readiness
   pool health": (a) delivery-queue health, (b) a manual generate-lessons control, (c) lesson
   buffer settings, (d) review-scaffold + mastery-validation diagnostics. This is the surface most
   likely to mislead an admin into thinking a "pool" of AI content is the primary model. Split per
   the table above.
2. **P1 — E7 reading-passages page unreachable from nav.** Route `/admin/resource-banks/reading-passages`
   exists and works, but the sidebar "Content" section lists only vocabulary/grammar/reading-reference
   banks. Adding one `sp-admin-sidebar-nav-item` is a safe quick win (see §9).
3. **P1 — "Content" nav section is overloaded.** It mixes primary content (Resource *, Activity
   templates, Review queue), delivery/generation (Lessons), and capability config (Exercise Types).
   G1 should regroup into e.g. "Content Banks" (resources/templates/review) vs "Delivery & Generation"
   (lessons/delivery health) vs leaving capability config under a "System/Capabilities" area.
4. **P1 — "AI System" section** correctly groups AI config/ops/prompts/usage/policies, but "AI
   Operations" is really a diagnostics dashboard and should feed G3's consolidated diagnostics area.

---

## 4. API / controller inventory

Classification legend: **keep-as-is** · **rename/reframe-later** (route/DTO docs, deferred) ·
**move-under-diagnostics-later** · **merge-later** · **remove-later** (after replacement) ·
**keep-legacy-fallback**. No routes are renamed in G0.

| Controller / key routes | Current responsibility | Bank-first alignment | Classification | Phase |
|---|---|---|---|---|
| `AdminReadinessPoolController` (`/api/admin/students/{id}/readiness-pool[/health]`, `/api/admin/readiness-pool/health`, `/review-scaffold/*`, `/mastery/validation-summary`, `/learning-plan[/progress]`) | Read-only pool inspection + Phase 19B review-scaffold approve/reject/reopen (mutates only `AdminReviewStatus`) | Load-bearing delivery lifecycle; "pool" naming stale | keep-as-is (behaviour); rename/reframe-later (route/DTO docs → "assignment/delivery-queue"); move-under-diagnostics-later (health endpoints) | G2 + G3 |
| `AdminAiOperationsController` (`/api/admin/ai-operations/summary`) | Read-only aggregate over speaking/writing eval, generation quality, AI usage, readiness/review-scaffold | Diagnostics over AI-fallback + delivery | move-under-diagnostics-later | G3 |
| `AdminGenerationController` (`/api/admin/generation/settings`, `/generation/batches[...]`, `/students/{id}/generate-lessons`, `/integrations/storage[...]`) | Generation batch mgmt + manual lesson generation + storage integration | AI generation = fallback/composition, not primary | keep-as-is; rename/reframe-later (frame as "AI fallback/composition generation"); storage endpoints merge-later into Integrations | G1 + G3 |
| `AdminGenerationQualityController` (`/api/admin/generation-quality/summary`) | Read-only generation validation-failure quality summary | Diagnostics over AI generation | move-under-diagnostics-later | G3 |
| `AdminStudentReadinessController` (`/api/admin/students/{id}/readiness/repair[-safe-all]`) | Explicit, idempotent, audited per-student readiness repair | Delivery-lifecycle repair tooling | keep-as-is; rename/reframe-later (→ "assignment/delivery repair"); move-under-diagnostics-later | G3 |
| `AdminStudentPracticeController` (`/api/admin/students/{id}/practice-summary`) | Read-only practice summary | Delivery/practice diagnostics | keep-as-is; move-under-diagnostics-later | G3 |
| `PracticeGymSuggestionsController` (`/api/practice-gym/suggestions[/{readinessItemId}/start|complete]`) | Student-facing: serve/start/complete a suggested readiness item | Delivery-queue consumer; `readinessItemId` is an internal id in the route | keep-as-is; rename/reframe-later (DTO/route docs → "assignment id") | G2 |
| `AdminResourceSourceController` / `AdminResourceImportController` / `AdminResourceBankController` / `AdminActivityTemplateController` / `AdminReviewQueueController` | Resource bank/candidate/template CRUD, import, publish, browse, review | **Primary content model** | keep-as-is | — |
| `AdminPlacementItemController` / `AdminOnboardingController` / `AdminPlacementController` | Form.io placement/onboarding banks | Bank-first native | keep-as-is | — |
| `AdminAiConfigController`* / `AdminPrompts`* / `AiUsageController` / `AdminUsageGovernanceController` / `AdminUsageAnalytics`* | AI engine config, prompts, usage, cost governance | AI = fallback/eval/cost engine | keep-as-is | G3 (grouping only) |
| `AdminSpeakingEvaluationController` / `AdminWritingEvaluationController` | AI evaluation admin | AI evaluation (kept) | keep-as-is; feed diagnostics | G3 |
| `DiagnosticsController` | System status/events/correlation-id | Diagnostics anchor | keep-as-is | G3 |
| `AdminCurriculumController` / `AdminExerciseTypes`* | Curriculum objectives / exercise-pattern capability registry | Capability config, not content model | keep-as-is; reframe (exercise types → capability registry) | PG-v2 |
| `ActivityController` / `SessionsController` / `StudentDashboardController` etc. | Student runtime | Consumers of delivery + banks | keep-as-is | — |

*Names inferred from the routing/nav; exact controller class names for a few AI/exercise-type/usage-analytics
pages were not each opened line-by-line in G0 — a full endpoint-by-endpoint sweep is G2's own first task.
This is a known G0 limitation (see §11).

**No route renames or DTO renames are recommended for G0.** All rename/reframe items above are
explicitly deferred to G2 (backend) or G1 (labels), because renaming a live route/DTO is a
non-trivial, higher-blast-radius change that needs its own tested change, not an audit-pass edit.

---

## 5. Background job / service inventory

| Job / service | Current responsibility | Bank-first aligned? | Legacy fallback? | Rename/reframe later? | Move to diagnostics? | Retire only after replacement proven? |
|---|---|---|---|---|---|---|
| `ReadinessPoolReplenishmentJob` + `IReadinessPoolReplenishmentService` | Expire stale/reserved items, recover orphaned generating items, retry failed, fill shortfalls, prevent duplicate generation | Yes — this IS the delivery/assignment lifecycle engine | No | Yes → "Assignment / Delivery Queue replenishment" | Health output → diagnostics | **No — kept, load-bearing** |
| `LessonBufferRefillJob` | Find students below ready-lesson buffer threshold, enqueue `LessonBatchGenerationJob` | Delivery buffering | No | Yes → "delivery buffer refill" | — | No — kept |
| `LessonBatchGenerationJob` | Ask AI for next batch of session plans, validate JSON, create `GenerationBatch`, materialize sessions | AI **fallback/composition** for Today; not primary content | Partial (legacy freeform where no bank content) | Yes → frame as AI fallback/composition | — | Legacy freeform portion → Phase F, per-pattern, after bank-first proven |
| `ActivityMaterializationJob` | Generate `LearningActivity` for each `SessionExercise`; **tries `TodayBankResourceSelector` first (D1/D2)**, falls back to legacy AI generation | **Bank-first for Vocabulary/Reading**, legacy elsewhere | Partial | Minor (docs) | — | Legacy fallback → Phase F, per-pattern |
| `PracticeGymBufferRefillJob` | Maintain Practice Gym ready-activity cache per student×pattern via `PracticeActivityCache` | Delivery buffering | Uses cache table | Yes → "practice delivery buffer" | — | **Defer to PG-v2** (cache may shrink/be removed after PG-v2) |
| `PracticeGymGenerationJob` | Materialize queued Practice Gym cache entries; **tries approved `ActivityTemplate` first (C1-C3)**, falls back to freeform | **Bank-first for 8 template-enabled patterns**, legacy for 25 | Partial | Minor (docs) | — | Legacy fallback → Phase F, per-pattern |
| `TodayBankResourceSelector` / `ResourceCandidate*` services / `ResourceBankQueryService` | Select published bank content; import/analyze/validate/publish/browse | **Yes — core bank-first** | No | No | — | **No — primary model** |
| `StudentReadinessAuditService` / repair services | Read-only per-student delivery audit (~20 checks) + explicit idempotent repair | Delivery-lifecycle diagnostics/repair | No | Yes → "assignment/delivery audit" | Yes → diagnostics | No — kept |
| `GenerationValidationFailurePruneJob` | Prune old generation-validation-failure rows | Housekeeping over AI generation | No | No | Output → diagnostics | No — kept |

**Special focus (as instructed):** `StudentActivityReadinessItem` is classified **keep, reframe as
Student Activity Assignment / Delivery Queue** — never delete. The `PracticeActivityCache`
queue-slot mechanism is classified **defer until PG-v2** (audited later; may shrink or be removed
after PG-v2, not touched now).

---

## 6. Terminology replacement table

Search basis: 55 occurrences of readiness/pool/cache-family terms across 14 frontend files, plus
backend `ReadinessPool*` namespaces and the `docs/` set. Most **student-facing** occurrences are
internal identifiers (`readinessItemId`, `courseReadiness.lifecycleStatus`) rather than visible
labels — the misleading language is concentrated in **admin labels** (esp. the "Lessons" page) and
**docs/code comments**.

| Term (current) | Where | Keep / replace / keep-internal | Preferred replacement | Notes |
|---|---|---|---|---|
| readiness pool | admin "Lessons" page labels, backend namespace `Application.ReadinessPool` | replace (admin labels) / keep-internal (namespace) | Student Activity Assignment · Delivery Queue | Renaming the C# namespace is risky/wide → G2 only if proven worth it; labels are cheap |
| pool health | admin "Lessons" page, `AdminReadinessPoolController` health endpoints | replace (labels) | Today Delivery Health · Assignment Health · Delivery Queue Health | Move under diagnostics (G3) |
| generated pool / pre-generated activities | docs, historical framing | replace (forward-facing) / keep (historical log entries) | bank-first delivery · assigned activities | Historical decision-log entries stay as accurate history |
| activity cache / generated cache / AI-generated cache | forward-facing framing | replace | Delivery Queue · assigned activity · (AI Fallback Generation where about the AI path) | The reframe that motivates the whole G-track |
| lesson readiness | admin "Lessons" page | replace | Today Delivery Health | Same surface as pool health |
| practice cache / `PracticeActivityCache` | `PracticeGymBufferRefillJob`, entity | keep-internal for now | practice delivery buffer (label only) | Entity rename deferred to PG-v2; do not rename now |
| materialization / `ActivityMaterializationJob` | jobs | keep-internal | (keep) | Accurate technical term; renaming has no user benefit and high blast radius |
| replenishment / `ReadinessPoolReplenishmentJob` | jobs/services | keep-internal; reframe in docs | delivery-queue replenishment (in prose) | Code name kept; prose reframed |
| suggestions (Practice Gym) | `PracticeGymSuggestionsController`, student UI | keep | (keep) — or "recommended practice" per PG-v2 | Revisit under PG-v2 UX |
| Bank Resource Selection / Bank-First Composition | `TodayBankResourceSelector` | keep — already correct | (keep) | Preferred vocabulary already in use |
| AI Fallback Generation / AI Evaluation / AI Cost/Diagnostics | AI System nav + ops | adopt in labels | (as written) | Frames AI as fallback/eval/cost, not primary |

Preferred forward-facing vocabulary (canonical): **Student Activity Assignment · Delivery Queue ·
Today Delivery Health · Assignment Health · Delivery Queue Health · Bank Resource Selection ·
Bank-First Composition · AI Fallback Generation · AI Evaluation · AI Cost/Diagnostics · Resource
Bank · Resource Candidate · Activity Template.**

---

## 7. Recommended phase breakdown

- **Phase G1 — Admin Information Architecture Cleanup.** Split the "Lessons" page (P0); add the
  missing reading-passages nav item (safe quick win); regroup the overloaded "Content" nav section
  into content-banks vs delivery/generation; relabel the student-detail readiness panel and the
  pool-health labels to assignment/delivery-queue language; reframe "Exercise Types" as a
  capability registry (surface label only; deeper work is PG-v2). Labels/nav/page-composition only
  — no route or DTO renames.
- **Phase G2 — Backend Legacy Surface Cleanup.** Full endpoint-by-endpoint sweep (completing §4's
  inferred rows); reframe route/DTO **docs** for readiness→assignment/delivery; decide whether the
  `Application.ReadinessPool` namespace and `PracticeActivityCache` names are worth renaming (only
  if proven low-risk); remove genuinely dead admin API surface if any is found. No behavioural
  change to the delivery lifecycle.
- **Phase G3 — Delivery/Bank/AI Diagnostics Consolidation.** Consolidate the "keep as diagnostics"
  pieces — `AdminAiOperationsController`, `AdminGenerationQualityController`, pool/delivery health
  endpoints, `StudentReadinessAuditService` output, generation-validation-failure summaries — into
  one coherent diagnostics area anchored on the existing `/admin/diagnostics` page. AI cost/usage
  visibility grouped here too.
- **Phase F — Legacy retirement (unchanged scope).** Per-pattern retirement of the legacy freeform
  `IAiActivityGenerator` fallback in `ActivityMaterializationJob` / `PracticeGymGenerationJob` /
  `LessonBatchGenerationJob`, only after each pattern's bank-first replacement is proven. G0 does
  not change this.
- **PG-v2 follow-up.** `PracticeActivityCache` shrink/removal; `ExerciseTypeDefinition` /
  `ExercisePatternDefinition` full reframe as internal capability registry; Practice Gym
  suggestions → skill/objective-first UX.

---

## 8. Do-not-delete list (G-track)

- `StudentActivityReadinessItem` (entity, config, DbSet) — kept, reframed only.
- `IStudentActivityReadinessPoolService` / `IReadinessPoolReplenishmentService` /
  `ReadinessPoolReplenishmentJob` — the delivery/assignment lifecycle engine.
- `LessonBufferRefillJob` / `LessonBatchGenerationJob` / `ActivityMaterializationJob` — Today
  delivery + generation (legacy fallback retires only in Phase F, per-pattern).
- `PracticeGymBufferRefillJob` / `PracticeGymGenerationJob` / `PracticeActivityCache` — Practice
  Gym delivery (cache change deferred to PG-v2).
- Legacy freeform `IAiActivityGenerator` path — Phase F only, after replacement proven.
- `AdminReadinessPoolController` / `AdminStudentReadinessController` / repair services — kept
  (reframed/moved to diagnostics, not removed).
- All Resource Bank / Resource Candidate / Activity Template / placement / onboarding surfaces —
  primary content model.

---

## 9. Safe quick wins (candidates for G1; NOT done in G0)

1. **Add the missing sidebar nav item** for `/admin/resource-banks/reading-passages` ("Reading
   passage bank") in the "Content" section of `admin-app-layout.component.html`, alongside the
   existing three bank items. One-line addition mirroring the existing pattern; the page, route,
   API, and tests already exist from Phase E7. **Deferred out of G0 to keep this phase docs-only.**
2. **Relabel** the "Lessons" page subtitle and the "Readiness pool — aggregate health" card
   headings to delivery-queue language (label-only string edits, no logic change).
3. **Relabel** the student-detail readiness panel heading to "Assignment / Delivery Queue".

These are labelled "safe" because each is a localized string/nav edit with existing test coverage
and no lifecycle-behaviour change — but even these belong to G1 (an implementation phase), not G0
(audit). G0 deliberately implements none of them.

---

## 10. Risky / deferred changes

- **Renaming the `Application.ReadinessPool` C# namespace / `PracticeActivityCache` entity** —
  wide blast radius across backend + tests + migrations; only if G2 proves it low-risk.
- **Renaming live API routes/DTOs** (`/api/admin/readiness-pool/*`, `readinessItemId`) — external
  contract change; deferred to G2 with its own tested change.
- **Removing `PracticeActivityCache`** — depends on PG-v2's selector replacing the cache; not
  before.
- **Retiring any legacy freeform generation path** — Phase F only, per-pattern, after proof.
- **Splitting/moving the "Lessons" page** — touches a real, in-use admin page with multiple data
  sources; G1 should do it as a tested change, not a blind move.

---

## 11. Open questions / known G0 limitations

1. G0 inferred (did not open line-by-line) the exact controller class names and full endpoint lists
   for a handful of AI/exercise-type/usage-analytics pages — G2's first task is a complete
   endpoint-by-endpoint sweep to confirm §4's inferred rows.
2. Should the "Content" nav split be two groups (Content Banks / Delivery & Generation) or three
   (adding a Capabilities group for Exercise Types)? Left for G1 with product input.
3. Should AI cost/usage live in "AI System" or move fully into the consolidated diagnostics area
   (G3)? Recommend a single diagnostics home, but this is a product/IA call for G1/G3.
4. Is a namespace/entity rename (readiness→assignment) worth the churn, or should the reframe stay
   label-and-docs-only with internal code names preserved? G0's recommendation: **labels + docs
   first; code-name renames only if G2 proves them cheap and safe.**
5. Practice Gym "suggestions" vocabulary vs PG-v2's "recommended practice" — align in PG-v2, not
   piecemeal now.

---

## Documentation impact

- Docs reviewed (Phase G0): admin routing (`app.routes.ts`), admin nav
  (`admin-app-layout.component.html`), `admin-lessons.component.*`, `admin-diagnostics.component.*`,
  `AdminReadinessPoolController`, `AdminAiOperationsController`, `AdminGenerationController`,
  `AdminGenerationQualityController`, `AdminStudentReadinessController`, `AdminStudentPracticeController`,
  `PracticeGymSuggestionsController`, the `Jobs/` set (`ReadinessPoolReplenishmentJob`,
  `LessonBufferRefillJob`, `LessonBatchGenerationJob`, `ActivityMaterializationJob`,
  `PracticeGymBufferRefillJob`, `PracticeGymGenerationJob`, `GenerationValidationFailurePruneJob`),
  and the readiness/cache terminology across `src/LinguaCoach.Web/src/app`. All via read-only
  inspection — no app code changed.
- Docs created (Phase G0): this file.
- Docs updated (Phase G0): `docs/roadmap/road-map.md`, `docs/sprints/current-sprint.md`,
  `docs/architecture/README.md`, `docs/backlog/product-backlog.md`,
  `docs/architecture/readiness-pool.md`, `docs/architecture/learning-activity-engine.md`.
- Docs intentionally not updated: `docs/architecture/english-resource-bank-import-platform.md` — its
  E-track scope is unchanged by this audit; Plan-Sync-G0 already added its forward reference.
- Reason: Phase G0 is an audit/planning deliverable — no app code, migrations, or config changed;
  this inventory + classification is the actual output this phase produces, and it feeds
  Phase G1/G2/G3.
