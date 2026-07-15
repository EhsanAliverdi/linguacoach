using System.Text.Json;
using LinguaCoach.Application.Onboarding;

namespace LinguaCoach.Infrastructure.Onboarding;

/// <summary>Validates Form.io schema JSON before it is ever saved/published — used by both the
/// onboarding template designer and the placement item-bank designer. Recursively walks nested
/// container components (panel/columns/table/wizard pages) and rejects anything outside the
/// approved allow-list, any script/eval-style property, external data sources, or answer/scoring
/// data leaking into what is meant to be a student-safe schema.</summary>
public sealed class FormIoSchemaValidationService : IFormIoSchemaValidationService
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "textfield", "textarea", "radio", "select", "selectboxes", "checkbox",
        "number", "email", "content", "panel", "columns", "table", "wizard", "form",
        // Form.io always auto-adds a submit button component to a new form/wizard page —
        // a plain submit button carries no script/data risk, so it's allowed.
        "button",
        // Custom components (registered client-side in shared/formio/components) — audioPlayer is
        // presentational-only (no answer data), speakingResponse carries only an uploaded audio
        // storage-key reference, never an authored correct answer.
        "audioPlayer", "speakingResponse",
        // Phase K21 — highlightWords (highlight_incorrect_words) carries only its own displayed
        // token text/ids, submitting the student's selected token ids as its answer value; the
        // set of *which* ids are correct lives only in ScoringRulesJson (backend-only), never in
        // this schema.
        "highlightWords",
        // Stock Form.io "Data Grid" (Phase C3, 2026-07-08 — reorder_paragraphs template
        // migration), used with its built-in "reorder" setting for drag-to-reorder lists. Its
        // own nested "components" (row template) are still recursively validated below via
        // ContainerComponentArrayProps, so this carries no more risk than any other container
        // type already allowed. "hidden" is a plain non-rendering data field (no script/eval
        // capability) used inside a datagrid row to carry each row's stable item id.
        "datagrid", "hidden"
    };

    private static readonly string[] ForbiddenScriptProperties =
    {
        "customConditional", "calculateValue", "customDefaultValue"
    };

    private static readonly string[] ForbiddenAnswerLeakKeys =
    {
        "correctAnswerKey", "correctAnswer", "correctAnswers", "score", "rubric", "scoringWeight",
        // Defense in depth for the Form.io builder's Quiz tab: IFormIoQuizSchemaSplitter is the
        // sole authority that strips "quiz" annotations before a schema is treated as
        // student-safe. This key should never survive that split — if it does (a splitter bug),
        // this independent check rejects the schema outright rather than silently serving it.
        "quiz"
    };

    private static readonly string[] ContainerComponentArrayProps = { "components", "columns", "rows" };

    public FormIoValidationResult ValidateSchema(string formIoSchemaJson)
    {
        if (string.IsNullOrWhiteSpace(formIoSchemaJson))
            return FormIoValidationResult.Fail("Schema JSON is required.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(formIoSchemaJson);
        }
        catch (JsonException ex)
        {
            return FormIoValidationResult.Fail($"Schema is not valid JSON: {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;
            var components = root.TryGetProperty("components", out var comps) && comps.ValueKind == JsonValueKind.Array
                ? comps
                : root; // allow a bare array-at-root shape too, checked below

            if (components.ValueKind == JsonValueKind.Array)
            {
                foreach (var component in components.EnumerateArray())
                {
                    var error = ValidateComponent(component);
                    if (error is not null) return FormIoValidationResult.Fail(error);
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var component in root.EnumerateArray())
                {
                    var error = ValidateComponent(component);
                    if (error is not null) return FormIoValidationResult.Fail(error);
                }
            }
            else
            {
                return FormIoValidationResult.Fail("Schema must have a top-level 'components' array.");
            }
        }

        return FormIoValidationResult.Ok();
    }

    private static bool HasMeaningfulValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => !string.IsNullOrWhiteSpace(el.GetString()),
        JsonValueKind.Null or JsonValueKind.Undefined => false,
        JsonValueKind.False => false,
        _ => true
    };

    private static string? ValidateComponent(JsonElement component)
    {
        if (component.ValueKind != JsonValueKind.Object)
            return "Each component must be a JSON object.";

        if (component.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
        {
            var type = typeEl.GetString()!;
            if (!AllowedTypes.Contains(type))
                return $"Component type '{type}' is not in the approved allow-list.";
        }
        else
        {
            return "Every component must declare a 'type'.";
        }

        // Form.io's builder stamps every component with these properties defaulted to an empty
        // string ("not used") — only actually-populated values represent real script/eval logic.
        foreach (var forbidden in ForbiddenScriptProperties)
        {
            if (component.TryGetProperty(forbidden, out var forbiddenEl) && HasMeaningfulValue(forbiddenEl))
                return $"Component contains disallowed script/eval-style property '{forbidden}'.";
        }

        // Nested "validate": { "custom": ... } is script-eval — forbidden, same empty-string caveat.
        if (component.TryGetProperty("validate", out var validateEl) && validateEl.ValueKind == JsonValueKind.Object
            && validateEl.TryGetProperty("custom", out var customEl) && HasMeaningfulValue(customEl))
        {
            return "Component contains disallowed 'validate.custom' script property.";
        }

        // External data sources are forbidden — only inline 'values' allowed.
        if (component.TryGetProperty("dataSrc", out var dataSrcEl) && dataSrcEl.ValueKind == JsonValueKind.String
            && !string.Equals(dataSrcEl.GetString(), "values", StringComparison.OrdinalIgnoreCase))
        {
            return $"Component dataSrc '{dataSrcEl.GetString()}' is not allowed — only inline 'values' data sources are permitted.";
        }

        // Answer/scoring data must never appear in a student-safe schema, including inside a
        // custom 'properties' bag.
        if (component.TryGetProperty("properties", out var propsEl) && propsEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in propsEl.EnumerateObject())
            {
                if (ForbiddenAnswerLeakKeys.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
                    return $"Component 'properties' bag contains disallowed answer/scoring key '{prop.Name}' — store this in ScoringRulesJson instead.";
            }
        }

        foreach (var leakKey in ForbiddenAnswerLeakKeys)
        {
            if (component.TryGetProperty(leakKey, out _))
                return $"Component contains disallowed answer/scoring key '{leakKey}' — store this in ScoringRulesJson instead.";
        }

        // Recurse into nested containers (panel/columns/table/wizard pages).
        foreach (var arrayProp in ContainerComponentArrayProps)
        {
            if (!component.TryGetProperty(arrayProp, out var arrEl)) continue;

            if (arrEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in arrEl.EnumerateArray())
                {
                    // "columns"/"rows" in a Columns/Table component are arrays of cell-objects,
                    // each with their own "components" array — handle one extra level.
                    if (child.ValueKind == JsonValueKind.Object && child.TryGetProperty("components", out var cellComps)
                        && cellComps.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var cellChild in cellComps.EnumerateArray())
                        {
                            var cellError = ValidateComponent(cellChild);
                            if (cellError is not null) return cellError;
                        }
                    }
                    else if (child.ValueKind == JsonValueKind.Object && child.TryGetProperty("type", out _))
                    {
                        var error = ValidateComponent(child);
                        if (error is not null) return error;
                    }
                }
            }
        }

        return null;
    }
}
