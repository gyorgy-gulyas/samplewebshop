
// <auto-generated>
//     This code was generated by d3i.interpreter
//
//     Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>


namespace Sales.Tracking
{
	public enum TrackingStatuses
	{
		/// Order has been placed but not yet processed
		Pending,

		/// Order has been confirmed (e.g. payment received)
		Confirmed,

		/// Order is being prepared in the warehouse
		Processing,

		/// Package is ready and waiting for pickup by courier
		ReadyForPickup,

		/// Package has been handed over to the shipping provider
		Shipped,

		/// Package is in transit between locations
		InTransit,

		/// Package is out for delivery to the customer
		OutForDelivery,

		/// Package successfully delivered to the customer
		Delivered,

		/// Delivery failed (e.g. customer was not available)
		FailedDelivery,

		/// Package returned (by customer or shipping provider)
		Returned,

		/// Order was cancelled (e.g. payment failed or user cancelled)
		Cancelled,

	}
}
