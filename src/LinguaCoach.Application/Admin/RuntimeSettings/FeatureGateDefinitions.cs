namespace LinguaCoach.Application.Admin.RuntimeSettings;

/// <summary>
/// Source of truth for every feature-gate group in the Phase 20B registry. Every key here
/// corresponds to a real property on an existing options class or DB entity — nothing here
/// is invented behavior.
/// </summary>
public static class FeatureGateDefinitions
{
    // Evaluated lazily (not via a field initializer) so it runs after every group field
    // below has been initialized, regardless of textual declaration order.
    public static IReadOnlyList<FeatureGateGroupDefinition> All => _all ??= BuildAll();
    private static IReadOnlyList<FeatureGateGroupDefinition>? _all;

    private static IReadOnlyList<FeatureGateGroupDefinition> BuildAll() =>
    [
        ReviewScaffoldGeneration,
        PracticeGymReviewScaffoldPilot,
        PracticeGymFormIoTemplatePilot,
        LessonGenerationBuffer,
        TtsGeneration,
        PracticeGymGenerationPerType,
        AiSignalSafetySpeaking,
        AiSignalSafetyWriting,
        LearningPlanRegeneration,
        ActivityFeedbackPolicy,
    ];

    public static readonly FeatureGateGroupDefinition ReviewScaffoldGeneration = new()
    {
        GroupKey = "review-scaffold-generation",
        DisplayName = "Review scaffold generation",
        Description = "Controls whether the readiness pool replenishment engine may generate lower-level review/scaffold activities when weakness signals are detected.",
        Category = FeatureGateCategory.ReviewScaffoldPracticeGymPilot,
        BackingStore = FeatureGateBackingStore.ReadinessPoolOverride,
        WarningText = "Enabling generation changes what content is created for students. Keep DryRunOnly on until the effect has been reviewed.",
        Settings =
        [
            new FeatureGateSettingDefinition
            {
                Key = "ReadinessPool.EnableReviewScaffoldGeneration",
                DisplayName = "Enable review scaffold generation",
                Description = "When on, replenishment may generate review/scaffold items when weakness signals are detected.",
                DataType = FeatureGateDataType.Boolean,
                DefaultValueJson = "false",
                IsEditableAtRuntime = true,
                RiskLevel = FeatureGateRiskLevel.High,
                RequiresConfirmation = true,
            },
            new FeatureGateSettingDefinition
            {
                Key = "ReadinessPool.DryRunOnly",
                DisplayName = "Dry-run only",
                Description = "When on, generation logic runs but never writes to the database. Must be off for generation to take effect.",
                DataType = FeatureGateDataType.Boolean,
                DefaultValueJson = "true",
                IsEditableAtRuntime = true,
                RiskLevel = FeatureGateRiskLevel.Medium,
            },
            new FeatureGateSettingDefinition
            {
                Key = "ReadinessPool.RequireAdminReview",
                DisplayName = "Require admin review",
                Description = "When on, generated review/scaffold items stay hidden from students until an admin approves them.",
                DataType = FeatureGateDataType.Boolean,
                DefaultValueJson = "true",
                IsEditableAtRuntime = true,
                RiskLevel = FeatureGateRiskLevel.Medium,
            },
            new FeatureGateSettingDefinition
            {
                Key = "ReadinessPool.MaxScaffoldItemsPerStudentPerDay",
                DisplayName = "Max scaffold items per student per day",
                Description = "Maximum review/scaffold items created for a single student in a calendar day (UTC).",
                DataType = FeatureGateDataType.Integer,
                DefaultValueJson = "3",
                IsEditableAtRuntime = true,
                RiskLevel = FeatureGateRiskLevel.Low,
                MinValue = 0,
                MaxValue = 10,
            },
            new FeatureGateSettingDefinition
            {
                Key = "ReadinessPool.ScaffoldAllowedSources",
                DisplayName = "Allowed sources",
                Description = "Readiness pool sources allowed to receive review/scaffold generation.",
                DataType = FeatureGateDataType.StringArray,
                DefaultValueJson = "[\"PracticeGym\"]",
                IsEditableAtRuntime = true,
                RiskLevel = FeatureGateRiskLevel.Medium,
                AllowedValues = ["TodayLesson", "PracticeGym", "LessonBatch", "Review"],
            },
            new FeatureGateSettingDefinition
            {
                Key = "ReadinessPool.AllowTodayLessonInsertion",
                DisplayName = "Allow Today lesson insertion",
                Description = "Explicit override required (in addition to ScaffoldAllowedSources containing TodayLesson) before review/scaffold items may be generated for the Today lesson pool.",
                DataType = FeatureGateDataType.Boolean,
                DefaultValueJson = "false",
                IsEditableAtRuntime = true,
                RiskLevel = FeatureGateRiskLevel.Critical,
                RequiresConfirmation = true,
            },
            new FeatureGateSettingDefinition
            {
                Key = "ReadinessPool.MinimumConfidenceForReviewNeed",
                DisplayName = "Minimum confidence for review need",
                Description = "Minimum ReviewNeedConfidence band required before a weak-event signal is allowed to trigger review/scaffold generation.",
                DataType = FeatureGateDataType.String,
                DefaultValueJson = "\"Medium\"",
                IsEditableAtRuntime = true,
                RiskLevel = FeatureGateRiskLevel.Low,
                AllowedValues = ["Low", "Medium", "High"],
            },
        ],
    };

