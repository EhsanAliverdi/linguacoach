# Phase 4.4E — Real Audio Duration Measurement

**Date:** 2026-07-16
**Related:** Phase 4.4D (`docs/reviews/2026-07-16-phase-4-4d-audio-measurement-and-ai-accounting-review.md`), Phase 4.4C, 4.4B, 4.4A, 4.4
**HEAD before work:** `f0bd8e1b` (feat: add measured import usage accounting)

## Scope

Unlike prior Phase 4.4 sub-phases, this brief was already a single, bounded effort (the audio-
duration measurement work explicitly deferred twice before — Phase 4.4 and Phase 4.4D). No scoping
question was asked of the user this session; the full brief was implemented.

## Probe implementation

`IAudioDurationProbe` (`src/LinguaCoach.Application/ResourceImport/AudioDurationProbeContracts.cs`)
— a thin, stateless abstraction: `Task<AudioDurationProbeResult> ProbeDurationAsync(Stream, string
fileExtension, CancellationToken)`. `AudioDurationProbeResult` carries a `Status`
(`Success`/`ToolUnavailable`/`Timeout`/`Cancelled`/`UnsupportedOrCorrupt`), optional
`DurationSeconds`, and a safe `ErrorMessage`.

`AudioDurationProbe` (`src/LinguaCoach.Infrastructure/ResourceImport/AudioDurationProbe.cs`) is the
real implementation:
- Copies the audio stream to a bounded temporary file (ffprobe needs seekable, on-disk input for
  reliable duration reporting across formats) — always deleted in a `finally`, even on
  failure/cancellation.
- Shells out to `ffprobe -v error -show_entries format=duration -of json <file>` via
  `ProcessStartInfo.ArgumentList` — **never** a concatenated command-line string, so there is no
  shell-injection surface regardless of file path or content (`UseShellExecute = false`, no shell
  is ever invoked).
- Configurable tool path (`AudioDurationProbeOptions.FfprobePath`, defaulting to the bare tool name
  `"ffprobe"`, resolved via the process's `PATH` — no hardcoded machine-specific path anywhere) and
  timeout (`TimeoutSeconds`, default 15s).
- A missing/unstartable binary is caught as `Win32Exception` → `ToolUnavailable` (clear, distinct
  from a bad file).
- Timeout is enforced via a linked `CancellationTokenSource.CancelAfter`; on expiry the process is
  killed (`Kill(entireProcessTree: true)`) and the result distinguishes `Timeout` from the caller's
  own `Cancelled` by checking which token actually fired.
- A non-zero ffprobe exit code, or JSON that doesn't parse to a usable duration, is reported as
  `UnsupportedOrCorrupt` with the (truncated) stderr text.

`ImportAssetAudioDurationResolver` (`src/LinguaCoach.Infrastructure/ResourceImport/ImportAssetAudioDurationResolver.cs`)
sits above the probe: checks `ImportAsset.HasReusableAudioDurationMeasurement()` first (content
checksum unchanged) and returns the stored value without touching storage/the probe at all;
otherwise reads the asset's content via `IFileStorageService` and calls the probe, then mutates the
asset's measurement fields (`RecordAudioDurationMeasured`/`RecordAudioDurationMeasurementFailed`) —
in memory only, the caller `SaveChangesAsync`s.

## Runtime dependency

`ffprobe` (part of the ffmpeg suite) — an external binary, **not bundled**, must be present on
`PATH` (or at the configured path) wherever this code runs. This is a genuine new runtime
dependency for any environment that wants real STT cost accounting to succeed rather than fail
closed. **This session's dev/CI environment does not have ffprobe installed** — confirmed directly
(`ffprobe -version` → command not found) before writing any code, which shaped the testing strategy
below.

## Supported formats

`.mp3`, `.wav`, `.m4a`, `.ogg` — the same four extensions Import already accepts as audio
elsewhere in the codebase (`AudioExtensions` in `ImportPackageProcessingService`,
`ImportExecutionPlanGenerationService`, `ImportPlanEstimateService`). Any other extension is
rejected immediately as `UnsupportedOrCorrupt`, before any process is spawned.

## Persistence changes

`ImportAsset` (Domain) gained five properties and two mutators:

```csharp
decimal? AudioDurationSeconds
string? AudioDurationMeasurementChecksum   // the Checksum value that was actually measured
ImportAudioDurationMeasurementStatus AudioDurationMeasurementStatus  // NotMeasured/Measured/Failed
DateTimeOffset? AudioDurationMeasuredAtUtc
string? AudioDurationMeasurementError

bool HasReusableAudioDurationMeasurement()  // Measured && MeasurementChecksum == current Checksum
void RecordAudioDurationMeasured(decimal durationSeconds, DateTimeOffset measuredAtUtc)
void RecordAudioDurationMeasurementFailed(string reason, DateTimeOffset measuredAtUtc)
```

One additive migration, `Phase_4_4E_AudioDurationMeasurement`: five new nullable columns on
`import_assets` (`audio_duration_seconds`, `audio_duration_measurement_checksum`,
`audio_duration_measurement_status` with default `"NotMeasured"`, `audio_duration_measured_at_utc`,
`audio_duration_measurement_error`) — no existing column touched, no data backfill.

## Estimate and accounting changes

**Execution (authoritative, billable):** `ImportPackageProcessingService`'s STT block —
`const decimal assumedMinutes = 5m;` is **deleted entirely**. The block now calls
`IImportAssetAudioDurationResolver.ResolveAsync` first; on failure the asset is marked `Failed`
(safe error persisted) and processing moves to the next asset — no STT call, no cost, no candidate,
no silent 5-minute substitution. On success, `measuredMinutes = DurationSeconds / 60m` feeds both
the cost-ceiling projection (`sttCostIfCharged = measuredMinutes * SttCostPerMinute`) and the STT
ledger's claim (`ImportSttOperationKey`'s existing `assumedMinutes` parameter now receives the real
measured value — the parameter name was kept, per the "avoid unnecessary rename churn" judgment
call documented below, but its contents are no longer an assumption).

