# Phase 10G — Student Profile & Learning Preferences v2

**Date:** 2026-06-17
**Sprint:** Phase 10G
**Related to:** Sprint 10 series — student engagement / learning preferences

---

## Files reviewed

### Backend
- `src/LinguaCoach.Domain/Entities/StudentProfile.cs`
- `src/LinguaCoach.Domain/Enums/TranslationHelpPreference.cs` (new)
- `src/LinguaCoach.Domain/Enums/DifficultyPreference.cs` (new)
- `src/LinguaCoach.Persistence/Configurations/StudentProfileConfiguration.cs`
- `src/LinguaCoach.Persistence/Migrations/20260616230228_T46_StudentLearningPreferences.cs` (new)
- `src/LinguaCoach.Application/Profile/ProfileQueries.cs` (new)
- `src/LinguaCoach.Application/Profile/ProfileCommands.cs` (new)
- `src/LinguaCoach.Infrastructure/Profile/ProfileQueryHandler.cs` (new)
- `src/LinguaCoach.Infrastructure/Profile/ProfileCommandHandler.cs` (new)
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs`
- `src/LinguaCoach.Api/Controllers/ProfileController.cs` (new)

### Angular
- `src/LinguaCoach.Web/src/app/core/services/profile.service.ts` (new)
- `src/LinguaCoach.Web/src/app/features/profile/profile.component.ts` (rewritten)
- `src/LinguaCoach.Web/src/app/features/profile/profile.component.spec.ts` (new)

### Tests
- `tests/LinguaCoach.UnitTests/Domain/StudentProfileLearningPreferencesTests.cs` (new)
- `tests/LinguaCoach.IntegrationTests/Api/ProfileEndpointTests.cs` (new)

---

## Fields added

### Student-editable preference fields (T46)

| Field | Type | Max | Notes |
|---|---|---|---|
| `PreferredName` | `string?` | 100 | How the student likes to be called |
| `SupportLanguageCode` | `string?` | 10 | BCP-47 language code, e.g. "fa" |
| `SupportLanguageName` | `string?` | 100 | Human name, e.g. "Persian" |
| `TranslationHelpPreference` | `enum?` | — | Never / WhenDifficult / AlwaysAvailable |
| `LearningGoals` | `List<string>` | 10 items × 100 chars | JSON (jsonb in PostgreSQL) |
| `CustomLearningGoal` | `string?` | 200 | Free-text beyond predefined list |
| `FocusAreas` | `List<string>` | 10 items × 100 chars | JSON (jsonb in PostgreSQL) |
| `CustomFocusArea` | `string?` | 200 | Free-text beyond predefined list |
| `DifficultyPreference` | `enum?` | — | Gentle / Balanced / Challenging |
| `LearningPreferencesUpdatedAt` | `DateTimeOffset?` | — | Set by UpdateLearningPreferences |

`PreferredSessionDurationMinutes` was already present; `UpdateLearningPreferences` can also update it.

---

## Student-editable fields

All fields updated via `UpdateLearningPreferences` and `PUT /api/profile/preferences`:
- PreferredName
- SupportLanguageCode + SupportLanguageName
- TranslationHelpPreference
- LearningGoals, CustomLearningGoal
- FocusAreas, CustomFocusArea
- DifficultyPreference
- PreferredSessionDurationMinutes
- LearningPreferencesUpdatedAt (auto-set)

---

## System/admin read-only fields

Never touched by `UpdateLearningPreferences`:
- `CefrLevel` — only `SetCefrLevel()` (internal, called by assessment flow)
- `FirstName`, `LastName`, `DisplayName`, `CareerContext`, `LearningGoal` — admin only
- `OnboardingStatus`, `LastCompletedStep` — onboarding state machine
- `LanguagePairId`, `CareerProfileId`, `LearningTrackId` — onboarding selections
- `SkillFocus` — set during onboarding
- `LifecycleStage` — admin/system
- `ProfessionalExperienceLevel`, `RoleFamiliarity`, `WorkplaceSeniority` — admin/system
- All prompt fields — admin-only, no student API surface

---

## CEFR level handling

- Read-only for students.
- Displayed in profile page Level section.
- Displayed with per-level explanation string (embedded in the component).
- No input or edit control shown.
- `UpdateLearningPreferences` does not accept or set CefrLevel.
- API contract (`PUT /api/profile/preferences` request model) has no CefrLevel field.

---

## Support language handling

- Named "Support language" in UI — never "Primary language".
- Stored as code + name pair (e.g. `fa` / `Persian`).
- 10-item predefined dropdown in the Angular component.
- Separate "Translation help" dropdown (Never / WhenDifficult / AlwaysAvailable).
- Both fields are student-editable.

---

## Learning goals / focus areas design

- Multi-select tag chips from predefined lists.
- Predefined goals: 11 options including Workplace English among others.
- Predefined focus areas: 12 options.
- Custom goal and custom focus area text inputs (max 200 chars each).
- Max 10 selections enforced at domain level and in UI toggle logic.
- Workplace English is one option among many — not hardcoded as a fixed default.
- "Workplace English" label is used in the UI as one chip option only.

---

## Downstream / future use

- `LearningGoals` and `FocusAreas` are available for AI context builders to personalise prompts.
- `SupportLanguageCode` / `TranslationHelpPreference` can be used to inject translation hints in AI activity generation.
- `DifficultyPreference` is available for curriculum/pattern selection weight adjustment.
- These fields are not yet wired into AI prompt generation or curriculum routing — that is future work.

---

## Known limitations

- Support language dropdown is a hardcoded list of 10 languages. Can be extended or made dynamic in a future phase.
- `LearningGoals` and `FocusAreas` JSON columns use `HasConversion` for SQLite compatibility in integration tests. PostgreSQL uses native `jsonb`.
- `GET /api/profile` returns email from ASP.NET Identity UserManager. If user not found, email is null.

---

## Implementation tasks produced

None beyond this phase. Preferences are captured; downstream wiring is deferred.

---

## Risks / unresolved questions

- `jsonb` columns with `HasConversion` may lose PostgreSQL-native JSON querying ability. Not needed yet.
- `SupportLanguageName` is stored redundantly with `SupportLanguageCode`. This avoids a language lookup table but means names can drift if the list changes. Acceptable for MVP.

---

## Final verdict

Phase 10G is complete. All CI/CD gates pass:
- Backend unit: 996 (was 985) — +11
- Backend integration: 539 (was 534) — +5
- Architecture: 3
- Angular unit: 243 (was 229) — +14

---

## Next phase recommendation

**Phase 10H — AI context personalisation from preferences.**
Wire `LearningGoals`, `FocusAreas`, `DifficultyPreference`, and `SupportLanguageCode` into `IAiContextBuilder` so generated activity prompts reflect the student's stated goals and difficulty level. This will make the 10G preference data visible to students through more relevant practice content.
