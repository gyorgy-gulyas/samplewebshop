using Sales.OrderManagement.Order;
using ServiceKit.Net;

namespace Sales.OrderManagement.Context.Implementations
{
    // v2 answers from the SAME service as v1. That is the point of versioning the published surface
    // rather than the model: a new field on the wire costs a mapping, not a second implementation.
    public class OrderIF_v2 : IOrderIF_v2
    {
        private readonly IOrderService _service;

        public OrderIF_v2(IOrderService service)
        {
            _service = service;
        }

        async Task<Response<IOrderIF_v2.OrderDTO>> IOrderIF_v2.getOrder(CallingContext ctx, string orderId)
        {
            var found = await _service.getOrder(ctx, orderId).ConfigureAwait(false);
            if (found.IsFailed())
                return new(found);

            return new(ToDto(found.Value));
        }

        async Task<Response<IOrderIF_v2.OrderDTO>> IOrderIF_v2.placeOrder(CallingContext ctx, IOrderIF_v2.OrderDTO order)
        {
            var placed = await _service.placeOrder(ctx, FromDto(order)).ConfigureAwait(false);
            if (placed.IsFailed())
                return new(placed);

            return new(ToDto(placed.Value));
        }

        Task<Response> IOrderIF_v2.justOrder(CallingContext ctx, string orderId)
        {
            return Response.Success().AsTask();
        }

        private static IOrderIF_v2.OrderDTO ToDto(OrderHeader order)
        {
            return new IOrderIF_v2.OrderDTO()
            {
                id = order.id,
                orderingDate = order.orderingDate.ToString("yyyy-MM-dd"),
                orderStatus = (IOrderIF_v2.OrderStatuses)order.status,
                totalPrice = order.totalPrice,
                customerData = new IOrderIF_v2.OrderDTO.CustomerDataDTO()
                {
                    customerId = order.customer,
                    customerName = string.Empty,
                },
                items = order.items.Select(item => new IOrderIF_v2.OrderItemDTO()
                {
                    productId = item.productId,
                    productName = item.productName,
                    quantity = item.quantity,
                    unitPrice = item.unitPrice,
                    subTotalPrice = item.subTotalPrice,
                }).ToList(),
            };
        }

        private static OrderHeader FromDto(IOrderIF_v2.OrderDTO dto)
        {
            return new OrderHeader()
            {
                id = dto.id,
                customer = dto.customerData?.customerId,
                orderingDate = DateOnly.TryParse(dto.orderingDate, out var parsed) ? parsed : DateOnly.FromDateTime(DateTime.UtcNow),
                status = (OrderStatuses)dto.orderStatus,
                totalPrice = dto.totalPrice,
                humanKey = dto.id,
                partnerData = dto.shippingCity,
                items = dto.items.Select(item => new OrderItem()
                {
                    productId = item.productId,
                    productName = item.productName,
                    quantity = item.quantity,
                    unitPrice = item.unitPrice,
                    subTotalPrice = item.subTotalPrice,
                }).ToList(),
            };
        }
    }
}