    public static readonly FeatureGateGroupDefinition PracticeGymReviewScaffoldPilot = new()
    {
        GroupKey = "practice-gym-review-scaffold-pilot",
        DisplayName = "Practice Gym review scaffold pilot",
        Description = "Phase 19C pilot gate: surfaces admin-approved review/scaffold items to students in Practice Gym.",
        Category = FeatureGateCategory.ReviewScaffoldPracticeGymPilot,
        BackingStore = FeatureGateBackingStore.ReadinessPoolOverride,
        Dependencies =
        [
            "RequireAdminReview should be true",
            "EnableReviewScaffoldGeneration should be true for new generation",
            "DryRunOnly must be false for real generation",
            "AllowTodayLessonInsertion remains false unless intentionally enabled",
        ],
        WarningText = "Turning this off is the fastest rollback: it hides all approved-but-unconsumed scaffold items from students without deleting any data.",
        Settings =
        [
            new FeatureGateSettingDefinition
            {
                Key = "ReadinessPool.PracticeGymPilotEnabled",
                DisplayName = "Pilot enabled",
                Description = "When on, admin-approved review/scaffold items are surfaced to students in Practice Gym.",
                DataType = FeatureGateDataType.Boolean,
                DefaultValueJson = "false",
                IsEditableAtRuntime = true,
                RiskLevel = FeatureGateRiskLevel.Medium,
            },
            new FeatureGateSettingDefinition
            {
                Key = "ReadinessPool.PracticeGymPilotLabel",
                DisplayName = "Student-facing label",
                Description = "Label shown on Practice Gym cards for approved review/scaffold items during the pilot.",
                DataType = FeatureGateDataType.String,
                DefaultValueJson = "\"Review\"",
                IsEditableAtRuntime = true,
                RiskLevel = FeatureGateRiskLevel.Low,
                MaxLength = 60,
            },
            new FeatureGateSettingDefinition
            {
                Key = "ReadinessPool.PracticeGymPilotReason",
                DisplayName = "Student-facing reason",
                Description = "Reason text shown on Practice Gym cards for approved review/scaffold items during the pilot. Must avoid negative wording.",
                DataType = FeatureGateDataType.String,
                DefaultValueJson = "\"This helps you practise a skill you are building.\"",
                IsEditableAtRuntime = true,
                RiskLevel = FeatureGateRiskLevel.Low,
                MaxLength = 200,
            },
            new FeatureGateSettingDefinition
            {
                Key = "ReadinessPool.MaxStudentVisibleScaffoldSuggestions",
                DisplayName = "Max visible suggestions per response",
                Description = "Maximum number of approved review/scaffold items shown to a single student in one Practice Gym response during the pilot.",
                DataType = FeatureGateDataType.Integer,
                DefaultValueJson = "2",
                IsEditableAtRuntime = true,
                RiskLevel = FeatureGateRiskLevel.Low,
                MinValue = 0,
                MaxValue = 4,
            },
        ],
    };

