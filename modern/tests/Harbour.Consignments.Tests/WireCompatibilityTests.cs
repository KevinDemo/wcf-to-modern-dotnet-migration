using System.Text;
using System.Xml.Linq;

namespace Harbour.Consignments.Tests;

/// <summary>
/// Replays SOAP requests captured from the real .NET Framework service and compares the new host's raw
/// XML with what the old service answered. This is the contract: if a field name, namespace, null
/// convention, price or fault detail moves, a client we cannot see would break, and this fails first.
/// </summary>
public class WireCompatibilityTests
{
    public static TheoryData<string> Cases() => new(GoldenFiles.RequestStems());

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Response_matches_the_legacy_service(string stem)
    {
        await using var app = await RunningApp.StartAsync();

        // Bookings take sequential numbers, so replay everything captured before this case, in order.
        foreach (var earlier in GoldenFiles.RequestStems().TakeWhile(s => s != stem))
        {
            await SendAsync(app, earlier);
        }

        var (status, body, contentType) = await SendAsync(app, stem);

        var expectedStatus = int.Parse(File.ReadAllText(Path.Combine(GoldenFiles.Directory, stem + ".status.txt")));
        var expected = File.ReadAllText(Path.Combine(GoldenFiles.Directory, stem + ".response.xml"));
        Assert.Equal(expectedStatus, status);
        Assert.StartsWith("text/xml", contentType);
        Assert.Equal(Canonical(expected), Canonical(body));
    }

    [Fact]
    public void Golden_files_exist_and_cover_every_operation()
    {
        // Guards against a passing suite that compared nothing, for example if the files were not copied.
        var stems = GoldenFiles.RequestStems();
        Assert.Equal(10, stems.Length);
        Assert.Contains(stems, s => s.Contains("book"));
        Assert.Contains(stems, s => s.Contains("get"));
        Assert.Contains(stems, s => s.Contains("track"));
    }

    [Fact]
    public async Task Wsdl_describes_the_same_contract_as_the_legacy_service()
    {
        await using var app = await RunningApp.StartAsync();
        var actual = await app.Http.GetStringAsync(app.SoapAddress + "?singleWsdl");
        var expected = File.ReadAllText(Path.Combine(GoldenFiles.Directory, "service.wsdl.xml"));

        Assert.Equal(ContractFacts(expected), ContractFacts(actual));
    }

    [Theory]
    [InlineData("01-book-express-crosszone", "<Charge>28.65</Charge>")]
    [InlineData("08-get-unknown-returns-null", "i:nil=\"true\"")]
    public void Golden_files_hold_the_legacy_behaviour_they_claim(string stem, string fragment)
    {
        // A sanity check on the frozen files, which come from the net48 service and were not written by hand.
        var response = File.ReadAllText(Path.Combine(GoldenFiles.Directory, stem + ".response.xml"));
        Assert.Contains(fragment, response);
    }

    private static async Task<(int Status, string Body, string ContentType)> SendAsync(RunningApp app, string stem)
    {
        var body = File.ReadAllText(Path.Combine(GoldenFiles.Directory, stem + ".request.xml"));
        var operation = XDocument.Parse(body).Root!.Elements().Last().Elements().First().Name;
        using var request = new HttpRequestMessage(HttpMethod.Post, app.SoapAddress)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/xml"),
        };
        request.Headers.Add("SOAPAction", $"\"{operation.NamespaceName}/IConsignmentService/{operation.LocalName}\"");
        using var response = await app.Http.SendAsync(request);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(), response.Content.Headers.ContentType!.ToString());
    }

    // Prefix choice and where namespaces are declared carry no meaning in XML and may differ between
    // WCF and CoreWCF. Names, namespaces, attributes, order and values all still have to match.
    private static string Canonical(string xml)
    {
        var root = XDocument.Parse(xml).Root!;
        var text = new StringBuilder();
        Write(root, text, 0);
        return text.ToString();

        static void Write(XElement element, StringBuilder output, int depth)
        {
            output.Append(' ', depth * 2).Append(element.Name);
            foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration).OrderBy(a => a.Name.ToString()))
            {
                output.Append(" [").Append(attribute.Name).Append('=').Append(attribute.Value).Append(']');
            }
            if (!element.HasElements) output.Append(" = ").Append(element.Value);
            output.AppendLine();
            foreach (var child in element.Elements()) Write(child, output, depth + 1);
        }
    }

    // The parts of a WSDL that clients generate code from. Addresses and policy are left out, because the
    // host name is expected to change at cutover.
    private static string[] ContractFacts(string wsdl)
    {
        XNamespace w = "http://schemas.xmlsoap.org/wsdl/";
        XNamespace xs = "http://www.w3.org/2001/XMLSchema";
        XNamespace soap = "http://schemas.xmlsoap.org/wsdl/soap/";
        var root = XDocument.Parse(wsdl).Root!;
        var facts = new List<string> { $"targetNamespace {root.Attribute("targetNamespace")?.Value}" };

        foreach (var op in root.Elements(w + "portType").Elements(w + "operation"))
        {
            facts.Add($"operation {op.Attribute("name")!.Value} input={LocalName(op.Element(w + "input")!, "message")} output={LocalName(op.Element(w + "output")!, "message")} faults={string.Join(",", op.Elements(w + "fault").Select(f => f.Attribute("name")!.Value).Order())}");
        }

        foreach (var op in root.Elements(w + "binding").Elements(w + "operation"))
        {
            var soapOp = op.Element(soap + "operation")!;
            facts.Add($"binding {op.Attribute("name")!.Value} action={soapOp.Attribute("soapAction")?.Value} style={soapOp.Attribute("style")?.Value}");
        }

        // Only the service's own schema. The WCF helper schemas are the same types described the same way.
        foreach (var schema in root.Descendants(xs + "schema").Where(s => s.Attribute("targetNamespace")?.Value.Contains("harbourfreight") == true))
        {
            foreach (var type in schema.Elements().Where(e => e.Name == xs + "complexType" || e.Name == xs + "simpleType" || e.Name == xs + "element"))
            {
                var members = type.Descendants().Where(e => e.Name == xs + "element" || e.Name == xs + "enumeration")
                    .Select(e => $"{e.Attribute("name")?.Value ?? e.Attribute("value")?.Value}:{e.Attribute("type")?.Value?.Split(':').Last()}:{e.Attribute("minOccurs")?.Value}:{e.Attribute("maxOccurs")?.Value}:{e.Attribute("nillable")?.Value}");
                facts.Add($"{type.Name.LocalName} {type.Attribute("name")!.Value} {string.Join(" ", members)}");
            }
        }

        facts.Sort(StringComparer.Ordinal);
        return facts.ToArray();

        static string? LocalName(XElement element, string attribute) => element.Attribute(attribute)?.Value.Split(':').Last();
    }
}
