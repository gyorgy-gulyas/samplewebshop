using Sales.OrderManagement.Order;
using ServiceKit.Net;

namespace Sales.OrderManagement.Context.Implementations
{
    // The published surface: it maps DTOs to the domain and back, and does nothing else. Keeping the
    // mapping here is what lets v1 and v2 answer from one service without either version leaking
    // into the model.
    public class OrderIF_v1 : IOrderIF_v1
    {
        private readonly IOrderService _service;

        public OrderIF_v1(IOrderService service)
        {
            _service = service;
        }

        async Task<Response<IOrderIF_v1.OrderDTO>> IOrderIF_v1.getOrder(CallingContext ctx, string orderId)
        {
            var found = await _service.getOrder(ctx, orderId).ConfigureAwait(false);
            if (found.IsFailed())
                return new(found);

            return new(ToDto(found.Value));
        }

        async Task<Response<IOrderIF_v1.OrderDTO>> IOrderIF_v1.placeOrder(CallingContext ctx, IOrderIF_v1.OrderDTO order)
        {
            var placed = await _service.placeOrder(ctx, FromDto(order)).ConfigureAwait(false);
            if (placed.IsFailed())
                return new(placed);

            return new(ToDto(placed.Value));
        }

        Task<Response<IOrderIF_v1.OrderItemDTO>> IOrderIF_v1.setPrice(CallingContext ctx, IOrderIF_v1.OrderItemDTO orderItem, decimal price)
        {
            orderItem.unitPrice = price;
            orderItem.subTotalPrice = price * orderItem.quantity;
            return Response<IOrderIF_v1.OrderItemDTO>.Success(orderItem).AsTask();
        }

        Task<Response> IOrderIF_v1.justOrder(CallingContext ctx, string orderId)
        {
            return Response.Success().AsTask();
        }

        private static IOrderIF_v1.OrderDTO ToDto(OrderHeader order)
        {
            return new IOrderIF_v1.OrderDTO()
            {
                id = order.id,
                orderingDate = order.orderingDate.ToString("yyyy-MM-dd"),
                orderStatus = (IOrderIF_v1.OrderStatuses)order.status,
                totalPrice = order.totalPrice,
                customerData = new IOrderIF_v1.OrderDTO.CustomerDataDTO()
                {
                    customerId = order.customer,
                    customerName = string.Empty,
                },
                items = order.items.Select(item => new IOrderIF_v1.OrderItemDTO()
                {
                    productId = item.productId,
                    productName = item.productName,
                    quantity = item.quantity,
                    unitPrice = item.unitPrice,
                    subTotalPrice = item.subTotalPrice,
                }).ToList(),
            };
        }

        private static OrderHeader FromDto(IOrderIF_v1.OrderDTO dto)
        {
            return new OrderHeader()
            {
                id = dto.id,
                // The typed id converts from a plain string implicitly, so the wire stays a string
                // while the model stays typed.
                customer = dto.customerData?.customerId,
                orderingDate = DateOnly.TryParse(dto.orderingDate, out var parsed) ? parsed : DateOnly.FromDateTime(DateTime.UtcNow),
                status = (OrderStatuses)dto.orderStatus,
                totalPrice = dto.totalPrice,
                humanKey = dto.id,
                partnerData = string.Empty,
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