    public static readonly FeatureGateGroupDefinition PracticeGymFormIoTemplatePilot = new()
    {
        GroupKey = "practice-gym-formio-template-pilot",
        DisplayName = "Practice Gym Form.io template pilot",
        Description = "AI Bank-First Teaching Architecture pilot: when on, PracticeGymGenerationJob personalizes the dedicated 'formio_practice_gym_pilot' pattern from a published, approved ActivityTemplate instead of free-form AI generation, and renders it via Form.io. Inert unless the pattern's ExerciseTypeDefinition is also promoted from 'planned' to 'ready' by an admin — this flag alone does not make it live.",
        Category = FeatureGateCategory.PracticeGymFormIoTemplatePilot,
        BackingStore = FeatureGateBackingStore.ReadinessPoolOverride,
        Dependencies =
        [
            "ExerciseTypeDefinition for 'formio_practice_gym_pilot' must be promoted to ImplementationStatus=ready",
            "At least one published, Approved ActivityTemplate with PatternKey='formio_practice_gym_pilot' must exist",
        ],
        WarningText = "Turning this off is the fastest rollback — generation immediately falls back to the pattern being inert (planned exercise type), no student-facing content is affected.",
        Settings =
        [
            new FeatureGateSettingDefinition
            {
                Key = "PracticeGymFormIoPilot.Enabled",
                DisplayName = "Pilot enabled",
                Description = "When on, the Form.io template pilot pattern personalizes from ActivityTemplate instead of free-form AI generation.",
                DataType = FeatureGateDataType.Boolean,
                DefaultValueJson = "false",
                IsEditableAtRuntime = true,
                RiskLevel = FeatureGateRiskLevel.High,
                RequiresConfirmation = true,
            },
        ],
    };

    public static readonly FeatureGateGroupDefinition LessonGenerationBuffer = new()
    {
        GroupKey = "lesson-generation-buffer",
        DisplayName = "Lesson generation buffer",
        Description = "Controls the ready-lesson buffer size and background generation throttles.",
        Category = FeatureGateCategory.ReadinessPoolLessonGeneration,
        BackingStore = FeatureGateBackingStore.LessonGenerationSettingsTable,
        Settings =
        [
            Int("LessonGeneration.ReadyLessonBufferSize", "Ready lesson buffer size", "Target number of ready lessons maintained per student.", 5, min: 1),
            Int("LessonGeneration.RefillThreshold", "Refill threshold", "Ready-lesson count at or below which a refill is triggered. Must stay below the buffer size.", 1, min: 0),
            Int("LessonGeneration.RefillBatchSize", "Refill batch size", "Number of lessons generated per refill batch.", 4, min: 1),
            Int("LessonGeneration.MaxGenerationAttempts", "Max generation attempts", "Maximum generation attempts per lesson before it is abandoned. No job currently reads this field — display only.", 2, min: 1, isRuntimeEffective: false),
            Int("LessonGeneration.GenerationTimeoutSeconds", "Generation timeout (seconds)", "Per-lesson generation timeout. No job currently reads this field — display only.", 120, min: 1, isRuntimeEffective: false),
            new FeatureGateSettingDefinition
            {
                Key = "LessonGeneration.MaxConcurrentGenerationJobs",
                DisplayName = "Max concurrent generation jobs",
                Description = "Maximum number of lesson-generation jobs allowed to run concurrently. No job currently reads this field — display only.",
                DataType = FeatureGateDataType.Integer,
                DefaultValueJson = "2",
                IsEditableAtRuntime = true,
                IsRuntimeEffective = false,
                RiskLevel = FeatureGateRiskLevel.Medium,
                MinValue = 1,
            },
            new FeatureGateSettingDefinition
            {
                Key = "LessonGeneration.EnableBackgroundGeneration",
                DisplayName = "Enable background generation",
                Description = "When off, the background lesson-generation job does not run.",
                DataType = FeatureGateDataType.Boolean,
                DefaultValueJson = "true",
                IsEditableAtRuntime = true,
                RiskLevel = FeatureGateRiskLevel.Medium,
            },
        ],
    };

    public static readonly FeatureGateGroupDefinition TtsGeneration = new()
    {
        GroupKey = "tts-generation",
        DisplayName = "TTS generation",
        Description = "Controls text-to-speech audio generation for lessons.",
        Category = FeatureGateCategory.ReadinessPoolLessonGeneration,
        BackingStore = FeatureGateBackingStore.LessonGenerationSettingsTable,
        Settings =
        [
            new FeatureGateSettingDefinition
            {
                Key = "LessonGeneration.EnableTtsGeneration",
                DisplayName = "Enable TTS generation",
                Description = "When off, audio assets are not generated for new lessons. No job currently reads this field — display only.",
                DataType = FeatureGateDataType.Boolean,
                DefaultValueJson = "true",
                IsEditableAtRuntime = true,
                IsRuntimeEffective = false,
                RiskLevel = FeatureGateRiskLevel.Low,
            },
            Int("LessonGeneration.TtsTimeoutSeconds", "TTS timeout (seconds)", "Per-request timeout for TTS generation. No job currently reads this field — display only.", 60, min: 1, isRuntimeEffective: false),
            Int("LessonGeneration.MaxConcurrentTtsJobs", "Max concurrent TTS jobs", "Maximum number of TTS generation jobs allowed to run concurrently. No job currently reads this field — display only.", 2, min: 1, isRuntimeEffective: false),
        ],
    };

