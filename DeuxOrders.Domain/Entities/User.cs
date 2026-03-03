using DeuxOrders.Domain.Enums;

namespace DeuxOrders.Domain.Entities
{
    public class User
    {        
        public Guid Id { get; private set; }
        public string Name { get; private set; } = null!;
        public string Username { get; private set; } = null!;
        public string PasswordHash { get; private set; } = null!;
        public string Email { get; private set; } = null!;
        public UserRole Role { get; private set; }
        public User(string name, string username, string passwordHash, string email, UserRole role) {
            Id = Guid.NewGuid();
            Name = name;
            Username = username;
            PasswordHash = passwordHash;
            Email = email;
            Role = role;
        }
        public void PromoteAdmin()
        {
            if (Role == UserRole.Administrator)
                throw new InvalidOperationException("Não é possível promover um usuário que já é Administrador.");
            Role = UserRole.Administrator;
        }

        public void DemoteAdmin()
        {
            if (Role != UserRole.Administrator)
                throw new InvalidOperationException("Não é possível rebaixar um usuário que não é Administrador.");
            Role = UserRole.User;
        }
        private User() {}
    }
}
