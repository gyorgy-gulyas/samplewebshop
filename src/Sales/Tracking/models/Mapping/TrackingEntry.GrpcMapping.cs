
// <auto-generated>
//     This code was generated by d3i.interpreter
//
//     Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>


namespace Sales.Tracking.OrderTrackingEntry
{
	public partial class TrackingEntry
	{
		public static Protos.TrackingEntry ToGrpc( TrackingEntry @this )
		{
			Protos.TrackingEntry result = new();

			result.TrackingStatus = @this.TrackingStatus.ToGrpc();
			result.StatusDate = @this.statusDate;
			result.OrderId = @this.orderId;

			return result;
		}
		public static TrackingEntry FromGrpc( Protos.TrackingEntry @from )
		{
			TrackingEntry result = new();

			result.TrackingStatus = @from.TrackingStatus.FromGrpc();
			result.statusDate = @from.StatusDate;
			result.orderId = @from.OrderId;

			return result;
		}
	}
}
