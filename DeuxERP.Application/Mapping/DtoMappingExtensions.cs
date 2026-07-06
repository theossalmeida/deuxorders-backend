using DeuxERP.Application.DTOs;
using DeuxERP.Domain.Inventory;
using DeuxERP.Domain.Sales;

namespace DeuxERP.Application.Mapping
{
    public static class DtoMappingExtensions
    {
        public static OrderResponse ToResponse(this Order order, string clientName = "", string? clientMobile = null, List<string>? signedReferenceUrls = null)
        {
            return new OrderResponse(
                order.Id,
                order.DeliveryDate,
                order.Status,
                order.ClientId,
                clientName,
                clientMobile,
                (long)order.TotalPaid,
                (long)order.TotalValue,
                signedReferenceUrls,
                order.Items.Select(i => i.ToResponse()).ToList(),
                order.PaidAt,
                order.PaidByUserName
            );
        }

        public static OrderItemResponse ToResponse(this OrderItem item)
        {
            return new OrderItemResponse(
                item.ProductId,
                item.Product?.Name ?? "Produto não encontrado",
                item.Product?.Size,
                item.Observation,
                item.Massa,
                item.Sabor,
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

        public static ClientListResponse ToListResponse(this Client client, int? totalOrders = null, long? totalSpent = null)
        {
            return new ClientListResponse(
                client.Id,
                client.Name,
                client.Mobile,
                client.Status,
                totalOrders,
                totalSpent
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

        public static InventoryMaterialResponse ToResponse(this InventoryMaterial material)
        {
            return new InventoryMaterialResponse(
                material.Id,
                material.Name,
                material.Quantity,
                material.UnitCost,
                material.MeasureUnit.ToString(),
                material.Status,
                material.CreatedAt,
                material.UpdatedAt
            );
        }
    }
}
