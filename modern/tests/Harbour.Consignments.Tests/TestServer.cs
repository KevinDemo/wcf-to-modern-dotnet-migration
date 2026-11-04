using Harbour.Consignments.Web;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Harbour.Consignments.Tests;

/// <summary>Runs the real app on real Kestrel and a real socket, because SOAP clients need actual HTTP.</summary>
public sealed class RunningApp : IAsyncDisposable
{
    private readonly WebApplication _app;

    private RunningApp(WebApplication app, Uri baseAddress)
    {
        _app = app;
        BaseAddress = baseAddress;
        Http = new HttpClient { BaseAddress = baseAddress };
    }

    public Uri BaseAddress { get; }
    public HttpClient Http { get; }
    public Uri SoapAddress => new(BaseAddress, HarbourApp.SoapPath);

    public static async Task<RunningApp> StartAsync()
    {
        var app = HarbourApp.Build(["--urls=http://127.0.0.1:0", "--Logging:LogLevel:Default=Warning"]);
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new RunningApp(app, new Uri(address));
    }

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

public static class GoldenFiles
{
    public static string Directory => Path.Combine(AppContext.BaseDirectory, "Golden");

    public static string[] RequestStems() =>
        System.IO.Directory.GetFiles(Directory, "*.request.xml")
            .Order()
            .Select(f => Path.GetFileName(f)[..^".request.xml".Length])
            .ToArray();
}
