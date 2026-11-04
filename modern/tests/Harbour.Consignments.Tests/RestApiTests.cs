using System.Net;
using System.Net.Http.Json;
using System.ServiceModel;
using System.Text.Json;

namespace Harbour.Consignments.Tests;

public class RestApiTests
{
    private static readonly object ValidBooking = new
    {
        customerAccount = "AC5001", pickupZone = "Z03", deliveryZone = "Z08",
        serviceLevel = "Express", weightKg = 3.2m, items = 2, reference = "PO-9",
    };

    [Fact]
    public async Task Post_books_and_returns_201_with_location()
    {
        await using var app = await RunningApp.StartAsync();

        var response = await app.Http.PostAsJsonAsync("/api/consignments", ValidBooking);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/consignments/HF-100004", response.Headers.Location!.ToString());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(28.65m, body.GetProperty("charge").GetDecimal());
    }

    [Fact]
    public async Task Rest_uses_the_corrected_field_name_not_the_soap_typo()
    {
        // New clients should not inherit the 2014 typo, and old clients must keep it. Both hold at once.
        await using var app = await RunningApp.StartAsync();

        var body = await app.Http.GetFromJsonAsync<JsonElement>("/api/consignments/HF-100001");

        Assert.Equal("PO-7781", body.GetProperty("reference").GetString());
        Assert.False(body.TryGetProperty("refrenceNo", out _));
    }

    [Fact]
    public async Task Invalid_booking_returns_400_problem_details_naming_the_field()
    {
        await using var app = await RunningApp.StartAsync();

        var response = await app.Http.PostAsJsonAsync("/api/consignments", new { customerAccount = "AC5001", pickupZone = "Z03", deliveryZone = "Z08", weightKg = 900, items = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType!.ToString());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Weight must be above 0 and at most 500 kg.", body.GetProperty("errors").GetProperty("WeightKg")[0].GetString());
    }

    [Theory]
    [InlineData("/api/consignments/HF-999999")]
    [InlineData("/api/consignments/HF-999999/tracking")]
    public async Task Unknown_consignment_is_a_plain_404(string path)
    {
        // Unlike SOAP's null and fault, because new clients have no history to preserve.
        await using var app = await RunningApp.StartAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await app.Http.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Tracking_returns_the_event_history_in_order()
    {
        await using var app = await RunningApp.StartAsync();

        var events = await app.Http.GetFromJsonAsync<JsonElement>("/api/consignments/HF-100001/tracking");

        Assert.Equal(["Booked", "Despatched", "PickedUp", "Delivered"], events.EnumerateArray().Select(e => e.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task OpenApi_document_describes_all_three_operations()
    {
        await using var app = await RunningApp.StartAsync();

        var paths = (await app.Http.GetFromJsonAsync<JsonElement>("/openapi/v1.json")).GetProperty("paths");

        Assert.True(paths.GetProperty("/api/consignments").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/consignments/{number}").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/consignments/{number}/tracking").TryGetProperty("get", out _));
    }

    [Fact]
    public async Task Soap_and_rest_share_one_application_layer()
    {
        // The strangler approach depends on this: clients move across one at a time and all see the same data.
        await using var app = await RunningApp.StartAsync();
        var soap = new ChannelFactory<ILegacyConsignmentContract>(new BasicHttpBinding(BasicHttpSecurityMode.None), new EndpointAddress(app.SoapAddress)).CreateChannel();

        var viaSoap = await soap.BookConsignmentAsync(new LegacyBookingRequest
        {
            CustomerAccount = "AC5002", PickupZone = "Z01", DeliveryZone = "Z01", WeightKg = 1m, Items = 1, RefrenceNo = "BOOKED-BY-SOAP",
        });
        var readByRest = await app.Http.GetFromJsonAsync<JsonElement>($"/api/consignments/{viaSoap.ConsignmentNumber}");
        Assert.Equal("BOOKED-BY-SOAP", readByRest.GetProperty("reference").GetString());
        Assert.Equal(viaSoap.Charge, readByRest.GetProperty("charge").GetDecimal());

        var postedByRest = await app.Http.PostAsJsonAsync("/api/consignments", ValidBooking);
        var number = (await postedByRest.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("consignmentNumber").GetString()!;
        var readBySoap = await soap.GetConsignmentAsync(number);
        Assert.Equal("PO-9", readBySoap!.RefrenceNo);
    }
}
