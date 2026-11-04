using CoreWCF;
using Harbour.Consignments.Application;
using App = Harbour.Consignments.Application;

namespace Harbour.Consignments.Web.Soap;

/// <summary>Translates between the frozen SOAP contract and the application layer. No business rules here.</summary>
[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
public sealed class ConsignmentSoapService(ConsignmentBookingService service) : IConsignmentService
{
    public BookingResult BookConsignment(BookingRequest? request)
    {
        if (request is null) throw Invalid("request", "A booking request is required.");
        try
        {
            var booking = service.Book(new BookingCommand(
                request.CustomerAccount, request.PickupZone, request.DeliveryZone,
                (App.ServiceLevel)request.ServiceLevel, request.WeightKg, request.Items, request.Reference));
            return new BookingResult { ConsignmentNumber = booking.ConsignmentNumber, Charge = booking.Charge, Currency = booking.Currency };
        }
        catch (BookingValidationException ex)
        {
            throw Invalid(ex.Field, ex.Message);
        }
    }

    public ConsignmentDetail? GetConsignment(string consignmentNumber)
    {
        var c = service.Find(consignmentNumber);
        return c is null ? null : new ConsignmentDetail
        {
            ConsignmentNumber = c.ConsignmentNumber,
            CustomerAccount = c.CustomerAccount,
            PickupZone = c.PickupZone,
            DeliveryZone = c.DeliveryZone,
            ServiceLevel = (ServiceLevel)c.ServiceLevel,
            WeightKg = c.WeightKg,
            Items = c.Items,
            Charge = c.Charge,
            Status = (ConsignmentStatus)c.Status,
            BookedUtc = c.BookedUtc,
            Reference = c.Reference,
        };
    }

    public TrackingEvent[] TrackConsignment(string consignmentNumber)
    {
        var history = service.Track(consignmentNumber) ?? throw new FaultException<ConsignmentNotFoundFault>(
            new ConsignmentNotFoundFault { ConsignmentNumber = consignmentNumber },
            new FaultReason(new FaultReasonText("Consignment not found.", "en")));

        return history
            .Select(e => new TrackingEvent { Status = (ConsignmentStatus)e.Status, OccurredUtc = e.OccurredUtc, Location = e.Location })
            .ToArray();
    }

    private static FaultException<ValidationFault> Invalid(string field, string problem) =>
        new(new ValidationFault { Field = field, Problem = problem }, new FaultReason(new FaultReasonText(problem, "en")));
}
