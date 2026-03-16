namespace DeuxOrders.Domain.Entities
{
    public class Client
    {
        public void ChangeClientStatus()
        {
            UpdatedAt = DateTime.UtcNow;
            Status = !Status;
        }
        public void Update(string name, string? mobile, bool? status = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nome não pode ser vazio.");
            Name = name;
            Mobile = string.IsNullOrWhiteSpace(mobile) ? null : mobile;
            if (status.HasValue) Status = status.Value;
            UpdatedAt = DateTime.UtcNow;
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
