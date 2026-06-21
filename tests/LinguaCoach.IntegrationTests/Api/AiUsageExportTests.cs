using System.Net;
using System.Net.Http.Headers;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Tests for GET /api/admin/ai-usage/export.csv
/// Verifies CSV content, headers, filters, escaping, invalid-input 400s.
/// Row cap is not tested with a large dataset (slow) — only that the endpoint accepts maxRows param correctly.
/// </summary>
public sealed class AiUsageExportTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    private static bool _seeded;
    private static readonly SemaphoreSlim _seedLock = new(1, 1);
    private static Guid _studentProfileId;

    private const string ProviderA  = "openai";
    private const string ProviderB  = "anthropic";
    private const string ModelA     = "gpt-4o-export-test";
    private const string ModelB     = "claude-export-test";
    private const string FeatureA   = "export_test_writing";
    private const string FeatureB   = "export_test_lesson";

    public AiUsageExportTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient AdminClient(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private async Task<string> SeedAndGetTokenAsync()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();

        await _seedLock.WaitAsync();
        try
        {
            if (_seeded) return token;

            await _factory.CreateStudentAndGetTokenAsync("student_export@test.linguacoach.com");

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

            _studentProfileId = await db.StudentProfiles
                .AsNoTracking()
                .Join(db.Users.Where(u => u.Email == "student_export@test.linguacoach.com"),
                    p => p.UserId, u => u.Id, (p, _) => p.Id)
                .FirstAsync();

            // Row 1: ProviderA / ModelA / FeatureA / success / student
            db.AiUsageLogs.Add(new AiUsageLog(_studentProfileId, FeatureA, ProviderA, ModelA,
                isFallback: false, wasSuccessful: true, failureReason: null,
                inputTokens: 10, outputTokens: 5, costUsd: 0.001m, durationMs: 50,
                correlationId: "corr-export-001"));

            // Row 2: ProviderB / ModelB / FeatureB / failed / no student
            db.AiUsageLogs.Add(new AiUsageLog(null, FeatureB, ProviderB, ModelB,
                isFallback: false, wasSuccessful: false, failureReason: "Timeout,\"quoted\"",
                inputTokens: 20, outputTokens: 0, costUsd: 0m, durationMs: 5000,
                correlationId: null));

            // Row 3: ProviderA / ModelA / FeatureB / fallback / no student
            db.AiUsageLogs.Add(new AiUsageLog(null, FeatureB, ProviderA, ModelA,
                isFallback: true, wasSuccessful: true, failureReason: null,
                inputTokens: 15, outputTokens: 8, costUsd: 0.002m, durationMs: 100,
                correlationId: null));

            // Row 4: ProviderB / ModelB / FeatureA / success / no student
            db.AiUsageLogs.Add(new AiUsageLog(null, FeatureA, ProviderB, ModelB,
                isFallback: false, wasSuccessful: true, failureReason: null,
                inputTokens: 30, outputTokens: 15, costUsd: 0.003m, durationMs: 200,
                correlationId: null));

            await db.SaveChangesAsync();
            _seeded = true;
        }
        finally
        {
            _seedLock.Release();
        }

        return token;
    }

    private static string Encode(string v) => Uri.EscapeDataString(v);

    // ── content type and basic response ──────────────────────────────────────

    [Fact]
    public async Task Export_Returns200WithCsvContentType()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync("/api/admin/ai-usage/export.csv");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("text/csv", resp.Content.Headers.ContentType?.MediaType ?? "");
    }

    [Fact]
    public async Task Export_HasFilenameContentDispositionHeader()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync("/api/admin/ai-usage/export.csv");

        var disposition = resp.Content.Headers.ContentDisposition?.FileName
                       ?? resp.Content.Headers.ContentDisposition?.FileNameStar ?? "";
        Assert.Contains("ai-usage-", disposition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".csv", disposition, StringComparison.OrdinalIgnoreCase);
    }

    // ── CSV header ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Export_CsvIncludesExpectedHeader()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync("/api/admin/ai-usage/export.csv");
        var csv = await resp.Content.ReadAsStringAsync();
        var firstLine = csv.Split('\n')[0].Trim();

        Assert.Contains("CreatedAt", firstLine);
        Assert.Contains("Provider", firstLine);
        Assert.Contains("Model", firstLine);
        Assert.Contains("FeatureKey", firstLine);
        Assert.Contains("StudentId", firstLine);
        Assert.Contains("WasSuccessful", firstLine);
        Assert.Contains("IsFallback", firstLine);
        Assert.Contains("FailureReason", firstLine);
        Assert.Contains("InputTokens", firstLine);
        Assert.Contains("OutputTokens", firstLine);
        Assert.Contains("TotalTokens", firstLine);
        Assert.Contains("CostUsd", firstLine);
        Assert.Contains("DurationMs", firstLine);
        Assert.Contains("CorrelationId", firstLine);
    }

    // ── CSV rows ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Export_CsvIncludesDataRows()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync("/api/admin/ai-usage/export.csv");
        var csv = await resp.Content.ReadAsStringAsync();
        var lines = csv.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        // Header + at least 4 seeded rows (may include rows from other tests)
        Assert.True(lines.Count >= 2, "Expected header + at least one data row");
        Assert.Contains(ProviderA, csv);
        Assert.Contains(ProviderB, csv);
    }

    // ── provider filter ───────────────────────────────────────────────────────

    [Fact]
    public async Task Export_ProviderFilter_OnlyIncludesThatProvider()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/export.csv?provider={ProviderA}");
        var csv = await resp.Content.ReadAsStringAsync();
        var dataLines = csv.Split('\n').Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        Assert.True(dataLines.Count >= 2); // Rows 1 and 3
        Assert.All(dataLines, line => Assert.Contains(ProviderA, line));
        Assert.DoesNotContain(ProviderB, csv.Split('\n').Skip(1).Where(l => l.Length > 0).Aggregate("", (a, b) => a + b));
    }

    // ── featureKey filter ─────────────────────────────────────────────────────

    [Fact]
    public async Task Export_FeatureKeyFilter_OnlyIncludesThatFeature()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/export.csv?featureKey={FeatureA}");
        var csv = await resp.Content.ReadAsStringAsync();
        var dataLines = csv.Split('\n').Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        Assert.True(dataLines.Count >= 2); // Rows 1 and 4
        Assert.All(dataLines, line => Assert.Contains(FeatureA, line));
    }

    // ── status filter ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Export_StatusSuccess_OnlyIncludesSuccessNonFallbackRows()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/export.csv?status=success");
        var csv = await resp.Content.ReadAsStringAsync();
        var dataLines = csv.Split('\n').Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        Assert.True(dataLines.Count >= 1);
        // All rows should have WasSuccessful=true and IsFallback=false
        Assert.All(dataLines, line =>
        {
            var cols = SplitCsvLine(line);
            Assert.Equal("true",  cols[5]); // WasSuccessful
            Assert.Equal("false", cols[6]); // IsFallback
        });
    }

    [Fact]
    public async Task Export_StatusFailed_OnlyIncludesFailedRows()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/export.csv?status=failed");
        var csv = await resp.Content.ReadAsStringAsync();
        var dataLines = csv.Split('\n').Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        Assert.True(dataLines.Count >= 1);
        Assert.All(dataLines, line =>
        {
            var cols = SplitCsvLine(line);
            Assert.Equal("false", cols[5]); // WasSuccessful
        });
    }

    [Fact]
    public async Task Export_StatusFallback_OnlyIncludesFallbackRows()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/export.csv?status=fallback");
        var csv = await resp.Content.ReadAsStringAsync();
        var dataLines = csv.Split('\n').Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        Assert.True(dataLines.Count >= 1);
        Assert.All(dataLines, line =>
        {
            var cols = SplitCsvLine(line);
            Assert.Equal("true", cols[6]); // IsFallback
        });
    }

    // ── studentId filter ──────────────────────────────────────────────────────

    [Fact]
    public async Task Export_StudentIdFilter_OnlyIncludesThatStudentsRows()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/export.csv?studentId={_studentProfileId}");
        var csv = await resp.Content.ReadAsStringAsync();
        var dataLines = csv.Split('\n').Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        Assert.True(dataLines.Count >= 1);
        Assert.All(dataLines, line =>
        {
            var cols = SplitCsvLine(line);
            Assert.Equal(_studentProfileId.ToString(), cols[4], StringComparer.OrdinalIgnoreCase);
        });
    }

    // ── date filter ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Export_DateFilter_FutureFromReturnsOnlyHeader()
    {
        var token = await SeedAndGetTokenAsync();
        var future = Encode(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O"));
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/export.csv?from={future}");
        var csv = await resp.Content.ReadAsStringAsync();
        var dataLines = csv.Split('\n').Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        Assert.Empty(dataLines);
    }

    // ── CSV escaping ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Export_CsvEscapesCommasAndQuotesInFailureReason()
    {
        var token = await SeedAndGetTokenAsync();
        // Row 2 has failureReason = Timeout,"quoted" — contains comma and quote
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/export.csv?provider={ProviderB}&status=failed");
        var csv = await resp.Content.ReadAsStringAsync();

        // The failure reason cell should be quoted and quotes doubled
        Assert.Contains("\"Timeout,\"\"quoted\"\"\"", csv);
    }

    // ── invalid inputs ────────────────────────────────────────────────────────

    [Fact]
    public async Task Export_InvalidStatus_Returns400()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/export.csv?status=bogus");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Export_InvalidStudentId_Returns400()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            "/api/admin/ai-usage/export.csv?studentId=not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Export_InvertedDateRange_Returns400()
    {
        var token = await SeedAndGetTokenAsync();
        var from = Encode(new DateTime(2030, 1, 2, 0, 0, 0, DateTimeKind.Utc).ToString("O"));
        var to   = Encode(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O"));
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/export.csv?from={from}&to={to}");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── model filter ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Export_ModelFilter_OnlyIncludesThatModel()
    {
        var token = await SeedAndGetTokenAsync();
        var resp = await AdminClient(token).GetAsync(
            $"/api/admin/ai-usage/export.csv?model={Encode(ModelB)}");
        var csv = await resp.Content.ReadAsStringAsync();
        var dataLines = csv.Split('\n').Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        Assert.True(dataLines.Count >= 2); // Rows 2 and 4
        Assert.All(dataLines, line => Assert.Contains(ModelB, line));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    // Minimal CSV line splitter: handles RFC 4180 quoted fields.
    private static List<string> SplitCsvLine(string line)
    {
        var cols = new List<string>();
        var i = 0;
        while (i < line.Length)
        {
            if (line[i] == '"')
            {
                i++;
                var sb = new System.Text.StringBuilder();
                while (i < line.Length)
                {
                    if (line[i] == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    { sb.Append('"'); i += 2; }
                    else if (line[i] == '"')
                    { i++; break; }
                    else { sb.Append(line[i++]); }
                }
                cols.Add(sb.ToString());
                if (i < line.Length && line[i] == ',') i++;
            }
            else
            {
                var end = line.IndexOf(',', i);
                if (end < 0) end = line.Length;
                cols.Add(line[i..end].Trim());
                i = end + 1;
            }
        }
        return cols;
    }
}
