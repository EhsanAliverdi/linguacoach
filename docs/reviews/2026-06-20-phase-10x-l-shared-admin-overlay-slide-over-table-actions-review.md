# Phase 10X-L — Shared Admin Overlay, Slide-Over, and Table Action Fixes

**Date:** 2026-06-20
**Sprint:** Phase 10X-L
**Related feature:** Admin student management UI — shared component fixes
**Files reviewed:**
- `src/LinguaCoach.Web/src/app/admin/components/slide-over/sp-admin-slide-over.component.ts`
- `src/LinguaCoach.Web/src/app/admin/components/slide-over/sp-admin-slide-over.component.spec.ts`
- `src/LinguaCoach.Web/src/app/admin/components/table-actions/sp-admin-table-actions.component.ts`
- `src/LinguaCoach.Web/src/app/admin/components/table-actions/sp-admin-table-actions.component.spec.ts` (new)
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts`

---

## Findings by priority

### Critical — fixed

**1. sp-admin-slide-over z-index too low**
- Previous z-index: backdrop 400, panel 401.
- Admin header and sidebar use Tailwind z-index classes (typically z-30 to z-50, i.e. 30–50 in px scale, but TailAdmin uses z-[99] for header).
- Fixed: backdrop now `1000 + stackIndex*50`, panel `1001 + stackIndex*50`.
- Applied via `[style.z-index]` bindings on the backdrop div and aside element. CSS static fallback also raised to 1000/1001.

**2. closeOnBackdrop defaulted to true — unexpected behaviour**
- Backdrop clicks could dismiss panels mid-edit unexpectedly.
- Fixed: default changed to `false`. Callers must explicitly opt in with `[closeOnBackdrop]="true"`.
- All existing callers were using the component without setting this input, so they now benefit from the safer default.

**3. Set CEFR used a centered modal div, not sp-admin-slide-over**
- The `settingCefr()` flow rendered a raw `.sp-admin-modal` + `.sp-admin-modal-backdrop` pair.
- Converted to `sp-admin-slide-over` with `size="sm"` and `[stackIndex]="1"`.
- All form fields, service calls (`updateStudentCefr`), error handling, and refresh behavior preserved exactly.

**4. Assign policy used a centered modal div, not sp-admin-slide-over**
- Same pattern as Set CEFR. Converted to `sp-admin-slide-over` with `size="sm"` and `[stackIndex]="1"`.
- All form fields, service calls (`assignStudentPolicy`), error handling, and refresh behavior preserved exactly.

**5. Table action dropdown caused vertical scroll**
- Root cause: dropdown used `position: absolute` inside a table cell. When the table container has `overflow: auto` or `overflow: hidden`, the absolute dropdown is clipped and the viewport scrolls to accommodate.
- Fix: dropdown now uses `position: fixed` with coordinates computed from `triggerRef.nativeElement.getBoundingClientRect()`. The menu escapes all overflow ancestors.
- Flip-up logic: if less than 200px below the trigger, menu opens upward.
- Right-aligned with the trigger button; clamped to viewport left edge.
- Window scroll closes the menu to keep position accurate.

### Medium — addressed

**6. Stacked slide-over support missing**
- Added `@Input() stackIndex = 0` to `sp-admin-slide-over`.
- First panel: `stackIndex=0` (z 1000/1001). Second panel stacked on top: `stackIndex=1` (z 1050/1051). Each step adds 50 units.
- CEFR and Assign policy slide-overs use `stackIndex=1` since they may appear over the page content while another slide-over is open.

**7. Accessibility gap in sp-admin-slide-over**
- Panel already had `role="dialog"`, `aria-modal="true"`, `[attr.aria-label]="title"`, and close button `aria-label="Close panel"`.
- No changes needed; confirmed correct in spec.

### Low — noted

**8. Student detail visual consistency**
- Component uses internal CSS classes (`sp-admin-link-button`, `sp-admin-danger-link`, `sp-admin-btn-primary`, `sp-button-ghost`) defined within its own `styles` array.
- These are applied consistently throughout the component.
- `sp-admin-button` wrapper component exists but targets newer pages with full Tailwind theming. Migrating all buttons here is a redesign, not a consistency fix, and is out of scope for 10X-L.
- No changes made to button rendering.

---

## Decisions made

| Decision | Rationale |
|---|---|
| closeOnBackdrop default → false | Prevents accidental data loss from stray clicks |
| z-index base 1000 | Comfortably above TailAdmin header (z-[99]) and sidebar (z-[300] max) |
| stackIndex step 50 | Gives enough room for future layers without reaching browser limits |
| Table actions: position:fixed + getBoundingClientRect | Escapes overflow without CDK dependency; simpler, testable |
| Close on window scroll | Prevents stale menu position when user scrolls |
| Assign policy: converted to slide-over | Simple form, low risk |
| Edit student / Reset password / Reset data / Lifecycle: left as modal | These are high-stakes destructive actions; modal-style is intentional for focus/danger emphasis; out of scope for 10X-L |

---

## Implementation tasks produced

All tasks completed in this sprint. No deferred implementation tasks.

---

## Risks / unresolved questions

- Table action menu position assumes the trigger button is visible in the viewport when clicked. If the table is inside a virtual scroll container that moves items in/out of the DOM, the computed position may be stale. This is not currently a concern (no virtual scroll in admin tables).
- If Angular CDK is added in a future sprint, consider migrating table-actions to `CdkOverlay` for a more robust portal solution.
- Edit student / Reset password / Reset data modals still use the old `.sp-admin-modal` pattern. These can be converted in a future sprint.

---

## Final verdict

All four tasks (A/B/C/D) completed. Build and all 791 tests pass. No product behaviour changed; only overlay/container mechanism changed for CEFR and Assign Policy flows.

## Next recommended action

Update graphify (`graphify update .`), then proceed to next sprint item.
