
// <auto-generated>
//     This code was generated by d3i.interpreter
//
//     Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>

using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Sales.OrderManagement;
using Sales.OrderManagement.Protos.OrderIF_v1;
using ServiceKit.Net;
using System.Globalization;

namespace Sales.OrderManagement
{
	/// public interface for Orders
	/// only for service-service communication
	public class OrderIF_v1_GrpcClient : IOrderIF_v1 
	{
		private readonly GrpcChannel _channel;
		private readonly OrderIF_v1.OrderIF_v1Client _client;

		OrderIF_v1_GrpcClient( string serverAddress )
		{
			_channel = GrpcChannel.ForAddress(serverAddress);
			_client = new OrderIF_v1.OrderIF_v1Client(_channel);
		}

		/// <inheritdoc />
		async Task<Response<IOrderIF_v1.OrderItemDTO>> IOrderIF_v1.setPrice(CallingContext ctx, IOrderIF_v1.OrderItemDTO orderItem, decimal price)
		{
			try
			{
				// fill grpc request
				var request = new OrderIF_v1_setPriceRequest();
				request.OrderItem = orderItem != null ? IOrderIF_v1.OrderItemDTO.ToGrpc( orderItem ) : null;
				request.Price = price.ToString(CultureInfo.InvariantCulture);

				// calling grpc client
				var grpc_response = await _client.setPriceAsync( request, new CallOptions(ctx.ToGrpcMetadata( "SalesOrderManagementOrderIF_v1", "setPrice" ))).ResponseAsync;

				// fill response
				switch( grpc_response.ResultCase )
				{
					case OrderIF_v1_setPriceResponse.ResultOneofCase.Value:
						IOrderIF_v1.OrderItemDTO value;
						value = grpc_response.Value != null ? IOrderIF_v1.OrderItemDTO.FromGrpc( grpc_response.Value ) : null;
						return Response<IOrderIF_v1.OrderItemDTO>.Success( value );

					case OrderIF_v1_setPriceResponse.ResultOneofCase.Error:
						return Response<IOrderIF_v1.OrderItemDTO>.Failure( new ServiceKit.Net.Error() {
							Status = grpc_response.Error.Status.FromGrpc(),
							MessageText = grpc_response.Error.MessageText,
							AdditionalInformation = grpc_response.Error.AdditionalInformation,
						} );

					case OrderIF_v1_setPriceResponse.ResultOneofCase.None:
					default:
						return Response<IOrderIF_v1.OrderItemDTO>.Failure( new ServiceKit.Net.Error() {
							Status = grpc_response.Error.Status.FromGrpc(),
							MessageText = "Not handled reponse in GRPC client when calling 'OrderIF_v1_setPrice'",
						} );
				}
			}
			catch (RpcException ex)
			{
				return Response<IOrderIF_v1.OrderItemDTO>.Failure( new ServiceKit.Net.Error() {
					Status = ex.StatusCode.FromGrpc(),
					MessageText = ex.Message,
					AdditionalInformation = ex.ToString(),
				} );
			}
			catch (Exception ex)
			{
				return Response<IOrderIF_v1.OrderItemDTO>.Failure( new ServiceKit.Net.Error() {
					Status = Statuses.InternalError,
					MessageText = ex.Message,
					AdditionalInformation = ex.ToString(),
				} );
			}
		}

