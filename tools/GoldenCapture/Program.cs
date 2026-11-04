// Records what the real legacy service says on the wire. Usage: GoldenCapture <service url> <golden dir>
// Sends each *.request.xml in name order and saves the raw response and HTTP status next to it,
// then saves the service's WSDL. Must run against a freshly started service, as bookings get sequential numbers.
using System.Text;
using System.Xml.Linq;

var serviceUrl = args[0];
var goldenDir = args[1];
using var http = new HttpClient();

foreach (var requestFile in Directory.GetFiles(goldenDir, "*.request.xml").Order())
{
    var body = await File.ReadAllTextAsync(requestFile);
    var operation = XDocument.Parse(body).Root!.Elements().Last().Elements().First().Name;
    using var request = new HttpRequestMessage(HttpMethod.Post, serviceUrl)
    {
        Content = new StringContent(body, Encoding.UTF8, "text/xml"),
    };
    request.Headers.Add("SOAPAction", $"\"{operation.NamespaceName}/IConsignmentService/{operation.LocalName}\"");

    using var response = await http.SendAsync(request);
    var stem = requestFile[..^".request.xml".Length];
    await File.WriteAllTextAsync(stem + ".response.xml", await response.Content.ReadAsStringAsync());
    await File.WriteAllTextAsync(stem + ".status.txt", ((int)response.StatusCode).ToString());
    Console.WriteLine($"{Path.GetFileName(stem)}: {(int)response.StatusCode}");
}

var wsdl = await http.GetStringAsync(serviceUrl + "?singleWsdl");
await File.WriteAllTextAsync(Path.Combine(goldenDir, "service.wsdl.xml"), wsdl);
Console.WriteLine("Saved service.wsdl.xml");
