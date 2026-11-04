using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Xml.Linq;

namespace Harbour.Consignments.Tests;

/// <summary>Real clients against the real endpoint: a System.ServiceModel client, and the actual net48 exe.</summary>
public class ClientCompatibilityTests
{
    private static ChannelFactory<ILegacyConsignmentContract> Factory(RunningApp app) =>
        new(new BasicHttpBinding(BasicHttpSecurityMode.None), new EndpointAddress(app.SoapAddress));

    [Fact]
    public async Task ServiceModel_client_books_and_reads_back_with_legacy_pricing()
    {
        await using var app = await RunningApp.StartAsync();
        var client = Factory(app).CreateChannel();

        var booked = await client.BookConsignmentAsync(new LegacyBookingRequest
        {
            CustomerAccount = "AC4001", PickupZone = "Z03", DeliveryZone = "Z08",
            ServiceLevel = LegacyServiceLevel.Express, WeightKg = 3.2m, Items = 2, RefrenceNo = "PO-1",
        });
        var read = await client.GetConsignmentAsync(booked.ConsignmentNumber!);

        // 3.2 kg is 7 started half kilos: 9.50 + 5.60 + 4.00 cross-zone = 19.10, times 1.5 for express.
        Assert.Equal("HF-100004", booked.ConsignmentNumber);
        Assert.Equal(28.65m, booked.Charge);
        Assert.Equal("PO-1", read!.RefrenceNo);
        Assert.Equal(DateTimeKind.Utc, read.BookedUtc.Kind);
    }

    [Fact]
    public async Task Unknown_consignment_is_null_on_get_but_a_typed_fault_on_track()
    {
        // An inconsistent pair, kept on purpose: existing clients have code paths written for each.
        await using var app = await RunningApp.StartAsync();
        var client = Factory(app).CreateChannel();

        Assert.Null(await client.GetConsignmentAsync("HF-999999"));
        var fault = await Assert.ThrowsAsync<FaultException<LegacyNotFoundFault>>(() => client.TrackConsignmentAsync("HF-999999"));
        Assert.Equal("HF-999999", fault.Detail.ConsignmentNumber);
    }

    [Fact]
    public async Task Invalid_booking_returns_the_typed_validation_fault()
    {
        await using var app = await RunningApp.StartAsync();
        var client = Factory(app).CreateChannel();

        var fault = await Assert.ThrowsAsync<FaultException<LegacyValidationFault>>(() => client.BookConsignmentAsync(new LegacyBookingRequest
        {
            CustomerAccount = "AC4001", PickupZone = "Z03", DeliveryZone = "Z08", WeightKg = 900m, Items = 1,
        }));
        Assert.Equal("WeightKg", fault.Detail.Field);
    }

    /// <summary>
    /// The headline test. Copies the compiled .NET Framework client, changes only the endpoint address in its
    /// config, runs it against CoreWCF, and requires output identical to what it printed against the old service.
    /// </summary>
    [WindowsOnlyFact]
    public async Task Unmodified_net48_client_prints_the_same_transcript_against_the_new_host()
    {
        var builtClient = FindBuiltLegacyClient();
        await using var app = await RunningApp.StartAsync();

        var workDir = Directory.CreateTempSubdirectory("harbour-client-");
        try
        {
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(builtClient)!))
            {
                File.Copy(file, Path.Combine(workDir.FullName, Path.GetFileName(file)));
            }

            var exe = Path.Combine(workDir.FullName, Path.GetFileName(builtClient));
            var configPath = exe + ".config";
            var config = XDocument.Load(configPath);
            config.Descendants("endpoint").Single().SetAttributeValue("address", app.SoapAddress.ToString());
            config.Save(configPath);

            var startInfo = new ProcessStartInfo(exe) { RedirectStandardOutput = true, RedirectStandardError = true };
            using var process = Process.Start(startInfo)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));

            Assert.True(process.ExitCode == 0, $"Client exit code {process.ExitCode}: {await output} {await errors}");
            var expected = File.ReadAllText(Path.Combine(GoldenFiles.Directory, "legacy-client-transcript.txt"));
            Assert.Equal(expected.ReplaceLineEndings("\n"), (await output).ReplaceLineEndings("\n"));
        }
        finally
        {
            workDir.Delete(recursive: true);
        }
    }

    private static string FindBuiltLegacyClient()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "legacy"))) dir = dir.Parent;
        if (dir is null) throw new InvalidOperationException("Could not find the legacy folder");

        // A missing exe fails the test rather than skipping it, so nothing can pass without really running it.
        var bin = Path.Combine(dir.FullName, "legacy", "Harbour.Legacy.Client", "bin");
        var candidates = new[] { "Release", "Debug" }
            .Select(c => Path.Combine(bin, c, "net48", "Harbour.Legacy.Client.exe"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
        return candidates.Length > 0 ? candidates[0] : throw new FileNotFoundException("Build the solution first: the net48 client is missing from " + bin);
    }
}

