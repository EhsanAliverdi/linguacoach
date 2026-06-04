using System.Collections.Concurrent;

namespace LinguaCoach.Infrastructure.Diagnostics;

/// <summary>
/// Thread-safe ring buffer for recent diagnostic events.
/// Not for production audit logging — development/staging debugging only.
/// Never stores passwords, API keys, JWT tokens, or sensitive user content.
/// </summary>
public sealed class DiagnosticEventBuffer
{
    private readonly ConcurrentQueue<DiagnosticEvent> _events = new();
    private readonly int _capacity;
    private int _count; // approximate counter avoids O(n) ConcurrentQueue.Count

    public bool IsEnabled { get; }

    public DiagnosticEventBuffer(int capacity = 500, bool enabled = true)
    {
        _capacity = Math.Max(1, capacity);
        IsEnabled = enabled;
    }

    public void Add(DiagnosticEvent evt)
    {
        if (!IsEnabled) return;

        _events.Enqueue(evt);
        var newCount = Interlocked.Increment(ref _count);

        // Roll oldest events when over capacity
        while (newCount > _capacity)
        {
            if (_events.TryDequeue(out _))
                newCount = Interlocked.Decrement(ref _count);
            else
                break;
        }
    }

    public IReadOnlyList<DiagnosticEvent> Query(
        string? level = null,
        string? category = null,
        string? correlationId = null,
        string? q = null,
        int limit = 100)
    {
        var items = _events.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(level))
            items = items.Where(e => e.Level.Equals(level, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(category))
            items = items.Where(e => e.Category.Contains(category, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(correlationId))
            items = items.Where(e => e.CorrelationId == correlationId);

        if (!string.IsNullOrWhiteSpace(q))
            items = items.Where(e => e.Message.Contains(q, StringComparison.OrdinalIgnoreCase)
                                  || (e.Path?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));

        return items
            .OrderByDescending(e => e.TimestampUtc)
            .Take(Math.Clamp(limit, 1, 500))
            .ToList();
    }

    public int Count => Math.Max(0, _count);
}