    public static readonly FeatureGateGroupDefinition PracticeGymGenerationPerType = new()
    {
        GroupKey = "practice-gym-generation-per-type",
        DisplayName = "Practice Gym generation (per exercise type)",
        Description = "Controls the Practice Gym exercise cache per exercise type.",
        Category = FeatureGateCategory.ReadinessPoolLessonGeneration,
        BackingStore = FeatureGateBackingStore.LessonGenerationSettingsTable,
        Settings =
        [
            Int("LessonGeneration.PracticeGymReadyExercisesPerType", "Ready exercises per type", "Target number of ready Practice Gym exercises cached per exercise type. No job currently reads this field — display only.", 10, min: 1, isRuntimeEffective: false),
            Int("LessonGeneration.PracticeGymRefillThresholdPerType", "Refill threshold per type", "Ready-count at or below which a refill is triggered for a given exercise type. Must stay below the ready count.", 3, min: 0),
            Int("LessonGeneration.PracticeGymRefillCountPerType", "Refill count per type", "Number of exercises generated per refill for a given exercise type.", 7, min: 1),
        ],
    };

    public static readonly FeatureGateGroupDefinition AiSignalSafetySpeaking = new()
    {
        GroupKey = "ai-signal-safety-speaking",
        DisplayName = "AI signal safety — Speaking",
        Description = "Controls how much trust the speaking-evaluation AI is given to affect student mastery data. All fields are read-only in this phase.",
        Category = FeatureGateCategory.AiSignalSafety,
        BackingStore = FeatureGateBackingStore.AppSettingsReadOnly,
        IsReadOnly = true,
        RequiresRestart = true,
        WarningText = "These gates default conservative by design and are not editable at runtime in this phase. Changing them requires an appsettings edit and redeploy, with product sign-off.",
        Settings =
        [
            ReadOnlyBool("Speaking.ApplyMasterySignals", "Apply mastery signals", "When on, completed speaking evaluations may update StudentSkillProfile/StudentLearningEvent.", false),
            ReadOnlyString("Speaking.MinimumConfidenceForMasterySignal", "Minimum confidence for mastery signal", "Minimum confidence band required before any signal is applied.", "High"),
            ReadOnlyBool("Speaking.AllowReviewSignals", "Allow review signals", "When on (and ApplyMasterySignals is on), low-scoring evaluations may produce a weakness/review learning event.", true),
            ReadOnlyBool("Speaking.AllowPositiveSignals", "Allow positive signals", "When on (and ApplyMasterySignals is on), high-scoring evaluations may produce a positive learning event.", false),
            HardcodedBool("Speaking.AllowObjectiveCompletion", "AI can complete objectives", "Objective completion from speaking AI is always disabled in code — not configurable.", false),
            HardcodedBool("Speaking.AllowCefrUpdate", "AI can update CEFR", "CEFR updates from speaking AI are always disabled in code — not configurable.", false),
        ],
    };

    public static readonly FeatureGateGroupDefinition AiSignalSafetyWriting = new()
    {
        GroupKey = "ai-signal-safety-writing",
        DisplayName = "AI signal safety — Writing",
        Description = "Controls how much trust the writing-evaluation AI is given to affect student mastery data. All fields are read-only in this phase.",
        Category = FeatureGateCategory.AiSignalSafety,
        BackingStore = FeatureGateBackingStore.AppSettingsReadOnly,
        IsReadOnly = true,
        RequiresRestart = true,
        WarningText = "These gates default conservative by design and are not editable at runtime in this phase. Changing them requires an appsettings edit and redeploy, with product sign-off.",
        Settings =
        [
            ReadOnlyBool("Writing.ApplyMasterySignals", "Apply mastery signals", "When on, completed writing evaluations may update StudentSkillProfile/StudentLearningEvent.", false),
            ReadOnlyString("Writing.MinimumConfidenceForMasterySignal", "Minimum confidence for mastery signal", "Minimum confidence band required before any signal is applied.", "High"),
            ReadOnlyBool("Writing.AllowReviewSignals", "Allow review signals", "When on (and ApplyMasterySignals is on), low-scoring evaluations may produce a weakness/review learning event.", true),
            ReadOnlyBool("Writing.AllowPositiveSignals", "Allow positive signals", "When on (and ApplyMasterySignals is on), high-scoring evaluations may produce a positive learning event.", false),
            HardcodedBool("Writing.AllowObjectiveCompletion", "AI can complete objectives", "Objective completion from writing AI is always disabled in code — not configurable.", false),
            HardcodedBool("Writing.AllowCefrUpdate", "AI can update CEFR", "CEFR updates from writing AI are always disabled in code — not configurable.", false),
        ],
    };