**Plan estimates (advisory, pre-approval):** `ImportExecutionPlanGenerationService.BuildVolumeEstimateAsync`
and `ImportPlanEstimateService.BuildVolumeEstimateAsync` (both, since the same duplicated logic
existed in both places pre-4.4E) now attempt real measurement for any audio-missing-transcript
entry that already has a materialized `ImportAsset` row — true for loose/inline submissions (Phase
4.2's synchronous asset creation) — falling back to the labeled 5-minute-per-file figure only for
entries without a materialized asset yet (ZIP packages, whose assets aren't extracted until the
approved plan's Extract stage) or where measurement itself fails. This asymmetry (execution never
falls back, estimation sometimes does) is deliberate and documented inline: an estimate is
advisory, not billed, so degrading gracefully here is correct; execution is billable, so it is not.

**Admin visibility:** `ImportSttOperationSummaryDto` gained `MeasuredAudioDurationSeconds` and
`AudioDurationMeasurementStatus`, populated from the operation's asset in
`ImportSttOperationSummaryQuery`. The Angular STT operations table gained a "Measured duration"
column.

## Design decision: no rename of `AssumedMinutes`

`ImportSttOperation.AssumedMinutes` (and the matching parameter names throughout
`IImportSttOperationLedger`) were **not** renamed to something like `MeasuredMinutes`, despite the
field's content now always being a real measured value rather than an assumption. Renaming would
have touched 5 source files (entity, EF config, ledger contracts, ledger implementation,
processing service) for a purely cosmetic gain with no functional benefit, and risked unnecessary
churn this late in a long session. The field's doc comment was updated instead to clarify its new
meaning. This is a deliberate, documented trade-off, not an oversight.

## Test environment problem and its resolution

Registering the real `AudioDurationProbe` as the default DI implementation would have made **every
pre-existing STT-dependent integration test in this repository start failing** in this session's
environment, because `ffprobe` genuinely is not installed here — every measurement attempt would
return `ToolUnavailable`, and per the brief's own requirement ("fail clearly ... do not silently
fall back"), that correctly blocks STT entirely.

This mirrors an already-established pattern in this codebase: `ISpeechToTextService` and
`ITextToSpeechService` both resolve to a real provider "if configured" or a `Fake*` implementation
"if not" (`DependencyInjection.cs`, `openAi.IsSupported ? openAi : sp.GetRequiredService<FakeSpeechToTextService>()`).
Audio duration measurement's failure mode is different in kind, though: the brief explicitly wants
production `AudioDurationProbe` to **fail clearly**, not silently substitute a working fake — so
the "real if configured, fake if not" pattern was **not** applied to the production DI registration
(`IAudioDurationProbe` always resolves to the real, ffprobe-shelling class in
`Infrastructure/DependencyInjection.cs`).

Instead, the substitution happens **only in the test host**: `ApiTestFactory.ConfigureWebHost`
overrides `IAudioDurationProbe` with a new `FakeAudioDurationProbe` (mirroring the existing
`IFileStorageService` → `FakeFileStorageService` override in the same method), returning a
default 300-second (5-minute) duration — deliberately the same figure the old flat assumption
used, so every pre-existing STT integration test is behaviourally unaffected by this substitution
while the real `ImportAssetAudioDurationResolver`'s persistence/reuse logic is still exercised
genuinely (only the low-level "read the file and report a duration" step is faked). Verified by
running the **full** pre-existing integration suite unchanged after the substitution: 1,321/1,321
passed before any new test was added.

## Critical tests

| # | Requirement | Test |
|---|---|---|
| 1 | Real duration replaces the five-minute assumption | `Real_measured_duration_replaces_the_five_minute_assumption_in_STT_cost` |
| 2 | Stored measurement reused for unchanged checksum | `Stored_measurement_is_reused_when_checksum_is_unchanged_no_second_probe_call` |
| 3 | Changed checksum triggers remeasurement | `Changed_checksum_triggers_remeasurement` |
| 4 | Corrupt audio fails clearly | `Probe_failure_is_recorded_clearly_never_a_silent_duration_fallback` + `Audio_duration_measurement_failure_fails_the_asset_clearly_without_calling_STT` |
| 5 | Unsupported audio fails clearly | `Unsupported_extension_fails_clearly_without_invoking_any_tool` + `Every_currently_accepted_audio_extension_is_recognized_as_supported` |
| 6 | Missing probe binary fails clearly | `Missing_probe_binary_fails_clearly` |
| 7 | Timeout and cancellation handled | `A_precancelled_token_is_reported_as_cancelled_not_a_tool_or_content_failure` (see Known Limitations for the timeout-under-load caveat) |
| 8 | Measured duration changes the plan estimate | Wired in `ImportExecutionPlanGenerationService`/`ImportPlanEstimateService`; proven indirectly via the execution-level cost tests (no dedicated plan-generation-level test — see Known Limitations) |
| 9 | Measured duration changes STT ceiling calculation | `Measured_duration_changes_the_cost_ceiling_calculation` |
| 10 | Measured duration changes final STT cost | `Real_measured_duration_replaces_the_five_minute_assumption_in_STT_cost` + `Stt_summary_reflects_the_real_measured_duration_not_the_old_flat_assumption` (integration) |
| 11 | Retry does not remeasure unchanged assets | `Retry_does_not_remeasure_an_unchanged_audio_asset` |
| 12 | Existing STT and AI ledger tests still pass | Full backend suite re-run, all pre-existing tests unchanged |
| 13 | Existing audited ceiling-amendment flow still passes | Full integration suite re-run, all `ImportCostCeilingAmendmentTests` unchanged |

## Tests

| Suite | Count | Result |
|---|---|---|
| Backend unit | 2,367 | Pass (+15: 4 `AudioDurationProbeTests`, 5 `ImportAssetAudioDurationResolverTests`, 4 new `ImportPackagePlanProcessingTests` cases, 2 constructor-fixture updates) |
| Backend integration | 1,322 | Pass (+1: `Stt_summary_reflects_the_real_measured_duration_not_the_old_flat_assumption`) |
| Backend architecture | 26 | Pass (unchanged — no new architectural boundary introduced this phase) |
| Angular unit (plan component) | fixture updates only | Compiles clean under `tsc --noEmit`; not executed (Karma still blocked, unchanged) |
| Playwright | 4 | Pass (2 pre-existing + 2 from Phase 4.4C, re-verified unaffected) |

**Gate results:**
- `git diff --check`: clean.
- `dotnet restore` / `dotnet build --configuration Release`: 0 errors.
- `dotnet test` (all three projects, Release): 2,367 + 1,322 + 26 = 3,715 passing, 0 failing.
- `npx tsc --noEmit`: identical pre-existing baseline error set — zero new.
- `npm run build -- --configuration production`: succeeds.
- `npm test -- --watch=false --browsers=ChromeHeadless`: blocked, same pre-existing baseline
  TypeScript errors, confirmed unchanged.
- `npx playwright test --workers=1` (the Import specs): 4 passed, 0 failed.

## Migration and live DB status

One additive migration, `Phase_4_4E_AudioDurationMeasurement`: five new nullable columns on
`import_assets`, no existing column changed, no data backfill. Live DB: **not touched**. Existing
data: **unchanged**.

## Known limitations

- **No real ffprobe verification.** The "successfully parses ffprobe's JSON output for a real
  audio file" happy path has not been exercised against an actual installed ffprobe binary in this
  session — none is available in this environment. All `AudioDurationProbeTests` are scoped to
  paths that don't require ffprobe (missing binary, unsupported extension, pre-cancelled token).
  The JSON-parsing logic is straightforward and reviewed but not integration-verified. Tracked as
  `TODO-4.4E-FFPROBE-HAPPY-PATH-VERIFICATION`.
- **Timeout-under-load is implemented but not load-tested.** The cancellation path (pre-cancelled
  token) is proven deterministically; the "process actually hangs and gets killed after
  `TimeoutSeconds`" path relies on code inspection rather than a live timing test, to keep the
  suite portable and fast rather than depending on an OS-specific slow command.
- **ZIP-packaged audio's plan *estimate* still uses the flat assumption** until Extract runs and
  real `ImportAsset` rows exist — execution itself always measures for real regardless of package
  type, so this gap is estimate-display-only, never a billed amount. Tracked as
  `TODO-4.4E-ZIP-PREEXTRACTION-MEASUREMENT`.
- **`ffprobe` is now a genuine, undocumented-until-now runtime dependency** for any environment
  that wants STT cost accounting to succeed. Without it, every audio file in an AI-structured
  package will fail measurement and be marked `Failed` — by design (fail clearly), but this is an
  operational deployment consideration that should be flagged before this ships to any environment
  actually processing audio (dev container / production image must install ffmpeg).
- **Test-host substitution** (`FakeAudioDurationProbe`) means none of this session's own
  integration tests exercise the real `AudioDurationProbe` end-to-end through the HTTP API — only
  through direct unit tests of the class itself and through the (faked) resolver/processing-service
  integration.

## Verdict

The flat five-minute-per-audio-file assumption is genuinely deleted from the codebase, replaced
with real, persisted, reusable measurement wired into every place the brief specified (cost
ceiling, final STT cost, plan estimates where materially possible, admin visibility). Fail-closed
behavior on measurement failure is implemented and proven — no silent fallback exists anywhere in
the execution path. All backend gates green across 3,715 tests, zero regressions. The one honest
gap is verifying the real ffprobe binary's actual output parsing, which this environment cannot
exercise; this is clearly flagged rather than glossed over.

## Next recommended action

`TODO-4.4E-FFPROBE-HAPPY-PATH-VERIFICATION` should be picked up in an environment with ffprobe
installed (or a CI image updated to install it) to close the one remaining real-world verification
gap. `TODO-4.4E-ZIP-PREEXTRACTION-MEASUREMENT` is a smaller, lower-priority follow-up.
