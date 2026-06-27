using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Placement;

/// <summary>
/// Deterministic adaptive placement assessment service (Phase 13A).
/// No AI calls — uses a seeded item bank for the foundation phase.
/// Real adaptive item generation from AI is deferred to Phase 13B+.
/// </summary>
public sealed class PlacementAssessmentService : IPlacementAssessmentService
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILearningPlanService _learningPlan;
    private readonly PlacementAssessmentOptions _opts;
    private readonly ILogger<PlacementAssessmentService> _logger;

    public PlacementAssessmentService(
        LinguaCoachDbContext db,
        ILearningPlanService learningPlan,
        IOptions<PlacementAssessmentOptions> opts,
        ILogger<PlacementAssessmentService> logger)
    {
        _db = db;
        _learningPlan = learningPlan;
        _opts = opts.Value;
        _logger = logger;
    }

    // ── Item bank (deterministic seed, Phase 13A) ──────────────────────────────

    private record PlacementItemTemplate(string Skill, string CefrLevel, string ItemType, string Prompt, string CorrectAnswer);

    private static readonly List<PlacementItemTemplate> ItemBank =
    [
        // Grammar
        new("grammar", "A1", "multiple_choice", "Which is correct? 'I ___ happy.' (A) am (B) is (C) are", "A"),
        new("grammar", "A1", "multiple_choice", "Choose: 'She ___ a teacher.' (A) am (B) is (C) are", "B"),
        new("grammar", "A1", "gap_fill", "Complete: 'They ___ students.' (am/is/are)", "are"),

        new("grammar", "A2", "multiple_choice", "Which is past tense? 'Yesterday I ___ to school.' (A) go (B) went (C) gone", "B"),
        new("grammar", "A2", "multiple_choice", "Choose: 'We have ___ the report.' (A) wrote (B) write (C) written", "C"),
        new("grammar", "A2", "gap_fill", "Complete: 'She ___ working here since 2020.' (has/have/had)", "has"),

        new("grammar", "B1", "multiple_choice", "Select: 'If I ___ more time, I would study harder.' (A) have (B) had (C) has", "B"),
        new("grammar", "B1", "multiple_choice", "Choose: 'The report ___ by the manager tomorrow.' (A) will review (B) will be reviewed (C) reviewed", "B"),
        new("grammar", "B1", "gap_fill", "Complete: 'She suggested ___ the meeting.' (postpone/postponing/to postpone)", "postponing"),

        new("grammar", "B2", "multiple_choice", "Select: 'Had she known, she ___ earlier.' (A) would arrive (B) would have arrived (C) had arrived", "B"),
        new("grammar", "B2", "multiple_choice", "Choose the correct form: 'The data ___ been analysed.' (A) have (B) has (C) had", "A"),
        new("grammar", "B2", "gap_fill", "Complete: 'It is essential that he ___ on time.' (be/is/was)", "be"),

        // Vocabulary
        new("vocabulary", "A1", "multiple_choice", "What does 'big' mean? (A) large (B) small (C) fast", "A"),
        new("vocabulary", "A1", "multiple_choice", "Which word means 'to eat in the morning'? (A) breakfast (B) lunch (C) dinner", "A"),
        new("vocabulary", "A1", "gap_fill", "Fill in: 'I drink ___ every morning.' (water/sky/chair)", "water"),

        new("vocabulary", "A2", "multiple_choice", "What is a synonym for 'happy'? (A) sad (B) cheerful (C) angry", "B"),
        new("vocabulary", "A2", "multiple_choice", "Choose the correct word: 'The meeting was ___.' (A) postponed (B) posting (C) post", "A"),
        new("vocabulary", "A2", "gap_fill", "Complete: 'She gave a ___ speech at the event.' (powerful/power/powering)", "powerful"),

        new("vocabulary", "B1", "multiple_choice", "What does 'ambiguous' mean? (A) unclear (B) obvious (C) simple", "A"),
        new("vocabulary", "B1", "multiple_choice", "Choose the best word: 'The contract was ___.' (A) terminated (B) terminating (C) terminate", "A"),
        new("vocabulary", "B1", "gap_fill", "Fill in: 'His answer was ___; nobody understood it.' (vague/clear/exact)", "vague"),

        new("vocabulary", "B2", "multiple_choice", "What does 'ubiquitous' mean? (A) rare (B) everywhere (C) ancient", "B"),
        new("vocabulary", "B2", "multiple_choice", "Select the most formal: (A) get (B) obtain (C) grab", "B"),
        new("vocabulary", "B2", "gap_fill", "Complete: 'The new policy was met with widespread ___.' (resistance/resist/resistant)", "resistance"),

        // Reading
        new("reading", "A1", "multiple_choice", "Read: 'The cat sat on the mat.' What did the cat do? (A) stand (B) sit (C) run", "B"),
        new("reading", "A1", "multiple_choice", "Read: 'John likes apples.' What fruit does John like? (A) oranges (B) apples (C) bananas", "B"),
        new("reading", "A1", "gap_fill", "Read: 'The dog is ___.' Complete with: (big/run/eat)", "big"),

        new("reading", "A2", "multiple_choice", "Read: 'She works in a hospital as a nurse.' Where does she work? (A) school (B) hospital (C) office", "B"),
        new("reading", "A2", "multiple_choice", "Read: 'The store opens at 9am and closes at 6pm.' When does it close? (A) 9am (B) 12pm (C) 6pm", "C"),
        new("reading", "A2", "gap_fill", "Read: 'He ___ a book every week.' (reads/eat/drives)", "reads"),

        new("reading", "B1", "multiple_choice", "Read: 'Despite the rain, the event was a success.' What happened? (A) cancelled (B) successful (C) postponed", "B"),
        new("reading", "B1", "multiple_choice", "Read: 'The study concluded that exercise improves mood.' What does the study show? (A) diet affects sleep (B) exercise improves mood (C) rest reduces stress", "B"),
        new("reading", "B1", "gap_fill", "Read: 'The report highlights several ___.' (concerns/concerned/concern)", "concerns"),

        new("reading", "B2", "multiple_choice", "The passage implies that the author: (A) supports the policy (B) questions its effectiveness (C) ignores the data", "B"),
        new("reading", "B2", "multiple_choice", "The word 'mitigate' in the text most closely means: (A) worsen (B) reduce (C) ignore", "B"),
        new("reading", "B2", "gap_fill", "Complete the inference: 'The author suggests that the problem is ___.' (systemic/individual/minor)", "systemic"),

        // Listening (simulated with text descriptions)
        new("listening", "A1", "multiple_choice", "You hear: 'Turn left at the traffic lights.' Where do you turn? (A) right (B) left (C) straight", "B"),
        new("listening", "A1", "multiple_choice", "You hear: 'The price is five euros.' How much is it? (A) 3 euros (B) 15 euros (C) 5 euros", "C"),
        new("listening", "A1", "gap_fill", "You hear: 'My name is ___.' (Maria/Monday/Morning)", "Maria"),

        new("listening", "A2", "multiple_choice", "You hear: 'The meeting is on Friday at 3pm.' When is the meeting? (A) Thursday 3pm (B) Friday 3pm (C) Friday 5pm", "B"),
        new("listening", "A2", "multiple_choice", "You hear a weather report: 'Expect rain in the afternoon.' When will it rain? (A) morning (B) afternoon (C) evening", "B"),
        new("listening", "A2", "gap_fill", "You hear: 'Please ___ at reception.' (arrive/register/leave)", "register"),

        new("listening", "B1", "multiple_choice", "You hear a complaint about slow service. What is the caller's main concern? (A) price (B) quality (C) speed", "C"),
        new("listening", "B1", "multiple_choice", "You hear: 'We need to consider both sides before deciding.' What does the speaker suggest? (A) decide quickly (B) consider both sides (C) avoid the decision", "B"),
        new("listening", "B1", "gap_fill", "You hear: 'The deadline has been ___.' (extended/shortened/cancelled)", "extended"),

        new("listening", "B2", "multiple_choice", "You hear a debate. The second speaker's tone is: (A) dismissive (B) conciliatory (C) aggressive", "B"),
        new("listening", "B2", "multiple_choice", "The speaker implies the proposal: (A) is fully funded (B) needs revision (C) has been rejected", "B"),
        new("listening", "B2", "gap_fill", "You hear: 'The analysis was ___.' (inconclusive/concluded/conclusive)", "inconclusive"),

        // Writing (self-assessment proxy - deterministic)
        new("writing", "A1", "multiple_choice", "Which sentence is correct? (A) 'I writed a letter.' (B) 'I wrote a letter.' (C) 'I writing a letter.'", "B"),
        new("writing", "A1", "multiple_choice", "Which is correctly punctuated? (A) 'hello how are you' (B) 'Hello, how are you?' (C) 'Hello how are you!'", "B"),
        new("writing", "A1", "gap_fill", "Choose the correct sentence ending: 'She ___ to school every day.' (go/goes/going)", "goes"),

        new("writing", "A2", "multiple_choice", "Which opening is best for a formal email? (A) 'Hey!' (B) 'Dear Sir/Madam,' (C) 'Yo!'", "B"),
        new("writing", "A2", "multiple_choice", "Which is a complete sentence? (A) 'Running fast.' (B) 'She runs fast.' (C) 'Fast running.'", "B"),
        new("writing", "A2", "gap_fill", "Complete: '___ you for your email.' (Thank/Thanks/Thanking)", "Thank"),

        new("writing", "B1", "multiple_choice", "Which transition best shows contrast? (A) Furthermore (B) However (C) Therefore", "B"),
        new("writing", "B1", "multiple_choice", "Which is the most concise? (A) 'Due to the fact that' (B) 'Because' (C) 'Owing to the reason that'", "B"),
        new("writing", "B1", "gap_fill", "Complete the formal closing: '___ regards,' (Best/Good/Fine)", "Best"),

        new("writing", "B2", "multiple_choice", "Which best hedges a claim? (A) 'It is certain that' (B) 'Evidence suggests that' (C) 'Everyone knows that'", "B"),
        new("writing", "B2", "multiple_choice", "Which shows strongest cohesion? (A) 'And also too' (B) 'Moreover' (C) 'Plus also'", "B"),
        new("writing", "B2", "gap_fill", "Complete: 'The findings ___ that further research is needed.' (indicate/indicates/indicating)", "indicate"),

        // Speaking (self-assessment proxy)
        new("speaking", "A1", "multiple_choice", "How would you greet someone in the morning? (A) 'Good morning!' (B) 'Good night!' (C) 'Goodbye!'", "A"),
        new("speaking", "A1", "multiple_choice", "How do you ask for a price? (A) 'How much is it?' (B) 'Where is it?' (C) 'When is it?'", "A"),
        new("speaking", "A1", "gap_fill", "Complete: '___ me to your manager, please.' (Take/Introduce/Tell)", "Introduce"),

        new("speaking", "A2", "multiple_choice", "How do you politely decline an invitation? (A) 'No way!' (B) 'I'm afraid I can't make it.' (C) 'That's boring.'", "B"),
        new("speaking", "A2", "multiple_choice", "Which is more polite? (A) 'Give me water.' (B) 'Could I have some water, please?' (C) 'Water now.'", "B"),
        new("speaking", "A2", "gap_fill", "Complete: 'I ___ if you could help me.' (wonder/wondering/wondered)", "wonder"),

        new("speaking", "B1", "multiple_choice", "How do you interrupt politely in a meeting? (A) 'Stop talking!' (B) 'Sorry to interrupt, but...' (C) 'Be quiet!'", "B"),
        new("speaking", "B1", "multiple_choice", "Which phrase shows you're listening actively? (A) 'Whatever.' (B) 'I see what you mean.' (C) 'That's wrong.'", "B"),
        new("speaking", "B1", "gap_fill", "Complete: 'To ___ what you said...' (clarify/summarise/confirm)", "summarise"),

        new("speaking", "B2", "multiple_choice", "Which phrase best introduces a nuanced point? (A) 'Actually, it's complicated because...' (B) 'You're wrong.' (C) 'That's simple.'", "A"),
        new("speaking", "B2", "multiple_choice", "How do you diplomatically disagree? (A) 'That's completely wrong.' (B) 'I see your point, but I would argue that...' (C) 'I disagree.'", "B"),
        new("speaking", "B2", "gap_fill", "Complete: 'To ___ the discussion, I would like to add...' (build on/ignore/dismiss)", "build on"),
    ];

    private static readonly string[] CefrLevels = ["A1", "A2", "B1", "B2"];

    // ── Scoring algorithm ───────────────────────────────────────────────────────

    private static (string estimatedLevel, double confidence, int evidenceCount) ScoreSkill(
        IEnumerable<PlacementAssessmentItem> items,
        string fallbackLevel)
    {
        var skillItems = items.Where(i => i.IsCorrect.HasValue).ToList();
        if (skillItems.Count == 0)
            return (fallbackLevel, 0.0, 0);

        var levelStats = CefrLevels.ToDictionary(
            level => level,
            level =>
            {
                var levelItems = skillItems.Where(i => i.TargetCefrLevel == level).ToList();
                var correct = levelItems.Count(i => i.IsCorrect == true);
                return (Total: levelItems.Count, Correct: correct);
            });

        // Find highest level where pass rate >= 70% (with at least 1 item)
        string? highestPassed = null;
        foreach (var level in CefrLevels)
        {
            var (total, correct) = levelStats[level];
            if (total == 0) continue;
            var rate = (double)correct / total;
            if (rate >= 0.70)
                highestPassed = level;
            else if (rate < 0.40)
                break; // Failed — stop climbing
        }

        var estimated = highestPassed ?? fallbackLevel;
        var totalItems = skillItems.Count;
        var totalCorrect = skillItems.Count(i => i.IsCorrect == true);
        var confidence = totalItems > 0
            ? Math.Min((double)totalItems / 6.0, (double)totalCorrect / totalItems)
            : 0.0;

        return (estimated, Math.Round(confidence, 3), totalItems);
    }

    private static string ComputeOverallCefr(IReadOnlyList<string> perSkillLevels, string fallback)
    {
        if (perSkillLevels.Count == 0) return fallback;

        var indices = perSkillLevels
            .Select(l => Array.IndexOf(CefrLevels, l))
            .Where(i => i >= 0)
            .ToList();

        if (indices.Count == 0) return fallback;

        // Conservative: use the lowest level
        return CefrLevels[indices.Min()];
    }

    // ── DTO builders ────────────────────────────────────────────────────────────

    private static PlacementAssessmentSummaryDto ToSummaryDto(
        PlacementAssessment assessment,
        IReadOnlyList<PlacementSkillResult> skillResults,
        bool learningPlanRegenerated = false,
        string? learningPlanWarning = null)
    {
        var skillDtos = skillResults.Select(r => new PlacementSkillResultDto(
            r.Skill,
            r.EstimatedCefrLevel,
            r.Confidence,
            r.EvidenceCount,
            r.Strengths,
            r.Weaknesses,
            (r.RecommendedStartingObjectiveKeys
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .ToList()
                ?? new List<string>()) as IReadOnlyList<string>
        )).ToList();

        return new PlacementAssessmentSummaryDto(
            assessment.Id,
            assessment.StudentProfileId,
            assessment.Status.ToString(),
            assessment.StartedAtUtc,
            assessment.CompletedAtUtc,
            assessment.ExpiredAtUtc,
            assessment.OverallEstimatedLevel,
            assessment.OverallConfidence,
            assessment.IsProvisional,
            assessment.ResultSummary,
            assessment.Source,
            skillDtos,
            learningPlanRegenerated,
            learningPlanWarning,
            assessment.Items.Count);
    }

    private static PlacementHistoryItemDto ToHistoryDto(PlacementAssessment a) =>
        new(a.Id, a.Status.ToString(), a.StartedAtUtc, a.CompletedAtUtc,
            a.OverallEstimatedLevel, a.OverallConfidence, a.IsProvisional, a.Items.Count);

    // ── IPlacementAssessmentService ─────────────────────────────────────────────

    public async Task<PlacementAssessmentSummaryDto> StartAssessmentAsync(
        Guid studentProfileId, string source, CancellationToken ct = default)
    {
        // Idempotent: return existing InProgress assessment
        var existing = await _db.PlacementAssessments
            .Include(a => a.Items)
            .Where(a => a.StudentProfileId == studentProfileId && a.Status == PlacementStatus.InProgress)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            var existingResults = await _db.PlacementSkillResults
                .Where(r => r.PlacementAssessmentId == existing.Id)
                .ToListAsync(ct);
            return ToSummaryDto(existing, existingResults);
        }

        // Determine starting CEFR level from profile
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.Id == studentProfileId, ct)
            ?? throw new InvalidOperationException($"Student profile {studentProfileId} not found.");

        var startingLevel = !string.IsNullOrWhiteSpace(profile.CefrLevel)
            ? profile.CefrLevel
            : _opts.StartingLevelFallback;

        // Create adaptive assessment via factory method
        var assessment = PlacementAssessment.CreateAdaptive(studentProfileId, source);
        assessment.Start();

        _db.PlacementAssessments.Add(assessment);
        await _db.SaveChangesAsync(ct);

        // Seed initial items from bank
        var items = CreateInitialItems(assessment.Id, startingLevel);
        _db.PlacementAssessmentItems.AddRange(items);
        await _db.SaveChangesAsync(ct);

        // Reload with items navigation populated
        var loaded = await _db.PlacementAssessments
            .Include(a => a.Items)
            .FirstAsync(a => a.Id == assessment.Id, ct);

        return ToSummaryDto(loaded, new List<PlacementSkillResult>());
    }

    private List<PlacementAssessmentItem> CreateInitialItems(Guid assessmentId, string startingLevel)
    {
        var items = new List<PlacementAssessmentItem>();
        var order = 0;

        var startIdx = Array.IndexOf(CefrLevels, startingLevel);
        if (startIdx < 0) startIdx = 1; // Default to A2

        foreach (var skill in _opts.SkillsToAssess)
        {
            // 2 items at starting level
            var atLevel = ItemBank
                .Where(t => t.Skill == skill && t.CefrLevel == CefrLevels[startIdx])
                .Take(2);

            foreach (var template in atLevel)
            {
                items.Add(PlacementAssessmentItem.Create(
                    assessmentId, template.Skill, template.CefrLevel,
                    template.ItemType, template.Prompt, template.CorrectAnswer, order++));
            }

            // 1 item at level above if possible
            if (startIdx + 1 < CefrLevels.Length)
            {
                var aboveLevel = ItemBank
                    .Where(t => t.Skill == skill && t.CefrLevel == CefrLevels[startIdx + 1])
                    .Take(1);

                foreach (var template in aboveLevel)
                {
                    items.Add(PlacementAssessmentItem.Create(
                        assessmentId, template.Skill, template.CefrLevel,
                        template.ItemType, template.Prompt, template.CorrectAnswer, order++));
                }
            }
        }

        return items;
    }

    public async Task<PlacementAssessmentSummaryDto?> GetLatestAssessmentAsync(
        Guid studentProfileId, CancellationToken ct = default)
    {
        var assessment = await _db.PlacementAssessments
            .Include(a => a.Items)
            .Where(a => a.StudentProfileId == studentProfileId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (assessment is null) return null;

        var skillResults = await _db.PlacementSkillResults
            .Where(r => r.PlacementAssessmentId == assessment.Id)
            .ToListAsync(ct);

        return ToSummaryDto(assessment, skillResults);
    }

    public async Task<IReadOnlyList<PlacementHistoryItemDto>> GetHistoryAsync(
        Guid studentProfileId, CancellationToken ct = default)
    {
        var assessments = await _db.PlacementAssessments
            .Include(a => a.Items)
            .Where(a => a.StudentProfileId == studentProfileId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return assessments.Select(ToHistoryDto).ToList();
    }

    public async Task<PlacementAssessmentSummaryDto> CompleteAssessmentAsync(
        Guid assessmentId, CancellationToken ct = default)
    {
        var assessment = await _db.PlacementAssessments
            .Include(a => a.Items)
            .FirstOrDefaultAsync(a => a.Id == assessmentId, ct)
            ?? throw new InvalidOperationException($"Assessment {assessmentId} not found.");

        if (assessment.Status == PlacementStatus.Completed)
        {
            var existingResults = await _db.PlacementSkillResults
                .Where(r => r.PlacementAssessmentId == assessmentId)
                .ToListAsync(ct);
            return ToSummaryDto(assessment, existingResults);
        }

        // Simulate responses for items without responses
        // Deterministic: 7/10 correct by item order
        foreach (var item in assessment.Items.Where(i => !i.IsCorrect.HasValue))
        {
            var isCorrect = item.ItemOrder % 10 < 7;
            item.RecordResponse(
                isCorrect ? item.CorrectAnswer ?? "A" : "wrong",
                isCorrect,
                isCorrect ? 1.0 : 0.0);
        }

        await _db.SaveChangesAsync(ct);

        // Score per skill
        var skillResults = new List<PlacementSkillResult>();
        var perSkillLevels = new List<string>();

        foreach (var skill in _opts.SkillsToAssess)
        {
            var skillItems = assessment.Items.Where(i => i.Skill == skill).ToList();
            var (level, confidence, evidenceCount) = ScoreSkill(skillItems, _opts.StartingLevelFallback);

            perSkillLevels.Add(level);
            var result = PlacementSkillResult.Create(
                assessmentId, skill, level, confidence, evidenceCount,
                evidenceCount >= 2 ? $"Demonstrated {level} competency in {skill}" : null,
                evidenceCount < 2 ? $"Insufficient evidence for {skill}" : null);

            _db.PlacementSkillResults.Add(result);
            skillResults.Add(result);
        }

        // Compute overall level and confidence
        var overallCefr = ComputeOverallCefr(perSkillLevels, _opts.StartingLevelFallback);
        var overallConfidence = skillResults.Count > 0
            ? Math.Round(skillResults.Average(r => r.Confidence), 3)
            : 0.0;

        var isProvisional = overallConfidence < _opts.ConfidenceThreshold;
        var resultSummary = $"Estimated level: {overallCefr}. Confidence: {overallConfidence:P0}. " +
                            (isProvisional ? "Provisional — more evidence needed." : "Sufficient evidence.");

        assessment.CompleteAdaptive(overallCefr, overallConfidence, resultSummary, isProvisional);

        // Update StudentProfile.CefrLevel if confidence is sufficient
        if (overallConfidence >= 0.6 && !string.IsNullOrWhiteSpace(overallCefr))
        {
            var profile = await _db.StudentProfiles
                .FirstOrDefaultAsync(p => p.Id == assessment.StudentProfileId, ct);
            profile?.AdminSetCefrLevel(overallCefr);
        }

        await _db.SaveChangesAsync(ct);

        // Attempt learning plan regeneration
        var learningPlanRegenerated = false;
        string? learningPlanWarning = null;
        try
        {
            await _learningPlan.RegeneratePlanAsync(assessment.StudentProfileId, "placement_completed", ct);
            learningPlanRegenerated = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Learning plan regeneration failed after placement completion for student {StudentId}",
                assessment.StudentProfileId);
            learningPlanWarning = ex.Message;
        }

        return ToSummaryDto(assessment, skillResults, learningPlanRegenerated, learningPlanWarning);
    }

    public async Task AbandonAssessmentAsync(Guid assessmentId, CancellationToken ct = default)
    {
        var assessment = await _db.PlacementAssessments
            .FirstOrDefaultAsync(a => a.Id == assessmentId, ct)
            ?? throw new InvalidOperationException($"Assessment {assessmentId} not found.");

        assessment.Abandon();
        await _db.SaveChangesAsync(ct);
    }
}
