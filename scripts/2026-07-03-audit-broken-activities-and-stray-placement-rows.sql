-- Manual audit/cleanup script for the 2026-07-03 pilot-student live audit.
-- See docs/reviews/2026-07-03-pilot-student-onboarding-placement-practice-live-audit.md
-- and docs/reviews/2026-07-03-workplace-default-content-and-placement-gating-review.md
--
-- Run the SELECTs first and review the output before running any UPDATE.
-- This script does not delete any rows — it only deactivates/marks-abandoned,
-- which is reversible.

-- ─────────────────────────────────────────────────────────────────────────────
-- 1. Find LearningActivity rows with an empty required array in a module-stage-v1
--    payload (the class of bug behind the broken "phrase_match" activity found
--    for pilot.student.20e@speakpath.app, activity c10722ac-a4a9-468b-aefd-27b82213d806).
--    Generalized beyond phrase_match: checks every pattern with an array-shaped
--    required field per ModuleStageContentValidator.RequiredPracticeKeysByPatternKey.
-- ─────────────────────────────────────────────────────────────────────────────

SELECT
    la."Id",
    la."Title",
    la."ExercisePatternKey",
    la."ActivityType",
    la."IsActive",
    la."CreatedAt"
FROM "LearningActivities" la
WHERE la."IsActive" = true
  AND la."ExercisePatternKey" IN (
        'phrase_match', 'gap_fill_workplace_phrase', 'write_from_dictation',
        'answer_short_question', 'read_aloud', 'repeat_sentence',
        'respond_to_situation', 'describe_image', 'reorder_paragraphs'
      )
  AND (
        -- "pairs" (phrase_match)
        (la."AiGeneratedContentJson"::jsonb #> '{practiceContent,exerciseData,pairs}') = '[]'::jsonb
        OR
        -- "items" (several patterns)
        (la."AiGeneratedContentJson"::jsonb #> '{practiceContent,exerciseData,items}') = '[]'::jsonb
      );

-- Review the SELECT output above. For each confirmed-broken row, deactivate it so
-- it stops being suggested/materialized-linked. Uncomment and fill in the ID list:
--
-- UPDATE "LearningActivities"
-- SET "IsActive" = false
-- WHERE "Id" IN ( /* paste confirmed IDs here */ );


-- ─────────────────────────────────────────────────────────────────────────────
-- 2. Find stray InProgress PlacementAssessments rows created after the student's
--    profile had already progressed past PlacementCompleted via a different flow
--    (the root cause of the /profile vs /dashboard placement-status disagreement).
--    StudentLifecycleStage enum: PlacementCompleted=6, CourseReady=7, InLesson=8,
--    ActiveLearning=9, Paused=10, Archived=11.
-- ─────────────────────────────────────────────────────────────────────────────

SELECT
    pa."Id",
    pa."StudentProfileId",
    pa."Status",
    pa."CreatedAt",
    sp."LifecycleStage",
    sp."CefrLevel"
FROM "PlacementAssessments" pa
JOIN "StudentProfiles" sp ON sp."Id" = pa."StudentProfileId"
WHERE pa."Status" = 1 -- InProgress
  AND sp."LifecycleStage" >= 6; -- PlacementCompleted or later

-- Review the SELECT output above. For each confirmed-stray row, mark it Abandoned
-- (PlacementStatus.Abandoned = 3) so it stops appearing in GetHistoryAsync results
-- and can never again be picked up by GetLatestAssessmentAsync's Completed-first
-- ordering as anything but what it is:
--
-- UPDATE "PlacementAssessments"
-- SET "Status" = 3 -- Abandoned
-- WHERE "Id" IN ( /* paste confirmed IDs here */ );
