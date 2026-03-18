using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Application.DTOs;
using DeuxOrders.Domain.Enums;

namespace DeuxOrders.Application.Services
{
    public class OrderService
    {
        private readonly IOrderRepository _repository;
        private readonly IClientRepository _clientRepository;
        private readonly IProductRepository _productRepository;
        private readonly IUnitOfWork _unitOfWork;

        public OrderService(
            IOrderRepository repository,
            IClientRepository clientRepository,
            IProductRepository productRepository,
            IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _clientRepository = clientRepository;
            _productRepository = productRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
        {
            var client = await _clientRepository.GetByIdAsync(request.ClientId);
            if (client == null || !client.Status)
                throw new ArgumentException("Cliente inexistente ou inativo.");

            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
            var dbProducts = await _productRepository.GetByManyIdsAsync(productIds);

            var productsDict = dbProducts.ToDictionary(p => p.Id);

            var order = new Order(request.ClientId, request.DeliveryDate);

            foreach (var item in request.Items)
            {
                if (!productsDict.TryGetValue(item.ProductId, out var product))
                    throw new ArgumentException($"Produto {item.ProductId} não encontrado.");

                order.AddItem(item.ProductId, item.Quantity, item.UnitPrice, product.Price, item.Observation, item.Massa, item.Sabor);
            }

            if (request.References != null)
                order.SetReferences(request.References);

            _repository.Add(order);
            await _unitOfWork.CommitAsync();

            return order;
        }

        public async Task<Order> UpdateOrderAsync(Guid id, UpdateOrderRequest request)
        {
            var order = await _repository.GetByIdAsync(id)
                ?? throw new InvalidOperationException("Pedido não encontrado.");

            if (request.DeliveryDate.HasValue)
                order.UpdateDeliveryDate(request.DeliveryDate.Value);

            if (request.Status.HasValue)
            {
                if (!Enum.IsDefined(typeof(OrderStatus), request.Status.Value))
                    throw new InvalidOperationException("Status inválido.");

                order.UpdateStatus((OrderStatus)request.Status.Value);
            }

            if (request.Items != null && request.Items.Count > 0)
            {
                var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
                var dbProducts = await _productRepository.GetByManyIdsAsync(productIds);
                var productsDict = dbProducts.ToDictionary(p => p.Id);

                foreach (var itemRequest in request.Items)
                {
                    if (!productsDict.TryGetValue(itemRequest.ProductId, out var product))
                        throw new InvalidOperationException($"Produto {itemRequest.ProductId} não encontrado.");

                    order.UpsertItem(
                        itemRequest.ProductId,
                        itemRequest.Quantity,
                        itemRequest.PaidUnitPrice,
                        itemRequest.Observation,
                        product.Price,
                        itemRequest.Massa,
                        itemRequest.Sabor
                    );
                }
            }

            if (request.References != null)
                order.AppendReferences(request.References);

            await _unitOfWork.CommitAsync();

            return order;
        }
    }
}