public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Skip = ".NET Framework client only runs on Windows";
    }
}

// What a new System.ServiceModel client would write from the published WSDL. It has its own type names, to
// show that the client depends only on what is on the wire and on no shared code.
[ServiceContract(Namespace = "http://harbourfreight.example/consignments/2014/06", Name = "IConsignmentService")]
public interface ILegacyConsignmentContract
{
    [OperationContract(Action = "http://harbourfreight.example/consignments/2014/06/IConsignmentService/BookConsignment", ReplyAction = "*")]
    [FaultContract(typeof(LegacyValidationFault))]
    Task<LegacyBookingResult> BookConsignmentAsync(LegacyBookingRequest request);

    [OperationContract(Action = "http://harbourfreight.example/consignments/2014/06/IConsignmentService/GetConsignment", ReplyAction = "*")]
    Task<LegacyConsignmentDetail?> GetConsignmentAsync(string consignmentNumber);

    [OperationContract(Action = "http://harbourfreight.example/consignments/2014/06/IConsignmentService/TrackConsignment", ReplyAction = "*")]
    [FaultContract(typeof(LegacyNotFoundFault))]
    Task<LegacyTrackingEvent[]> TrackConsignmentAsync(string consignmentNumber);
}

[DataContract(Name = "ServiceLevel", Namespace = "http://harbourfreight.example/consignments/2014/06")]
public enum LegacyServiceLevel { [EnumMember] Standard, [EnumMember] Express, [EnumMember] Overnight }

[DataContract(Name = "ConsignmentStatus", Namespace = "http://harbourfreight.example/consignments/2014/06")]
public enum LegacyStatus { [EnumMember] Booked, [EnumMember] Despatched, [EnumMember] PickedUp, [EnumMember] Delivered }

[DataContract(Name = "BookingRequest", Namespace = "http://harbourfreight.example/consignments/2014/06")]
public class LegacyBookingRequest
{
    [DataMember] public string? CustomerAccount { get; set; }
    [DataMember] public string? PickupZone { get; set; }
    [DataMember] public string? DeliveryZone { get; set; }
    [DataMember] public LegacyServiceLevel ServiceLevel { get; set; }
    [DataMember] public decimal WeightKg { get; set; }
    [DataMember] public int Items { get; set; }
    [DataMember] public string? RefrenceNo { get; set; }
}

[DataContract(Name = "BookingResult", Namespace = "http://harbourfreight.example/consignments/2014/06")]
public class LegacyBookingResult
{
    [DataMember] public string? ConsignmentNumber { get; set; }
    [DataMember] public decimal Charge { get; set; }
    [DataMember] public string? Currency { get; set; }
}

[DataContract(Name = "ConsignmentDetail", Namespace = "http://harbourfreight.example/consignments/2014/06")]
public class LegacyConsignmentDetail
{
    [DataMember] public string? ConsignmentNumber { get; set; }
    [DataMember] public DateTime BookedUtc { get; set; }
    [DataMember] public string? RefrenceNo { get; set; }
}

[DataContract(Name = "TrackingEvent", Namespace = "http://harbourfreight.example/consignments/2014/06")]
public class LegacyTrackingEvent
{
    [DataMember] public LegacyStatus Status { get; set; }
    [DataMember] public DateTime OccurredUtc { get; set; }
    [DataMember] public string? Location { get; set; }
}

[DataContract(Name = "ValidationFault", Namespace = "http://harbourfreight.example/consignments/2014/06")]
public class LegacyValidationFault
{
    [DataMember] public string? Field { get; set; }
    [DataMember] public string? Problem { get; set; }
}

[DataContract(Name = "ConsignmentNotFoundFault", Namespace = "http://harbourfreight.example/consignments/2014/06")]
public class LegacyNotFoundFault
{
    [DataMember] public string? ConsignmentNumber { get; set; }
}
