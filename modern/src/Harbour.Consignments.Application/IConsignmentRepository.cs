namespace Harbour.Consignments.Application;

public interface IConsignmentRepository
{
    /// <summary>Allocates the next consignment number, stores the consignment and returns the number.</summary>
    string Add(Func<string, Consignment> create, IReadOnlyList<TrackingEntry> history);

    Consignment? Find(string consignmentNumber);

    IReadOnlyList<TrackingEntry>? History(string consignmentNumber);
}

/// <summary>In-memory store with the same seed data as the legacy demo. A real system would use a database here.</summary>
public sealed class InMemoryConsignmentRepository : IConsignmentRepository
{
    private readonly Lock _sync = new();
    private readonly Dictionary<string, Consignment> _consignments = [];
    private readonly Dictionary<string, IReadOnlyList<TrackingEntry>> _history = [];
    private int _next = 100001;

    public InMemoryConsignmentRepository()
    {
        Seed("AC1001", "Z01", "Z01", ServiceLevel.Standard, 2.0m, 1, 12.70m, ConsignmentStatus.Delivered, new DateTime(2025, 3, 3, 8, 15, 0, DateTimeKind.Utc), "PO-7781");
        Seed("AC1002", "Z02", "Z07", ServiceLevel.Express, 11.5m, 3, 47.85m, ConsignmentStatus.Despatched, new DateTime(2025, 3, 4, 9, 30, 0, DateTimeKind.Utc), null);
        Seed("AC1003", "Z05", "Z09", ServiceLevel.Overnight, 0.5m, 1, 28.60m, ConsignmentStatus.Booked, new DateTime(2025, 3, 5, 16, 45, 0, DateTimeKind.Utc), "PO-9920");
    }

    public string Add(Func<string, Consignment> create, IReadOnlyList<TrackingEntry> history)
    {
        lock (_sync)
        {
            var number = $"HF-{_next++}";
            _consignments[number] = create(number);
            _history[number] = history;
            return number;
        }
    }

    public Consignment? Find(string consignmentNumber)
    {
        lock (_sync) return _consignments.GetValueOrDefault(consignmentNumber);
    }

    public IReadOnlyList<TrackingEntry>? History(string consignmentNumber)
    {
        lock (_sync) return _history.GetValueOrDefault(consignmentNumber);
    }

    private void Seed(string account, string pickup, string delivery, ServiceLevel level, decimal weight, int items, decimal charge, ConsignmentStatus status, DateTime booked, string? reference)
    {
        var history = Enum.GetValues<ConsignmentStatus>()
            .Where(step => step <= status)
            .Select(step => new TrackingEntry(step, booked.AddMinutes(45 * (int)step), step == ConsignmentStatus.Delivered ? delivery : pickup))
            .ToList();
        Add(number => new Consignment(number, account, pickup, delivery, level, weight, items, charge, status, booked, reference), history);
    }
}
