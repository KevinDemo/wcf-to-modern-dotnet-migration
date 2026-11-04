using System.Text.Json.Serialization;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Harbour.Consignments.Application;
using Harbour.Consignments.Web.Rest;
using Harbour.Consignments.Web.Soap;

namespace Harbour.Consignments.Web;

public static class HarbourApp
{
    public const string SoapPath = "/ConsignmentService.svc";

    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // CoreWCF's buffered message handling reads the request stream synchronously.
        builder.WebHost.ConfigureKestrel(options => options.AllowSynchronousIO = true);

        // Shared application layer.
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IConsignmentRepository, InMemoryConsignmentRepository>();
        builder.Services.AddSingleton<ConsignmentBookingService>();

        // SOAP for existing clients.
        builder.Services.AddServiceModelServices();
        builder.Services.AddServiceModelMetadata();
        builder.Services.AddSingleton<ConsignmentSoapService>();

        // REST and OpenAPI for new clients.
        builder.Services.AddOpenApi();
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        var app = builder.Build();

        app.UseServiceModel(serviceBuilder =>
        {
            serviceBuilder.AddService<ConsignmentSoapService>();
            serviceBuilder.AddServiceEndpoint<ConsignmentSoapService, IConsignmentService>(
                new BasicHttpBinding(BasicHttpSecurityMode.None), SoapPath);
            app.Services.GetRequiredService<ServiceMetadataBehavior>().HttpGetEnabled = true;
        });

        app.MapOpenApi();
        app.MapConsignmentApi();
        return app;
    }
}
