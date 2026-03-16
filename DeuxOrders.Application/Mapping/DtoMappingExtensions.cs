using DeuxOrders.Domain.Entities;
using DeuxOrders.Application.DTOs;

namespace DeuxOrders.Application.Mapping
{
    public static class DtoMappingExtensions
    {
        public static OrderResponse ToResponse(this Order order, string clientName = "", List<string>? signedReferenceUrls = null)
        {
            return new OrderResponse(
                order.Id,
                order.DeliveryDate,
                order.Status,
                order.ClientId,
                clientName,
                (long)order.TotalPaid,
                (long)order.TotalValue,
                signedReferenceUrls,
                order.Items.Select(i => i.ToResponse()).ToList()
            );
        }

        public static OrderItemResponse ToResponse(this OrderItem item)
        {
            return new OrderItemResponse(
                item.ProductId,
                item.Product?.Name ?? "Produto não encontrado",
                item.Observation,
                item.Quantity,
                item.PaidUnitPrice,
                item.BaseUnitPrice,
                item.ItemCanceled,
                item.TotalPaid,  
                item.TotalValue  
            );
        }

        public static ClientResponse ToResponse(this Client client)
        {
            return new ClientResponse(
                client.Id,
                client.Name,
                client.Mobile
            );
        }
        public static ProductResponse ToResponse(this Product product, string? imageUrl = null)
        {
            return new ProductResponse(
                product.Id,
                product.Name,
                product.Price,
                product.ProductStatus,
                imageUrl,
                product.Category,
                product.Size
            );
        }
    }

}