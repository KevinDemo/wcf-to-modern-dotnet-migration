namespace Harbour.Consignments.Application;

/// <summary>
/// The one place for booking rules. SOAP and REST are thin adapters over this class, and neither of them
/// contains a business decision. Validation order, messages and pricing match the legacy service exactly.
/// </summary>
public sealed class ConsignmentBookingService(IConsignmentRepository repository, TimeProvider clock)
{
    public Booking Book(BookingCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.CustomerAccount)) throw new BookingValidationException("CustomerAccount", "Customer account is required.");
        if (string.IsNullOrWhiteSpace(command.PickupZone)) throw new BookingValidationException("PickupZone", "Pickup zone is required.");
        if (string.IsNullOrWhiteSpace(command.DeliveryZone)) throw new BookingValidationException("DeliveryZone", "Delivery zone is required.");
        if (command.WeightKg <= 0 || command.WeightKg > 500) throw new BookingValidationException("WeightKg", "Weight must be above 0 and at most 500 kg.");
        if (command.Items < 1) throw new BookingValidationException("Items", "At least one item is required.");

        var charge = CalculateCharge(command.PickupZone, command.DeliveryZone, command.ServiceLevel, command.WeightKg);
        var now = clock.GetUtcNow().UtcDateTime;
        var number = repository.Add(number => new Consignment(
            number, command.CustomerAccount, command.PickupZone, command.DeliveryZone, command.ServiceLevel,
            command.WeightKg, command.Items, charge, ConsignmentStatus.Booked, now, command.Reference),
            [new TrackingEntry(ConsignmentStatus.Booked, now, command.PickupZone)]);

        return new Booking(number, charge, "AUD");
    }

    public Consignment? Find(string? consignmentNumber) =>
        consignmentNumber is null ? null : repository.Find(consignmentNumber);

    /// <summary>Returns null for an unknown number. Each adapter decides what that means for its clients.</summary>
    public IReadOnlyList<TrackingEntry>? Track(string? consignmentNumber) =>
        consignmentNumber is null ? null : repository.History(consignmentNumber);

    /// <summary>Weight is charged per started half kilo. Existing invoices are built on this rule.</summary>
    public static decimal CalculateCharge(string pickupZone, string deliveryZone, ServiceLevel level, decimal weightKg)
    {
        var charge = 9.50m + Math.Ceiling(weightKg * 2) * 0.80m;
        if (pickupZone != deliveryZone) charge += 4.00m;
        if (level == ServiceLevel.Express) charge *= 1.5m;
        if (level == ServiceLevel.Overnight) charge *= 2m;
        return Math.Round(charge, 2, MidpointRounding.AwayFromZero);
    }
}
