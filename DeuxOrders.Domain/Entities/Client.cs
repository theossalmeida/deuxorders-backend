namespace DeuxOrders.Domain.Entities
{
    public class Client
    {
        public void DeactivateClient()
        {
            if (!Status)
            {
                throw new InvalidOperationException("Não é possível desativar um cliente que já está inativo.");
            }

            UpdatedAt = DateTime.UtcNow;
            Status = false;
        }
        public void SetMobile(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile)) { throw new InvalidOperationException("Não é possível adicionar um número de telefone vazio."); }
            Mobile = mobile;
        }

        public Guid Id { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public Boolean Status { get; private set; }
        public string? Mobile {  get; private set; }
        public string Name { get; private set; }

        public Client(string name)
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            Status = true;
            Name = name;
        }
    }
}
