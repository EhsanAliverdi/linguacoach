---
status: current
lastUpdated: 2026-06-18 23:30
owner: engineering
sprint: Phase 10X-D
---

# Phase 10X-D — TailAdmin Template Import & Adapter Plan — Engineering Review

**Date:** 2026-06-18
**Phase:** 10X-D
**HEAD before work:** 476b6a3 (Phase 10X-C-F gate closure)

---

## Scope

Import the free TailAdmin Angular template as a vendor reference under `src/app/templates/tailadmin/`.
Create the adapter inventory document mapping TailAdmin patterns to `sp-admin-*` wrappers.
Remove the stale `admin-template/tailadmin/README.md` placeholder.
Document the new template source layer in architecture docs.

---

## Files Changed

### Added
- `src/LinguaCoach.Web/src/app/templates/README.md`
- `src/LinguaCoach.Web/src/app/templates/tailadmin/README.md`
- `src/LinguaCoach.Web/src/app/templates/tailadmin/free-angular-tailwind-dashboard/` (gitignored vendor source)
- `docs/architecture/admin-tailadmin-adapter-inventory.md`

### Modified
- `src/LinguaCoach.Web/.gitignore` — added `src/app/templates/` exclusion
- `docs/architecture/admin-ui-design-system.md` — updated visual source of truth, folder structure, vendor source location, closed TODO-10X-ASSETS
- `docs/sprints/current-sprint.md` — recorded 10X-D as active sprint
- `docs/handoffs/current-product-state.md` — updated admin UI state
- `TODOS.md` — closed TODO-10X-ASSETS, added TODO-10X-D-MODAL, updated TODO-10X-E/F

### Removed
- `src/LinguaCoach.Web/src/app/admin-template/tailadmin/README.md` (stale placeholder)
- `src/LinguaCoach.Web/src/app/admin-template/` folder

---

## TailAdmin Source

- **URL:** https://github.com/TailAdmin/free-angular-tailwind-dashboard
- **Commit:** da992cf — "update README with new AI settings, map components, and additional chart layouts"
- **Import date:** 2026-06-18
- **License:** MIT
- **Location in repo:** `src/LinguaCoach.Web/src/app/templates/tailadmin/free-angular-tailwind-dashboard/`
- **Gitignored:** yes — `src/app/templates/` is excluded from main repo via `.gitignore`

---

## Folder Structure (final)

```
src/LinguaCoach.Web/src/app/
  templates/                             <- gitignored vendor reference
    README.md
    tailadmin/
      README.md
      free-angular-tailwind-dashboard/   <- TailAdmin free Angular source (MIT)
  admin/                                 <- SpeakPath wrapper layer
    tokens/admin-tokens.css
    components/sp-admin-*/
    services/
    index.ts
```

---

## Architecture Decisions Made

1. **Template location**: `src/app/templates/` inside the web project, not repo root. Keeps vendor source adjacent to the app that uses it as reference.
2. **Gitignored**: Template clone is excluded from main repo via `.gitignore`. Clone separately when onboarding. This avoids committing ~400 third-party files to the main repo history.
3. **No submodule**: Nested `.git` removed. Not using git submodule — too complex for a static vendor reference. Update process documented in `templates/tailadmin/README.md`.
4. **`admin-template/` removed**: The old empty placeholder folder was misleading. Deleted entirely.
5. **Dependency direction unchanged**: `templates/` → wrappers (`sp-admin-*`) → feature pages. Feature pages never import from `templates/`.
6. **Angular compiler isolation**: `tsconfig.app.json` compiles from `src/main.ts` only; `templates/` has no `.d.ts` files and no imports — it is invisible to the Angular build.

---

## Adapter Inventory Summary

See `docs/architecture/admin-tailadmin-adapter-inventory.md` for the full table.

| Status | Count | Examples |
|---|---|---|
| ✅ Partial (foundation exists) | 12 | layout, sidebar, header, button, badge, card, table, modal, input, pagination, filter-bar, drawer |
| ⬜ Future | 5 | dropdown, notification dropdown, theme toggle, breadcrumb, charts |
| 🚫 Do not copy | all demo pages | `pages/dashboard/`, `pages/tables/`, etc. |

---

## CI Gate Results

| Gate | Result |
|---|---|
| git diff --check | PASSED |
| dotnet restore | PASSED |
| dotnet build --configuration Release | PASSED (7 warnings, 0 errors) |
| dotnet test --configuration Release | PASSED — 3 arch + 1233 unit + 649 integration = 1885 total |
| npm ci | PASSED |
| npm run build --configuration production | pending (run after npm ci) |
| npm test --watch=false --browsers=ChromeHeadless | pending |
| npx playwright test --workers=1 | pending |

---

## What Was Not Implemented

- Full wrapper replacement using TailAdmin source (10X-E)
- Full admin page refactor (10X-F)
- Usage governance UX (TODO-10R-F)
- AI Usage redesign (TODO-10U)
- Prompt playground (TODO-10V)
- Notification platform
- Enterprise auth/security
- Observability stack
- Billing
- StudentProfile.CefrLevel migration
- Full placement engine
- Full mastery engine

---

## Risks and Known Limitations

1. `templates/` is gitignored — new contributors must clone TailAdmin manually. Document in onboarding/CONTRIBUTING.md in a future pass.
2. TailAdmin free template uses Tailwind CSS. SpeakPath admin currently uses CSS custom properties approximating TailAdmin tokens. Full Tailwind alignment is a 10X-E/10X-F concern.
3. Commit `da992cf` is a point-in-time snapshot. If TailAdmin updates its free template, re-clone and re-audit the adapter inventory.

---

## Next Recommended Action

Phase 10X-E: wrapper alignment — adapt real TailAdmin layout/sidebar/header/button/badge/card/input/modal source into `sp-admin-*` wrappers. Remove approximation CSS replaced by real patterns.
