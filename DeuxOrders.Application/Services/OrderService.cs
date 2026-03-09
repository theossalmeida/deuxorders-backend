using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Application.DTOs;

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

                order.AddItem(item.ProductId, item.Quantity, item.UnitPrice, product.Price, item.Observation);
            }

            _repository.Add(order);
            await _unitOfWork.CommitAsync();

            return order;
        }
    }
}