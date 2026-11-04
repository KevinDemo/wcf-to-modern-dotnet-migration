using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;

namespace Harbour.Legacy.Service
{
    // Typical of the era on purpose: validation, pricing and storage all live in the service class,
    // so none of it can be reused or tested without hosting WCF.
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class ConsignmentService : IConsignmentService
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, ConsignmentDetail> Consignments = new Dictionary<string, ConsignmentDetail>();
        private static readonly Dictionary<string, List<TrackingEvent>> Events = new Dictionary<string, List<TrackingEvent>>();
        private static int _nextNumber = 100001;

        static ConsignmentService()
        {
            Seed("AC1001", "Z01", "Z01", ServiceLevel.Standard, 2.0m, 1, 12.70m, ConsignmentStatus.Delivered, new DateTime(2025, 3, 3, 8, 15, 0, DateTimeKind.Utc), "PO-7781");
            Seed("AC1002", "Z02", "Z07", ServiceLevel.Express, 11.5m, 3, 47.85m, ConsignmentStatus.Despatched, new DateTime(2025, 3, 4, 9, 30, 0, DateTimeKind.Utc), null);
            Seed("AC1003", "Z05", "Z09", ServiceLevel.Overnight, 0.5m, 1, 28.60m, ConsignmentStatus.Booked, new DateTime(2025, 3, 5, 16, 45, 0, DateTimeKind.Utc), "PO-9920");
        }

        private static void Seed(string account, string pickup, string delivery, ServiceLevel level, decimal weight, int items, decimal charge, ConsignmentStatus status, DateTime booked, string reference)
        {
            var number = "HF-" + _nextNumber++;
            Consignments[number] = new ConsignmentDetail
            {
                ConsignmentNumber = number,
                CustomerAccount = account,
                PickupZone = pickup,
                DeliveryZone = delivery,
                ServiceLevel = level,
                WeightKg = weight,
                Items = items,
                Charge = charge,
                Status = status,
                BookedUtc = booked,
                Reference = reference
            };

            var history = new List<TrackingEvent>();
            for (var step = ConsignmentStatus.Booked; step <= status; step++)
            {
                history.Add(new TrackingEvent
                {
                    Status = step,
                    OccurredUtc = booked.AddMinutes(45 * (int)step),
                    Location = step == ConsignmentStatus.Delivered ? delivery : pickup
                });
            }
            Events[number] = history;
        }

        public BookingResult BookConsignment(BookingRequest request)
        {
            if (request == null) throw Invalid("request", "A booking request is required.");
            if (string.IsNullOrWhiteSpace(request.CustomerAccount)) throw Invalid("CustomerAccount", "Customer account is required.");
            if (string.IsNullOrWhiteSpace(request.PickupZone)) throw Invalid("PickupZone", "Pickup zone is required.");
            if (string.IsNullOrWhiteSpace(request.DeliveryZone)) throw Invalid("DeliveryZone", "Delivery zone is required.");
            if (request.WeightKg <= 0 || request.WeightKg > 500) throw Invalid("WeightKg", "Weight must be above 0 and at most 500 kg.");
            if (request.Items < 1) throw Invalid("Items", "At least one item is required.");

            // Weight is charged per started half kilo. Customers' invoices are built on this rule.
            var halfKilos = Math.Ceiling(request.WeightKg * 2);
            var charge = 9.50m + halfKilos * 0.80m;
            if (request.PickupZone != request.DeliveryZone) charge += 4.00m;
            if (request.ServiceLevel == ServiceLevel.Express) charge *= 1.5m;
            if (request.ServiceLevel == ServiceLevel.Overnight) charge *= 2m;
            charge = Math.Round(charge, 2, MidpointRounding.AwayFromZero);

            lock (Sync)
            {
                var number = "HF-" + _nextNumber++;
                var now = DateTime.UtcNow;
                Consignments[number] = new ConsignmentDetail
                {
                    ConsignmentNumber = number,
                    CustomerAccount = request.CustomerAccount,
                    PickupZone = request.PickupZone,
                    DeliveryZone = request.DeliveryZone,
                    ServiceLevel = request.ServiceLevel,
                    WeightKg = request.WeightKg,
                    Items = request.Items,
                    Charge = charge,
                    Status = ConsignmentStatus.Booked,
                    BookedUtc = now,
                    Reference = request.Reference
                };
                Events[number] = new List<TrackingEvent>
                {
                    new TrackingEvent { Status = ConsignmentStatus.Booked, OccurredUtc = now, Location = request.PickupZone }
                };
                return new BookingResult { ConsignmentNumber = number, Charge = charge, Currency = "AUD" };
            }
        }

        public ConsignmentDetail GetConsignment(string consignmentNumber)
        {
            lock (Sync)
            {
                ConsignmentDetail detail;
                return consignmentNumber != null && Consignments.TryGetValue(consignmentNumber, out detail) ? detail : null;
            }
        }

        public TrackingEvent[] TrackConsignment(string consignmentNumber)
        {
            lock (Sync)
            {
                List<TrackingEvent> history;
                if (consignmentNumber == null || !Events.TryGetValue(consignmentNumber, out history))
                {
                    throw new FaultException<ConsignmentNotFoundFault>(
                        new ConsignmentNotFoundFault { ConsignmentNumber = consignmentNumber },
                        new FaultReason(new FaultReasonText("Consignment not found.", "en")));
                }
                return history.ToArray();
            }
        }

        private static FaultException<ValidationFault> Invalid(string field, string problem)
        {
            return new FaultException<ValidationFault>(
                new ValidationFault { Field = field, Problem = problem },
                new FaultReason(new FaultReasonText(problem, "en")));
        }
    }
}
