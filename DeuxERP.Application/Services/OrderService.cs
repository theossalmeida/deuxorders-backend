using DeuxERP.Application.Common;
using DeuxERP.Application.DTOs;
using DeuxERP.Application.Notifications;
using DeuxERP.Domain.Notifications;
using DeuxERP.Domain.Sales;
using DeuxERP.Domain.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace DeuxERP.Application.Services
{
    public class OrderService
    {
        private readonly IAppDbContext _db;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly InventoryService _inventoryService;
        private readonly IPushNotificationService _push;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            IAppDbContext db,
            ICurrentUserAccessor currentUser,
            InventoryService inventoryService,
            IPushNotificationService push,
            ILogger<OrderService> logger)
        {
            _db = db;
            _currentUser = currentUser;
            _inventoryService = inventoryService;
            _push = push;
            _logger = logger;
        }

        public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
        {
            var client = await _db.Clients.FirstOrDefaultAsync(client => client.Id == request.ClientId);
            if (client == null || !client.Status)
                throw new InvalidOperationException("Cliente inexistente ou inativo.");

            var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
            var dbProducts = await _db.Products
                .Where(product => productIds.Contains(product.Id))
                .ToListAsync();

            var productsDict = dbProducts.ToDictionary(product => product.Id);
            var order = new Order(request.ClientId, request.DeliveryDate);

            foreach (var item in request.Items)
            {
                if (!productsDict.TryGetValue(item.ProductId, out var product))
                    throw new InvalidOperationException($"Produto {item.ProductId} não encontrado.");

                if (!product.ProductStatus)
                    throw new InvalidOperationException($"O produto '{product.Name}' está inativo e não pode ser adicionado a um novo pedido.");

                order.AddItem(item.ProductId, item.Quantity, item.UnitPrice, product.Price, item.Observation, item.Massa, item.Sabor);
            }

            if (request.References != null)
            {
                await ValidateAndConsumeReferenceUploadsAsync(request.References, order.Id);
                order.SetReferences(request.References);
            }

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            await NotifyOrderCreatedAsync(order, client.Name);

            return order;
        }

        public async Task<(Order Order, List<string> Warnings)> UpdateOrderAsync(Guid id, UpdateOrderRequest request)
        {
            var order = await LoadTrackedOrderAsync(id)
                ?? throw new InvalidOperationException("Pedido não encontrado.");

            var warnings = new List<string>();
            var statusBeforeUpdate = order.Status;
            var isPreparingOrWaiting = statusBeforeUpdate == OrderStatus.Preparing
                || statusBeforeUpdate == OrderStatus.WaitingPickupOrDelivery;

            OrderStatus? requestedStatus = null;
            if (request.Status.HasValue)
            {
                if (!Enum.IsDefined(typeof(OrderStatus), request.Status.Value))
                    throw new InvalidOperationException("Status inválido.");

                requestedStatus = (OrderStatus)request.Status.Value;
            }

            if (request.DeliveryDate.HasValue)
                order.UpdateDeliveryDate(request.DeliveryDate.Value);

            if (request.Items != null && request.Items.Count > 0)
            {
                var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
                var dbProducts = await _db.Products
                    .Where(product => productIds.Contains(product.Id))
                    .ToListAsync();
                var productsDict = dbProducts.ToDictionary(product => product.Id);
                var existingProductIds = order.Items.Select(item => item.ProductId).ToHashSet();

                foreach (var itemRequest in request.Items)
                {
                    if (!productsDict.TryGetValue(itemRequest.ProductId, out var product))
                        throw new InvalidOperationException($"Produto {itemRequest.ProductId} não encontrado.");

                    var existingItem = order.Items.FirstOrDefault(item => item.ProductId == itemRequest.ProductId);
                    var previousQuantity = existingItem?.Quantity ?? 0;

                    var isNewItem = existingProductIds.Add(itemRequest.ProductId);
                    if (isNewItem && !product.ProductStatus)
                        throw new InvalidOperationException($"O produto '{product.Name}' está inativo e não pode ser adicionado ao pedido.");

                    order.UpsertItem(
                        itemRequest.ProductId,
                        itemRequest.Quantity,
                        itemRequest.PaidUnitPrice,
                        itemRequest.Observation,
                        product.Price,
                        itemRequest.Massa,
                        itemRequest.Sabor
                    );

                    if (!isPreparingOrWaiting)
                        continue;

                    var updatedItem = order.Items.First(item => item.ProductId == itemRequest.ProductId);
                    var quantityDelta = updatedItem.Quantity - previousQuantity;
                    if (quantityDelta == 0)
                        continue;

                    warnings.AddRange(await _inventoryService.AdjustForOrderItemAsync(updatedItem, quantityDelta));
                }
            }

            if (requestedStatus.HasValue)
            {
                order.UpdateStatus(requestedStatus.Value);

                if (requestedStatus.Value == OrderStatus.Preparing
                    && statusBeforeUpdate != OrderStatus.Preparing
                    && statusBeforeUpdate != OrderStatus.WaitingPickupOrDelivery
                    && statusBeforeUpdate != OrderStatus.Completed)
                {
                    warnings.AddRange(await _inventoryService.DeductForOrderAsync(order));
                }
            }

            if (request.References != null)
            {
                await ValidateAndConsumeReferenceUploadsAsync(request.References, order.Id);
                order.AppendReferences(request.References);
            }

            await _db.SaveChangesAsync();

            return (order, warnings.Distinct().ToList());
        }

        public async Task<Order> RemoveReferenceAsync(Order order, string objectKey)
        {
            order.RemoveReference(objectKey);
            await _db.SaveChangesAsync();
            return order;
        }

        public async Task<Order> CompleteAsync(Order order)
        {
            order.MarkAsCompleted();
            await _db.SaveChangesAsync();
            return order;
        }

        public async Task<Order> CancelAsync(Order order)
        {
            var shouldRestoreInventory = order.Status == OrderStatus.Preparing
                || order.Status == OrderStatus.WaitingPickupOrDelivery;

            if (shouldRestoreInventory)
                await _inventoryService.RestoreForOrderAsync(order);

            order.MarkAsCanceled();
            await _db.SaveChangesAsync();
            return order;
        }

        public async Task<Order> CancelItemAsync(Order order, Guid productId)
        {
            var item = order.Items.FirstOrDefault(orderItem => orderItem.ProductId == productId)
                ?? throw new InvalidOperationException("Item não encontrado no pedido.");

            var shouldRestoreInventory = order.Status == OrderStatus.Preparing
                || order.Status == OrderStatus.WaitingPickupOrDelivery;

            order.CancelItem(productId);

            if (shouldRestoreInventory)
                await _inventoryService.AdjustForOrderItemAsync(item, -item.Quantity);

            await _db.SaveChangesAsync();
            return order;
        }

        public async Task<(Order Order, List<string> Warnings)> UpdateItemQuantityAsync(Order order, Guid productId, int increment)
        {
            order.UpdateItemQuantity(productId, increment);

            var warnings = new List<string>();
            if (order.Status == OrderStatus.Preparing || order.Status == OrderStatus.WaitingPickupOrDelivery)
            {
                var item = order.Items.First(orderItem => orderItem.ProductId == productId);
                warnings = await _inventoryService.AdjustForOrderItemAsync(item, increment);
            }

            await _db.SaveChangesAsync();
            return (order, warnings);
        }

        public async Task<Order> MarkAsPaidAsync(Order order)
        {
            order.MarkAsPaid(_currentUser.UserId, _currentUser.UserName, DateTime.UtcNow);
            await _db.SaveChangesAsync();
            return order;
        }

        public async Task<Order> UnmarkAsPaidAsync(Order order, string reason)
        {
            order.UnmarkAsPaid(_currentUser.UserId, _currentUser.UserName, reason);
            await _db.SaveChangesAsync();
            return order;
        }

        public Task<Order?> LoadTrackedOrderAsync(Guid id)
        {
            return _db.Orders
                .Include(order => order.Client)
                .Include(order => order.Items)
                    .ThenInclude(item => item.Product)
                .FirstOrDefaultAsync(order => order.Id == id);
        }

        private async Task ValidateAndConsumeReferenceUploadsAsync(List<string> references, Guid orderId)
        {
            if (references.Count == 0)
                return;

            if (references.Count != references.Distinct(StringComparer.Ordinal).Count())
                throw new InvalidOperationException("Referências duplicadas não são permitidas.");

            if (references.Any(reference => !OrderReferenceObjectKey.IsValid(reference)))
                throw new InvalidOperationException("Uma ou mais referências possuem chave de arquivo inválida.");

            var now = DateTime.UtcNow;
            var sessions = await _db.OrderReferenceUploads
                .Where(upload => references.Contains(upload.ObjectKey))
                .ToListAsync();

            var sessionsByKey = sessions.ToDictionary(upload => upload.ObjectKey, StringComparer.Ordinal);
            foreach (var reference in references)
            {
                if (!sessionsByKey.TryGetValue(reference, out var session))
                    throw new InvalidOperationException("Referência enviada sem sessão de upload válida.");

                if (session.UserId != _currentUser.UserId)
                    throw new InvalidOperationException("Referência enviada pertence a outro usuário.");

                session.Consume(orderId, now);
            }
        }

        private async Task NotifyOrderCreatedAsync(Order order, string clientName)
        {
            try
            {
                await _push.SendToAllAsync(
                    NotificationType.OrderCreated,
                    "Novo pedido",
                    $"{clientName} - {order.Items.Count} item(ns)",
                    $"/orders/{order.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to queue new order push notification for order {OrderId}.", order.Id);
            }
        }
    }
}
