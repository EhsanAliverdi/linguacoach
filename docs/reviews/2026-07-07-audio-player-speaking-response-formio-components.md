# Audio Player + Speaking Response custom Form.io components

**Date**: 2026-07-07
**Related sprint/feature**: Placement listening/speaking items (SpeakPath placement engine)
**Type**: implementation plan + engineering review

## Context

The admin Form.io builder (shared by onboarding and placement) only supported Form.io's stock
component types. There was no way to embed audio playback or a speech-recording control *inside* a
schema. Concretely:

- **Listening** placement items faked it: audio was rendered as page-level chrome (a bare
  `<audio>` element sibling above the Form.io form in `placement.component.html`), driven by an
  `item.hasAudio` boolean and a separate `GET /student/placement/audio/.../listening` call. An
  admin authoring a question in the builder had no way to place the audio player inline with the
  question itself.
- **Speaking** placement items didn't exist at all — the 12 seeded "speaking" skill items were
  actually self-assessment multiple-choice questions about spoken-language etiquette (e.g. "How
  would you greet someone in the morning?"), scored identically to grammar/vocabulary. No audio was
  ever recorded. A real, working recording pipeline (`VoiceRecorderComponent`,
  `ISpeechToTextService`, `ISpeakingEvaluationProvider`/`OpenAiSpeakingEvaluationProvider`) already
  existed, but only wired into the unrelated Activity/Practice feature's `SpeakingRolePlay` flow.

The user asked for two first-class Form.io components — audio playback and speech recording —
available in the shared admin builder, planned before implementation per the standing project rule
requiring a plan for non-trivial work.

## Files reviewed

- `src/LinguaCoach.Web/src/app/shared/formio/formio-builder.component.ts` /
  `formio-renderer.component.ts` — confirmed both are thin wrappers around raw `@formio/js`
  (`Formio.builder`/`Formio.createForm`), not `@formio/angular`, with no existing custom-component
  registration anywhere in the repo.
- `src/LinguaCoach.Infrastructure/Onboarding/FormIoSchemaValidationService.cs` — the backend
  component-type allow-list (the actual security boundary).
- `src/LinguaCoach.Web/src/app/features/student/placement/placement.component.ts/.html` — the
  existing page-chrome audio flow.
- `src/LinguaCoach.Infrastructure/Placement/AdaptivePlacementAudioService.cs` — confirmed the TTS
  audio pipeline was already fully functional; the gap was purely "no schema component," not a
  broken audio pipeline.
- `src/LinguaCoach.Web/src/app/features/student/activity/renderers/voice-recorder/voice-recorder.component.ts`,
  `src/LinguaCoach.Api/Controllers/ActivityController.cs`,
  `src/LinguaCoach.Application/Speaking/ISpeakingEvaluationProvider.cs`,
  `src/LinguaCoach.Infrastructure/Activity/SpeakingAudioService.cs` — the existing, working
  recording/evaluation pipeline reused (not duplicated) by this feature.
- `src/LinguaCoach.Persistence/Seed/PlacementItemBankSeeder.cs` — how listening/speaking items were
  authored today (workaround-level, no first-class components).
- `node_modules/@formio/js/lib/cjs/components/{content,textfield,signature}/*.js` and
  `components/Components.js` — verified the real `@formio/js` v5 component-authoring contract
  (`Formio.Components.addComponent`, `static schema()`/`builderInfo`, `render()`/`attach()`,
  `loadRefs`, `ref="x"` DOM convention, `dataValue`/`setValue`/`isEmpty`) before writing any
  component code, since no prior custom-component work existed in this repo to copy from.

## Decisions made (AskUserQuestion)

1. **Speaking scoring approach**: reuse the existing `ISpeakingEvaluationProvider`/
   `OpenAiSpeakingEvaluationProvider` for real AI-backed scoring in v1, rather than a
   manual-review-only placeholder. *(Recommended option chosen.)*
2. **Seed content**: rewrite the 12 seeded "speaking" items to use the new `speakingResponse`
   component with real prompts, rather than leaving the old fake multiple-choice content in place.
   *(Recommended option chosen.)*

## Architecture

- **`audioPlayer`** — presentational-only Form.io component. Carries no audio source in its
  authored schema (the real audio is generated per-assessment server-side from a backend-only
  script — there's nothing to author). The host page pushes the resolved URL in via a public
  `setAudioSrc(url)` method, called by `FormioRendererComponent`'s new `audioSrc` input. This keeps
  the HTTP/blob-URL fetch logic in Angular (where it already worked, unchanged) and the Form.io
  component purely presentational.
- **`speakingResponse`** — a real input component (`key: "answer"`). Wraps a new
  framework-agnostic `MicRecorder` helper (getUserMedia/MediaRecorder state machine, mirrors but
  does not share code with `VoiceRecorderComponent`, which remains untouched). Uploads immediately
  on stop through a host-supplied `placementContext.uploadSpeakingAudio(...)` function (passed via
  `Formio.createForm(..., { placementContext })`), since a vanilla Form.io component has no
  Angular `HttpClient`/auth interceptor of its own. Stores `{ storageKey, mimeType,
  durationSeconds }` as its Form.io value, flowing through the existing, unmodified
  `submitForm()` → `/student/placement/respond` path.
- **Scoring**: new `ScoringRuleKinds.Speaking` kind + `IPlacementSpeakingScorer`
  (`PlacementSpeakingScorer`), an additive branch in `PlacementAssessmentService.SubmitResponseAsync`
  that routes speaking-kind items to the AI evaluator instead of the deterministic scorer. The
  deterministic `PlacementScoringService` is completely untouched.
- **Upload endpoint**: `POST /student/placement/audio/{assessmentId}/items/{itemId}/speaking`
  (multipart) on `StudentPlacementController`, reusing `SpeakingAudioService`'s mime/size checks
  via a new `category`/`StoreDirectAsync` parameter (no duplicated validation logic).
- **Registration**: both components registered once via `Formio.Components.addComponent` at app
  bootstrap (`main.ts`), so both `FormioBuilderComponent` (builder palette) and
  `FormioRendererComponent` (student rendering) pick them up automatically for both onboarding and
  placement — no per-consumer wiring needed, matching the existing "one shared builder/renderer"
  architecture.

## Testing

- Backend: `PlacementSpeakingScorerTests` (7 unit tests — score mapping/normalization, threshold,
  no-storage-key, provider-unsupported, provider-throws), 5 new `StudentPlacementControllerTests`
  (auth, ownership, mime validation, success), `PlacementItemBankSeederTests` updated (excluded
  speaking items from the deterministic-comparison test, added a dedicated
  `speakingResponse`/`speaking`-kind assertion). Full suite: 1796 unit + 1382 integration + 5
  architecture, all green.
- Frontend: `mic-recorder.spec.ts` (9 unit tests covering the full state machine), 2 new
  `PlacementComponent` specs for `placementContext`. Full Karma suite: 1588 passing, 123 failing —
  matches the pre-existing baseline (~120, confirmed unrelated to this work by name — all in
  `AdminStudentDetailComponent`); none of the new specs are among the failures.
- Manual verification: performed end to end in-browser (admin builder palette, then as a student
  taking placement). This surfaced two real bugs neither test suite caught, both fixed in this
  session:
  1. The seeded listening items' schemas never actually contained an `audioPlayer` component —
     `BuildFormIoAuthoring` needed an explicit branch adding one before the question for listening
     items (mirroring the existing `reading_passage` leading-content-component pattern). Without
     this, `FormioRendererComponent.applyAudioSrc()` had nothing to push the resolved URL into and
     the audio would have silently stopped rendering entirely (the old sibling `<audio>` markup
     was removed from `placement.component.html` in favor of the schema component).
  2. `SpeakingResponseComponent.render()` never rendered `this.component.label` (the actual
     prompt) — it only showed the recorder status text, leaving the student with no indication of
     what to talk about. Fixed by rendering an escaped label line above the recorder controls
     (new `escapeHtml` helper, `shared/formio/escape-html.ts`).
  Confirmed after both fixes (dev DB reseeded, API container rebuilt/restarted): a listening item
  shows the question, then a real generated `<audio>` element (0:03 duration, Gemini TTS) inline
  with the question; a speaking item shows its real prompt text, a working Record button, and a
  graceful "Microphone access was denied" message in the headless test browser (no mic available)
  rather than a crash. No console errors in either flow.

## Risks / unresolved questions

- Actual audio *recording* (not just the permission-denied fallback) was not verified against a
  real microphone in this session — the test environment (headless browser) has no audio input
  device. The MicRecorder unit tests cover the full state machine against a fake MediaRecorder; the
  upload → score path (`PlacementSpeakingScorer` → `OpenAiSpeakingEvaluationProvider`) is covered
  by backend integration/unit tests but not exercised with a real recorded clip end-to-end.
- `SpeakingEvaluationRequest.ActivityId`/`AttemptId` are reused for placement items as correlation
  metadata, not real `Activity`/`ActivityAttempt` foreign keys — acceptable since the interface
  only uses them for provider-side logging/tracking, not FK integrity, but worth flagging if a
  future refactor tightens that interface's contract.
- `PlacementAssessmentOptions.SpeakingPassThreshold` (default 0.6) is a first guess, not tuned
  against real recordings/evaluator output — may need adjustment once real speaking data exists.

## Final verdict

Both components implemented, wired end-to-end (builder → schema → student render → record →
upload → score), and covered by backend + frontend automated tests, all green. Documentation
(`docs/architecture/formio-onboarding-placement-model.md`) updated with a new "Audio & Speaking
components" section; the "Known limitations" bullet about unscored speaking items was resolved.

## Next recommended action

Verify actual audio recording/upload/AI scoring against a real microphone in a non-headless
browser (this session confirmed everything up to the recorder's permission-denied fallback, since
no mic was available in the test environment) — take a placement speaking item as a real student,
confirm the recording uploads, and that `PlacementSpeakingScorer` produces a real score/transcript
via `OpenAiSpeakingEvaluationProvider` (or the configured provider) end to end.