    public static readonly FeatureGateGroupDefinition LearningPlanRegeneration = new()
    {
        GroupKey = "learning-plan-regeneration",
        DisplayName = "AI Learning Plan regeneration",
        Description = "Learning Plan regeneration is currently controlled by internal application logic (GetOrCreatePlanAsync / mastery-driven triggers), not by a dedicated runtime flag. No safe runtime toggle exists yet, so this entry is informational only.",
        Category = FeatureGateCategory.AiSignalSafety,
        BackingStore = FeatureGateBackingStore.Informational,
        IsReadOnly = true,
        Settings = [],
    };

    public static readonly FeatureGateGroupDefinition ActivityFeedbackPolicy = new()
    {
        GroupKey = "activity-feedback-policy",
        DisplayName = "Activity feedback policy",
        Description = "Controls whether students are prompted for difficulty/clarity/usefulness/repeat feedback after completing an activity, per surface (Today lesson vs Practice Gym). See docs/reviews/2026-07-08-bank-first-ai-teaching-clean-architecture-plan.md (Phase B2).",
        Category = FeatureGateCategory.ActivityFeedback,
        BackingStore = FeatureGateBackingStore.ReadinessPoolOverride,
        WarningText = "Setting a surface to 'Required' means the client will not let the student skip the feedback prompt for that surface.",
        Settings =
        [
            new FeatureGateSettingDefinition
            {
                Key = "ActivityFeedback.TodayPolicy",
                DisplayName = "Today lesson feedback policy",
                Description = "Whether Today-lesson activity completions prompt the student for feedback: Off, Optional (skippable), or Required.",
                DataType = FeatureGateDataType.String,
                DefaultValueJson = "\"Optional\"",
                IsEditableAtRuntime = true,
                RiskLevel = FeatureGateRiskLevel.Low,
                AllowedValues = ["Off", "Optional", "Required"],
            },
            new FeatureGateSettingDefinition
            {
                Key = "ActivityFeedback.PracticeGymPolicy",
                DisplayName = "Practice Gym feedback policy",
                Description = "Whether Practice Gym activity completions prompt the student for feedback: Off, Optional (skippable), or Required.",
                DataType = FeatureGateDataType.String,
                DefaultValueJson = "\"Optional\"",
                IsEditableAtRuntime = true,
                RiskLevel = FeatureGateRiskLevel.Low,
                AllowedValues = ["Off", "Optional", "Required"],
            },
        ],
    };

    private static FeatureGateSettingDefinition Int(string key, string displayName, string description, int defaultValue, int? min = null, int? max = null, bool isRuntimeEffective = true) => new()
    {
        Key = key,
        DisplayName = displayName,
        Description = description,
        DataType = FeatureGateDataType.Integer,
        DefaultValueJson = defaultValue.ToString(),
        IsEditableAtRuntime = true,
        IsRuntimeEffective = isRuntimeEffective,
        RiskLevel = FeatureGateRiskLevel.Low,
        MinValue = min,
        MaxValue = max,
    };

    private static FeatureGateSettingDefinition ReadOnlyBool(string key, string displayName, string description, bool defaultValue) => new()
    {
        Key = key,
        DisplayName = displayName,
        Description = description,
        DataType = FeatureGateDataType.Boolean,
        DefaultValueJson = defaultValue ? "true" : "false",
        IsEditableAtRuntime = false,
        RiskLevel = FeatureGateRiskLevel.Critical,
    };

    private static FeatureGateSettingDefinition ReadOnlyString(string key, string displayName, string description, string defaultValue) => new()
    {
        Key = key,
        DisplayName = displayName,
        Description = description,
        DataType = FeatureGateDataType.String,
        DefaultValueJson = $"\"{defaultValue}\"",
        IsEditableAtRuntime = false,
        RiskLevel = FeatureGateRiskLevel.Medium,
    };

    private static FeatureGateSettingDefinition HardcodedBool(string key, string displayName, string description, bool defaultValue) => new()
    {
        Key = key,
        DisplayName = displayName,
        Description = description,
        DataType = FeatureGateDataType.Boolean,
        DefaultValueJson = defaultValue ? "true" : "false",
        IsEditableAtRuntime = false,
        RiskLevel = FeatureGateRiskLevel.Critical,
    };
}
