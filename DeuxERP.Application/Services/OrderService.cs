using DeuxERP.Application.DTOs;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Sales;

namespace DeuxERP.Application.Services
{
    public class OrderService
    {
        private readonly IOrderRepository _repository;
        private readonly IClientRepository _clientRepository;
        private readonly IProductRepository _productRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly InventoryService _inventoryService;

        public OrderService(
            IOrderRepository repository,
            IClientRepository clientRepository,
            IProductRepository productRepository,
            IUnitOfWork unitOfWork,
            InventoryService inventoryService)
        {
            _repository = repository;
            _clientRepository = clientRepository;
            _productRepository = productRepository;
            _unitOfWork = unitOfWork;
            _inventoryService = inventoryService;
        }

        public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
        {
            var client = await _clientRepository.GetByIdAsync(request.ClientId);
            if (client == null || !client.Status)
                throw new InvalidOperationException("Cliente inexistente ou inativo.");

            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
            var dbProducts = await _productRepository.GetByManyIdsAsync(productIds);

            var productsDict = dbProducts.ToDictionary(p => p.Id);

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
                order.SetReferences(request.References);

            _repository.Add(order);
            await _unitOfWork.CommitAsync();

            return order;
        }

        public async Task<(Order Order, List<string> Warnings)> UpdateOrderAsync(Guid id, UpdateOrderRequest request)
        {
            var order = await _repository.GetByIdAsync(id)
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
                var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
                var dbProducts = await _productRepository.GetByManyIdsAsync(productIds);
                var productsDict = dbProducts.ToDictionary(p => p.Id);
                var existingProductIds = order.Items.Select(i => i.ProductId).ToHashSet();

                foreach (var itemRequest in request.Items)
                {
                    if (!productsDict.TryGetValue(itemRequest.ProductId, out var product))
                        throw new InvalidOperationException($"Produto {itemRequest.ProductId} não encontrado.");

                    var existingItem = order.Items.FirstOrDefault(i => i.ProductId == itemRequest.ProductId);
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

                    var updatedItem = order.Items.First(i => i.ProductId == itemRequest.ProductId);
                    var quantityDelta = updatedItem.Quantity - previousQuantity;
                    if (quantityDelta == 0)
                        continue;

                    warnings.AddRange(await _inventoryService.AdjustForItemAsync(itemRequest.ProductId, quantityDelta));
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
                order.AppendReferences(request.References);

            await _unitOfWork.CommitAsync();

            return (order, warnings.Distinct().ToList());
        }
    }
}