		/// <inheritdoc />
		async Task<Response<IOrderIF_v1.OrderDTO>> IOrderIF_v1.multiPart(CallingContext ctx, IOrderIF_v1.OrderDTO order, IOrderIF_v1.OrderItemDTO orderitem)
		{
			try
			{
				// fill grpc request
				var request = new OrderIF_v1_multiPartRequest();
				request.Order = order != null ? IOrderIF_v1.OrderDTO.ToGrpc( order ) : null;
				request.Orderitem = orderitem != null ? IOrderIF_v1.OrderItemDTO.ToGrpc( orderitem ) : null;

				// calling grpc client
				var grpc_response = await _client.multiPartAsync( request, new CallOptions(ctx.ToGrpcMetadata( "SalesOrderManagementOrderIF_v1", "multiPart" ))).ResponseAsync;

				// fill response
				switch( grpc_response.ResultCase )
				{
					case OrderIF_v1_multiPartResponse.ResultOneofCase.Value:
						IOrderIF_v1.OrderDTO value;
						value = grpc_response.Value != null ? IOrderIF_v1.OrderDTO.FromGrpc( grpc_response.Value ) : null;
						return Response<IOrderIF_v1.OrderDTO>.Success( value );

					case OrderIF_v1_multiPartResponse.ResultOneofCase.Error:
						return Response<IOrderIF_v1.OrderDTO>.Failure( new ServiceKit.Net.Error() {
							Status = grpc_response.Error.Status.FromGrpc(),
							MessageText = grpc_response.Error.MessageText,
							AdditionalInformation = grpc_response.Error.AdditionalInformation,
						} );

					case OrderIF_v1_multiPartResponse.ResultOneofCase.None:
					default:
						return Response<IOrderIF_v1.OrderDTO>.Failure( new ServiceKit.Net.Error() {
							Status = grpc_response.Error.Status.FromGrpc(),
							MessageText = "Not handled reponse in GRPC client when calling 'OrderIF_v1_multiPart'",
						} );
				}
			}
			catch (RpcException ex)
			{
				return Response<IOrderIF_v1.OrderDTO>.Failure( new ServiceKit.Net.Error() {
					Status = ex.StatusCode.FromGrpc(),
					MessageText = ex.Message,
					AdditionalInformation = ex.ToString(),
				} );
			}
			catch (Exception ex)
			{
				return Response<IOrderIF_v1.OrderDTO>.Failure( new ServiceKit.Net.Error() {
					Status = Statuses.InternalError,
					MessageText = ex.Message,
					AdditionalInformation = ex.ToString(),
				} );
			}
		}

		/// <inheritdoc />
		async Task<Response<IOrderIF_v1.OrderDTO>> IOrderIF_v1.getOrder(CallingContext ctx, string orderId)
		{
			try
			{
				// fill grpc request
				var request = new OrderIF_v1_getOrderRequest();
				request.OrderId = orderId;

				// calling grpc client
				var grpc_response = await _client.getOrderAsync( request, new CallOptions(ctx.ToGrpcMetadata( "SalesOrderManagementOrderIF_v1", "getOrder" ))).ResponseAsync;

				// fill response
				switch( grpc_response.ResultCase )
				{
					case OrderIF_v1_getOrderResponse.ResultOneofCase.Value:
						IOrderIF_v1.OrderDTO value;
						value = grpc_response.Value != null ? IOrderIF_v1.OrderDTO.FromGrpc( grpc_response.Value ) : null;
						return Response<IOrderIF_v1.OrderDTO>.Success( value );

					case OrderIF_v1_getOrderResponse.ResultOneofCase.Error:
						return Response<IOrderIF_v1.OrderDTO>.Failure( new ServiceKit.Net.Error() {
							Status = grpc_response.Error.Status.FromGrpc(),
							MessageText = grpc_response.Error.MessageText,
							AdditionalInformation = grpc_response.Error.AdditionalInformation,
						} );

					case OrderIF_v1_getOrderResponse.ResultOneofCase.None:
					default:
						return Response<IOrderIF_v1.OrderDTO>.Failure( new ServiceKit.Net.Error() {
							Status = grpc_response.Error.Status.FromGrpc(),
							MessageText = "Not handled reponse in GRPC client when calling 'OrderIF_v1_getOrder'",
						} );
				}
			}
			catch (RpcException ex)
			{
				return Response<IOrderIF_v1.OrderDTO>.Failure( new ServiceKit.Net.Error() {
					Status = ex.StatusCode.FromGrpc(),
					MessageText = ex.Message,
					AdditionalInformation = ex.ToString(),
				} );
			}
			catch (Exception ex)
			{
				return Response<IOrderIF_v1.OrderDTO>.Failure( new ServiceKit.Net.Error() {
					Status = Statuses.InternalError,
					MessageText = ex.Message,
					AdditionalInformation = ex.ToString(),
				} );
			}
		}

