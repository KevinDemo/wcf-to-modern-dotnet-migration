using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Harbour.Legacy.Service
{
    internal static class Ns
    {
        public const string Value = "http://harbourfreight.example/consignments/2014/06";
    }

    [ServiceContract(Namespace = Ns.Value)]
    public interface IConsignmentService
    {
        [OperationContract]
        [FaultContract(typeof(ValidationFault))]
        BookingResult BookConsignment(BookingRequest request);

        /// <summary>Returns null for an unknown number. Clients depend on that, so it never faults.</summary>
        [OperationContract]
        ConsignmentDetail GetConsignment(string consignmentNumber);

        [OperationContract]
        [FaultContract(typeof(ConsignmentNotFoundFault))]
        TrackingEvent[] TrackConsignment(string consignmentNumber);
    }

    [DataContract(Namespace = Ns.Value)]
    public enum ServiceLevel
    {
        [EnumMember] Standard,
        [EnumMember] Express,
        [EnumMember] Overnight
    }

    [DataContract(Namespace = Ns.Value)]
    public enum ConsignmentStatus
    {
        [EnumMember] Booked,
        [EnumMember] Despatched,
        [EnumMember] PickedUp,
        [EnumMember] Delivered
    }

    [DataContract(Namespace = Ns.Value)]
    public class BookingRequest
    {
        [DataMember] public string CustomerAccount { get; set; }
        [DataMember] public string PickupZone { get; set; }
        [DataMember] public string DeliveryZone { get; set; }
        [DataMember] public ServiceLevel ServiceLevel { get; set; }
        [DataMember] public decimal WeightKg { get; set; }
        [DataMember] public int Items { get; set; }
        // The typo went live in 2014. Renaming it would break every client, so it stays on the wire.
        [DataMember(Name = "RefrenceNo")] public string Reference { get; set; }
    }

    [DataContract(Namespace = Ns.Value)]
    public class BookingResult
    {
        [DataMember] public string ConsignmentNumber { get; set; }
        [DataMember] public decimal Charge { get; set; }
        [DataMember] public string Currency { get; set; }
    }

    [DataContract(Namespace = Ns.Value)]
    public class ConsignmentDetail
    {
        [DataMember] public string ConsignmentNumber { get; set; }
        [DataMember] public string CustomerAccount { get; set; }
        [DataMember] public string PickupZone { get; set; }
        [DataMember] public string DeliveryZone { get; set; }
        [DataMember] public ServiceLevel ServiceLevel { get; set; }
        [DataMember] public decimal WeightKg { get; set; }
        [DataMember] public int Items { get; set; }
        [DataMember] public decimal Charge { get; set; }
        [DataMember] public ConsignmentStatus Status { get; set; }
        [DataMember] public DateTime BookedUtc { get; set; }
        [DataMember(Name = "RefrenceNo")] public string Reference { get; set; }
    }

    [DataContract(Namespace = Ns.Value)]
    public class TrackingEvent
    {
        [DataMember] public ConsignmentStatus Status { get; set; }
        [DataMember] public DateTime OccurredUtc { get; set; }
        [DataMember] public string Location { get; set; }
    }

    [DataContract(Namespace = Ns.Value)]
    public class ValidationFault
    {
        [DataMember] public string Field { get; set; }
        [DataMember] public string Problem { get; set; }
    }

    [DataContract(Namespace = Ns.Value)]
    public class ConsignmentNotFoundFault
    {
        [DataMember] public string ConsignmentNumber { get; set; }
    }
}
