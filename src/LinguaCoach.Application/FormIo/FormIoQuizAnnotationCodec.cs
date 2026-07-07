using System.Text.Json;
using System.Text.Json.Nodes;
using LinguaCoach.Application.Placement;

namespace LinguaCoach.Application.FormIo;

/// <summary>Pure JSON manipulation shared by both directions of Quiz-tab authoring: splitting an
/// admin-annotated schema into student-safe schema + scoring rules (used at save time by the
/// admin handlers), and the reverse — embedding an existing ScoringRulesJson back onto a schema's
/// matching components as "quiz" annotations (used to backfill items/versions that had scoring
/// before the Quiz tab existed, so the builder shows their existing answers instead of "Disabled").
/// No I/O, no DI — callable directly from Persistence's seeders (which cannot reference
/// Infrastructure) as well as from Infrastructure's DI-registered <c>IFormIoQuizSchemaSplitter</c>.</summary>
public static class FormIoQuizAnnotationCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly string[] ContainerComponentArrayProps = { "components", "columns", "rows" };

    /// <summary>Splits an admin-authored Form.io schema (with inline per-component "quiz"
    /// annotations) into a student-safe schema and a backend-only ScoringRulesDocument. Every
    /// "quiz" property is unconditionally deleted from the walked component — by key, not by
    /// round-tripping through a strongly-typed DTO — regardless of its enabled value or shape, so
    /// a malformed or unexpected "quiz" node can never survive into the student-facing schema.</summary>
    public static FormIoQuizSplitResult Split(string authoringSchemaJson)
    {
        var root = JsonNode.Parse(authoringSchemaJson)
            ?? throw new InvalidOperationException("Authoring schema JSON must not be null.");

        var scoringComponents = new Dictionary<string, ComponentScoringRule>(StringComparer.Ordinal);

        if (root is JsonObject rootObj && rootObj["components"] is JsonArray topComponents)
            WalkArray(topComponents, WalkComponentSplit, scoringComponents);
        else if (root is JsonArray bareArray)
            WalkArray(bareArray, WalkComponentSplit, scoringComponents);

        var studentSchemaJson = root.ToJsonString(JsonOptions);
        var scoringDoc = new ScoringRulesDocument(scoringComponents);
        var scoringRulesJson = JsonSerializer.Serialize(scoringDoc, JsonOptions);

        return new FormIoQuizSplitResult(studentSchemaJson, scoringRulesJson);
    }

    /// <summary>The reverse of <see cref="Split"/>: given a student-safe schema and its paired
    /// ScoringRulesJson (in either the current <c>{"components":{...}}</c> shape or the legacy
    /// pre-Quiz-tab flat <c>{"key":{"correctAnswerKey":...}}</c> shape), re-embeds each rule onto
    /// its matching component (by key) as <c>component.quiz = { enabled: true, rule }</c>, so the
    /// Quiz tab shows the existing answer instead of "Disabled". Components whose key has no
    /// matching rule are left untouched. Returns the schema unchanged if there are no rules to embed.</summary>
    public static string Embed(string formIoSchemaJson, string? scoringRulesJson)
    {
        var rules = ParseScoringRules(scoringRulesJson);
        if (rules.Count == 0) return formIoSchemaJson;

        var root = JsonNode.Parse(formIoSchemaJson)
            ?? throw new InvalidOperationException("Form.io schema JSON must not be null.");

        if (root is JsonObject rootObj && rootObj["components"] is JsonArray topComponents)
            WalkArray(topComponents, WalkComponentEmbed, rules);
        else if (root is JsonArray bareArray)
            WalkArray(bareArray, WalkComponentEmbed, rules);

        return root.ToJsonString(JsonOptions);
    }

    /// <summary>Parses a ScoringRulesJson document into the unified per-component rule shape,
    /// supporting both the current <c>{"components": {"key": {"kind":..., "correctAnswer":...}}}</c>
    /// shape and the legacy pre-Quiz-tab flat <c>{"key": {"correctAnswerKey": "..."}}</c> shape
    /// (onboarding-only, never re-migrated — read compatibly forever).</summary>
    public static Dictionary<string, ComponentScoringRule> ParseScoringRules(string? scoringRulesJson)
    {
        var empty = new Dictionary<string, ComponentScoringRule>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(scoringRulesJson))
            return empty;

        try
        {
            using var doc = JsonDocument.Parse(scoringRulesJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("components", out var componentsEl) && componentsEl.ValueKind == JsonValueKind.Object)
            {
                var result = new Dictionary<string, ComponentScoringRule>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in componentsEl.EnumerateObject())
                {
                    var rule = JsonSerializer.Deserialize<ComponentScoringRule>(prop.Value.GetRawText(), JsonOptions);
                    if (rule is not null) result[prop.Name] = rule;
                }
                return result;
            }

            var legacy = new Dictionary<string, ComponentScoringRule>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Value.TryGetProperty("correctAnswerKey", out var cak) && cak.ValueKind == JsonValueKind.String)
                    legacy[prop.Name] = new ComponentScoringRule(ScoringRuleKinds.SingleChoice, CorrectAnswer: cak.GetString());
            }
            return legacy;
        }
        catch (JsonException)
        {
            return empty;
        }
    }

    /// <summary>Walks every component in a Form.io schema (recursing into nested
    /// panel/columns/table/wizard containers, same as <see cref="Split"/>/<see cref="Embed"/>) and
    /// returns the set of every component <c>key</c> found. Used to check which
    /// backend-recognized profile-mapped keys
    /// (<see cref="LinguaCoach.Application.Onboarding.OnboardingProfileFieldMapping"/>) a given
    /// onboarding schema actually contains.</summary>
    public static IReadOnlySet<string> CollectComponentKeys(string schemaJson)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var root = JsonNode.Parse(schemaJson);

        void Collect(JsonObject component, HashSet<string> state)
        {
            if (component["key"] is JsonValue keyVal && keyVal.TryGetValue<string>(out var key))
                state.Add(key);
        }

        if (root is JsonObject rootObj && rootObj["components"] is JsonArray topComponents)
            WalkArray(topComponents, Collect, keys);
        else if (root is JsonArray bareArray)
            WalkArray(bareArray, Collect, keys);

        return keys;
    }

    private static void WalkArray<TState>(JsonArray array, Action<JsonObject, TState> visit, TState state)
    {
        foreach (var node in array)
        {
            if (node is JsonObject component)
                WalkComponent(component, visit, state);
        }
    }

    private static void WalkComponent<TState>(JsonObject component, Action<JsonObject, TState> visit, TState state)
    {
        visit(component, state);

        foreach (var arrayProp in ContainerComponentArrayProps)
        {
            if (component[arrayProp] is not JsonArray arr) continue;

            foreach (var child in arr)
            {
                if (child is not JsonObject childObj) continue;

                // "columns"/"rows" in a Columns/Table component are arrays of cell-objects, each
                // with their own "components" array — handle one extra level, mirroring
                // FormIoSchemaValidationService's identical container-walk shape.
                if (childObj["components"] is JsonArray cellComps)
                    WalkArray(cellComps, visit, state);
                else if (childObj["type"] is not null)
                    WalkComponent(childObj, visit, state);
            }
        }
    }

    private static void WalkComponentSplit(JsonObject component, Dictionary<string, ComponentScoringRule> scoringComponents)
    {
        // Capture the quiz node before removing it — the removal itself must happen
        // unconditionally, even if the node is malformed/garbage, so best-effort extraction is
        // isolated in a try/catch around a snapshot taken beforehand rather than around the removal.
        var quizNode = component["quiz"];
        component.Remove("quiz");

        try
        {
            if (quizNode is JsonObject quizObj
                && quizObj["enabled"] is JsonValue enabledVal && enabledVal.GetValue<bool>()
                && component["key"] is JsonValue keyVal && keyVal.GetValue<string>() is { Length: > 0 } key
                && quizObj["rule"] is JsonObject ruleObj)
            {
                var rule = ruleObj.Deserialize<ComponentScoringRule>(JsonOptions);
                if (rule is not null) scoringComponents[key] = rule;
            }
        }
        catch (Exception)
        {
            // Malformed "quiz" annotation — ignored; "quiz" is already stripped above regardless.
        }
    }

    private static void WalkComponentEmbed(JsonObject component, Dictionary<string, ComponentScoringRule> rules)
    {
        if (component["key"] is JsonValue keyVal
            && keyVal.TryGetValue<string>(out var key)
            && rules.TryGetValue(key, out var rule))
        {
            component["quiz"] = JsonSerializer.SerializeToNode(new { enabled = true, rule }, JsonOptions);
        }
    }
}