		/// <inheritdoc />
		async Task<Response<IOrderIF_v1.OrderDTO>> IOrderIF_v1.placeOrder(CallingContext ctx, IOrderIF_v1.OrderDTO order)
		{
			try
			{
				// fill grpc request
				var request = new OrderIF_v1_placeOrderRequest();
				request.Order = order != null ? IOrderIF_v1.OrderDTO.ToGrpc( order ) : null;

				// calling grpc client
				var grpc_response = await _client.placeOrderAsync( request, new CallOptions(ctx.ToGrpcMetadata( "SalesOrderManagementOrderIF_v1", "placeOrder" ))).ResponseAsync;

				// fill response
				switch( grpc_response.ResultCase )
				{
					case OrderIF_v1_placeOrderResponse.ResultOneofCase.Value:
						IOrderIF_v1.OrderDTO value;
						value = grpc_response.Value != null ? IOrderIF_v1.OrderDTO.FromGrpc( grpc_response.Value ) : null;
						return Response<IOrderIF_v1.OrderDTO>.Success( value );

					case OrderIF_v1_placeOrderResponse.ResultOneofCase.Error:
						return Response<IOrderIF_v1.OrderDTO>.Failure( new ServiceKit.Net.Error() {
							Status = grpc_response.Error.Status.FromGrpc(),
							MessageText = grpc_response.Error.MessageText,
							AdditionalInformation = grpc_response.Error.AdditionalInformation,
						} );

					case OrderIF_v1_placeOrderResponse.ResultOneofCase.None:
					default:
						return Response<IOrderIF_v1.OrderDTO>.Failure( new ServiceKit.Net.Error() {
							Status = grpc_response.Error.Status.FromGrpc(),
							MessageText = "Not handled reponse in GRPC client when calling 'OrderIF_v1_placeOrder'",
						} );
				}
			}
			catch (RpcException ex)
			{
				return Response<IOrderIF_v1.OrderDTO>.Failure( new ServiceKit.Net.Error() {
					Status = ex.StatusCode.FromGrpc(),
					MessageText = ex.Message,
					AdditionalInformation = ex.ToString(),
				} );
			}
			catch (Exception ex)
			{
				return Response<IOrderIF_v1.OrderDTO>.Failure( new ServiceKit.Net.Error() {
					Status = Statuses.InternalError,
					MessageText = ex.Message,
					AdditionalInformation = ex.ToString(),
				} );
			}
		}

		/// <inheritdoc />
		async Task<Response> IOrderIF_v1.justOrder(CallingContext ctx, string orderId)
		{
			try
			{
				// fill grpc request
				var request = new OrderIF_v1_justOrderRequest();
				request.OrderId = orderId;

				// calling grpc client
				var grpc_response = await _client.justOrderAsync( request, new CallOptions(ctx.ToGrpcMetadata( "SalesOrderManagementOrderIF_v1", "justOrder" ))).ResponseAsync;

				// fill response
				switch( grpc_response.ResultCase )
				{
					case OrderIF_v1_justOrderResponse.ResultOneofCase.Success:
						return Response.Success();

					case OrderIF_v1_justOrderResponse.ResultOneofCase.Error:
						return Response.Failure( new ServiceKit.Net.Error() {
							Status = grpc_response.Error.Status.FromGrpc(),
							MessageText = grpc_response.Error.MessageText,
							AdditionalInformation = grpc_response.Error.AdditionalInformation,
						} );

					case OrderIF_v1_justOrderResponse.ResultOneofCase.None:
					default:
						return Response.Failure( new ServiceKit.Net.Error() {
							Status = grpc_response.Error.Status.FromGrpc(),
							MessageText = "Not handled reponse in GRPC client when calling 'OrderIF_v1_justOrder'",
						} );
				}
			}
			catch (RpcException ex)
			{
				return Response.Failure( new ServiceKit.Net.Error() {
					Status = ex.StatusCode.FromGrpc(),
					MessageText = ex.Message,
					AdditionalInformation = ex.ToString(),
				} );
			}
			catch (Exception ex)
			{
				return Response.Failure( new ServiceKit.Net.Error() {
					Status = Statuses.InternalError,
					MessageText = ex.Message,
					AdditionalInformation = ex.ToString(),
				} );
			}
		}

	}
}
