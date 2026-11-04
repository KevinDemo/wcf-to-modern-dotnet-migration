namespace Harbour.Consignments.Application;

public enum ServiceLevel { Standard, Express, Overnight }

public enum ConsignmentStatus { Booked, Despatched, PickedUp, Delivered }

public sealed record BookingCommand(
    string? CustomerAccount,
    string? PickupZone,
    string? DeliveryZone,
    ServiceLevel ServiceLevel,
    decimal WeightKg,
    int Items,
    string? Reference);

public sealed record Booking(string ConsignmentNumber, decimal Charge, string Currency);

public sealed record Consignment(
    string ConsignmentNumber,
    string CustomerAccount,
    string PickupZone,
    string DeliveryZone,
    ServiceLevel ServiceLevel,
    decimal WeightKg,
    int Items,
    decimal Charge,
    ConsignmentStatus Status,
    DateTime BookedUtc,
    string? Reference);

public sealed record TrackingEntry(ConsignmentStatus Status, DateTime OccurredUtc, string Location);

public sealed class BookingValidationException(string field, string problem) : Exception(problem)
{
    public string Field { get; } = field;
}
