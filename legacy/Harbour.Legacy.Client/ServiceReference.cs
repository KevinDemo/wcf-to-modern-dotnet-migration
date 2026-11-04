// Stands in for the proxy a client generated with svcutil years ago. It is deliberately a separate copy
// of the contract with no project reference to the service, the way a real customer's client is.
using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Harbour.Legacy.Client
{
    [ServiceContract(Namespace = "http://harbourfreight.example/consignments/2014/06")]
    public interface IConsignmentService
    {
        [OperationContract]
        [FaultContract(typeof(ValidationFault))]
        BookingResult BookConsignment(BookingRequest request);

        [OperationContract]
        ConsignmentDetail GetConsignment(string consignmentNumber);

        [OperationContract]
        [FaultContract(typeof(ConsignmentNotFoundFault))]
        TrackingEvent[] TrackConsignment(string consignmentNumber);
    }

    public class ConsignmentServiceClient : ClientBase<IConsignmentService>, IConsignmentService
    {
        public ConsignmentServiceClient(string endpointName) : base(endpointName) { }
        public BookingResult BookConsignment(BookingRequest request) { return Channel.BookConsignment(request); }
        public ConsignmentDetail GetConsignment(string consignmentNumber) { return Channel.GetConsignment(consignmentNumber); }
        public TrackingEvent[] TrackConsignment(string consignmentNumber) { return Channel.TrackConsignment(consignmentNumber); }
    }

    [DataContract(Namespace = "http://harbourfreight.example/consignments/2014/06")]
    public enum ServiceLevel { [EnumMember] Standard, [EnumMember] Express, [EnumMember] Overnight }

    [DataContract(Namespace = "http://harbourfreight.example/consignments/2014/06")]
    public enum ConsignmentStatus { [EnumMember] Booked, [EnumMember] Despatched, [EnumMember] PickedUp, [EnumMember] Delivered }

    [DataContract(Namespace = "http://harbourfreight.example/consignments/2014/06")]
    public class BookingRequest
    {
        [DataMember] public string CustomerAccount { get; set; }
        [DataMember] public string PickupZone { get; set; }
        [DataMember] public string DeliveryZone { get; set; }
        [DataMember] public ServiceLevel ServiceLevel { get; set; }
        [DataMember] public decimal WeightKg { get; set; }
        [DataMember] public int Items { get; set; }
        [DataMember] public string RefrenceNo { get; set; }
    }

    [DataContract(Namespace = "http://harbourfreight.example/consignments/2014/06")]
    public class BookingResult
    {
        [DataMember] public string ConsignmentNumber { get; set; }
        [DataMember] public decimal Charge { get; set; }
        [DataMember] public string Currency { get; set; }
    }

    [DataContract(Namespace = "http://harbourfreight.example/consignments/2014/06")]
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
        [DataMember] public string RefrenceNo { get; set; }
    }

    [DataContract(Namespace = "http://harbourfreight.example/consignments/2014/06")]
    public class TrackingEvent
    {
        [DataMember] public ConsignmentStatus Status { get; set; }
        [DataMember] public DateTime OccurredUtc { get; set; }
        [DataMember] public string Location { get; set; }
    }

    [DataContract(Namespace = "http://harbourfreight.example/consignments/2014/06")]
    public class ValidationFault
    {
        [DataMember] public string Field { get; set; }
        [DataMember] public string Problem { get; set; }
    }

    [DataContract(Namespace = "http://harbourfreight.example/consignments/2014/06")]
    public class ConsignmentNotFoundFault
    {
        [DataMember] public string ConsignmentNumber { get; set; }
    }
}
