using Harbour.Consignments.Application;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Harbour.Consignments.Web.Rest;

public sealed record BookConsignmentRequest(
    string? CustomerAccount,
    string? PickupZone,
    string? DeliveryZone,
    ServiceLevel ServiceLevel,
    decimal WeightKg,
    int Items,
    string? Reference);

public static class ConsignmentApi
{
    public static void MapConsignmentApi(this IEndpointRouteBuilder routes)
    {
        var api = routes.MapGroup("/api/consignments").WithTags("Consignments");

        api.MapPost("", Book).WithName("BookConsignment");
        api.MapGet("/{number}", Get).WithName("GetConsignment");
        api.MapGet("/{number}/tracking", Track).WithName("TrackConsignment");
    }

    private static Results<Created<Booking>, ValidationProblem> Book(BookConsignmentRequest request, ConsignmentBookingService service)
    {
        try
        {
            var booking = service.Book(new BookingCommand(
                request.CustomerAccount, request.PickupZone, request.DeliveryZone,
                request.ServiceLevel, request.WeightKg, request.Items, request.Reference));
            return TypedResults.Created($"/api/consignments/{booking.ConsignmentNumber}", booking);
        }
        catch (BookingValidationException ex)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
    }

    // New clients get a normal 404. Only the SOAP adapter keeps the legacy null and fault behaviour.
    private static Results<Ok<Consignment>, NotFound> Get(string number, ConsignmentBookingService service) =>
        service.Find(number) is { } consignment ? TypedResults.Ok(consignment) : TypedResults.NotFound();

    private static Results<Ok<IReadOnlyList<TrackingEntry>>, NotFound> Track(string number, ConsignmentBookingService service) =>
        service.Track(number) is { } history ? TypedResults.Ok(history) : TypedResults.NotFound();
}
