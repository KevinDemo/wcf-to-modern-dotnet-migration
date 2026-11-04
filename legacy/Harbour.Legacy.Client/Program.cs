using System;
using System.Globalization;
using System.ServiceModel;

namespace Harbour.Legacy.Client
{
    // Exercises every operation and prints the answers. The output is deterministic on a freshly started
    // service, so the transcript from the old service can be compared with the one from the new service.
    internal static class Program
    {
        private static int Main()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var client = new ConsignmentServiceClient("ConsignmentService");
            try
            {
                var booked = client.BookConsignment(new BookingRequest
                {
                    CustomerAccount = "AC2001",
                    PickupZone = "Z03",
                    DeliveryZone = "Z08",
                    ServiceLevel = ServiceLevel.Express,
                    WeightKg = 3.2m,
                    Items = 2,
                    RefrenceNo = "PO-1234"
                });
                Console.WriteLine("Book: {0} charge={1} {2}", booked.ConsignmentNumber, booked.Charge, booked.Currency);

                PrintDetail(booked.ConsignmentNumber, client, maskTime: true);
                PrintDetail("HF-100001", client, maskTime: false);
                PrintDetail("HF-100002", client, maskTime: false);
                PrintDetail("HF-999999", client, maskTime: false);

                PrintTracking("HF-100001", client);
                PrintTracking("HF-999999", client);

                try
                {
                    client.BookConsignment(new BookingRequest { CustomerAccount = "AC2001", PickupZone = "Z03", DeliveryZone = "Z08", WeightKg = 900m, Items = 1 });
                    Console.WriteLine("Invalid booking: accepted");
                }
                catch (FaultException<ValidationFault> fault)
                {
                    Console.WriteLine("Invalid booking: fault field={0} problem={1} reason={2}", fault.Detail.Field, fault.Detail.Problem, fault.Reason);
                }

                client.Close();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Client failed: " + ex.GetType().Name + ": " + ex.Message);
                client.Abort();
                return 1;
            }
        }

        private static void PrintDetail(string number, ConsignmentServiceClient client, bool maskTime)
        {
            var d = client.GetConsignment(number);
            if (d == null)
            {
                Console.WriteLine("Get {0}: null", number);
                return;
            }

            var booked = maskTime ? "(just now)" : d.BookedUtc.ToString("o");
            Console.WriteLine("Get {0}: account={1} {2}->{3} {4} {5}kg items={6} charge={7} status={8} booked={9} kind={10} ref={11}",
                d.ConsignmentNumber, d.CustomerAccount, d.PickupZone, d.DeliveryZone, d.ServiceLevel, d.WeightKg, d.Items, d.Charge, d.Status, booked, d.BookedUtc.Kind, d.RefrenceNo ?? "(none)");
        }

        private static void PrintTracking(string number, ConsignmentServiceClient client)
        {
            try
            {
                foreach (var e in client.TrackConsignment(number))
                {
                    Console.WriteLine("Track {0}: {1} at {2} in {3}", number, e.Status, e.OccurredUtc.ToString("o"), e.Location);
                }
            }
            catch (FaultException<ConsignmentNotFoundFault> fault)
            {
                Console.WriteLine("Track {0}: fault number={1} reason={2}", number, fault.Detail.ConsignmentNumber, fault.Reason);
            }
        }
    }
}
