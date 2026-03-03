# [EN] DeuxOrders - Backend MVP 🚀
**DeuxOrders** is an order management system built with a focus on performance, security, and scalability for small businesses. The project follows Clean Architecture principles and modern development practices to ensure a solid foundation from the MVP stage.

## 🛠 Tech Stack

* **Runtime:** .NET 10
* **ORM:** Entity Framework Core
* **Database:** PostgreSQL (Dockerized)
* **Security:** JWT Authentication & Password Hashing via BCrypt
* **Infra:** Docker & GitHub Actions (CI/CD)

## 🚀 Getting Started

Follow the steps below to set up your local development environment:

### 1. Clone the Repository
```bash
git clone [https://github.com/your-user/deuxorders-backend.git](https://github.com/your-user/deuxorders-backend.git)
cd deuxorders-backend
```
### 2. Spin up Infrastructure (Docker)
Ensure Docker is installed and running, then start the database container:
```
docker-compose up -d
```
3. Configure Secrets (User-Secrets) 🔒
The project uses dotnet user-secrets to keep sensitive credentials out of source control. You must manually configure the JWT key and the local connection string:
```
# Navigate to the startup project (API)
cd DeuxOrders.API

# Initialize the secret manager
dotnet user-secrets init

# Set the JWT secret key (minimum 32 characters)
dotnet user-secrets set "JwtSettings:Secret" "YOUR_SUPER_SECRET_32_CHAR_KEY_HERE"

# Set the local database connection string
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=deux_orders;Username=postgres;Password=YOUR_DOCKER_PASSWORD"
```
### 4. Run Migrations
With the database running and secrets configured, create the PostgreSQL tables:
```
dotnet ef database update --project ../DeuxOrders.Infrastructure --startup-project .
```
### 5. Run the API
```
dotnet run
```
---
### 🔑 Authentication & Usage
The API uses Bearer Tokens (JWT) to protect business routes.

Registration: Use the POST /api/v1/auth/register endpoint to create a user.

Login: Use POST /api/v1/auth/login to obtain your access token.

Authorization: Include the received token in the Header of every request:
Authorization: Bearer <your_token>

### 🏗 Project Structure
DeuxOrders.Domain: Core entities, Enums, and fundamental Business Rules.

DeuxOrders.Application: Interfaces, DTOs (Data Transfer Objects), and application logic.

DeuxOrders.Infrastructure: Repository implementations, EF Core persistence, and infrastructure services.

DeuxOrders.API: Endpoints, Controllers, and Middleware configurations.

-------
-------

# [PT-BR] DeuxOrders - Backend MVP 🚀

O **DeuxOrders** é um sistema de gestão de pedidos desenvolvido com foco em performance, segurança e escalabilidade para pequenos negócios. O projeto utiliza uma arquitetura limpa e práticas modernas de desenvolvimento para garantir uma base sólida desde o MVP.

## 🛠 Tech Stack

* **Runtime:** .NET 10
* **ORM:** Entity Framework Core
* **Banco de Dados:** PostgreSQL (Dockerizado)
* **Segurança:** Autenticação JWT & Hashing de senhas com BCrypt
* **Infra:** Docker & GitHub Actions (CI/CD)

## 🚀 Como Rodar o Projeto

Siga os passos abaixo para configurar o ambiente de desenvolvimento local:

### 1. Clonar o Repositório
```bash
git clone https://github.com/seu-usuario/deuxorders-backend.git
cd deuxorders-backend
```
### 2. Subir a Infraestrutura (Docker)
Certifique-se de que o Docker está rodando e suba o container do banco de dados:
```
docker-compose up -d
```
### 3. Configurar Segredos (User-Secrets)
O projeto utiliza dotnet user-secrets para não expor credenciais sensíveis no código. Configure a chave do JWT e a string de conexão com o banco local:
* Garanta que as credenciais e conexões configuradas são as mesmas do docker-compose
```
# Navegue até a pasta da API
cd DeuxOrders.API

# Inicialize e configure os segredos
dotnet user-secrets init
dotnet user-secrets set "JwtSettings:Secret" "SUA_CHAVE_SUPER_SECRETA_DE_32_CARACTERES"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=deux_orders;Username=postgres;Password=SUA_SENHA_DO_DOCKER"
```
### 4. Executar Migrations
Com o banco rodando e a connection string configurada, crie as tabelas:
```
dotnet ef database update --project ../DeuxOrders.Infrastructure --startup-project .
```
### 5. Rodar a API
```
dotnet run
```
-----------------------------

### 🔒 Segurança e Autenticação
A API utiliza Bearer Tokens (JWT) para proteger as rotas.

Utilize o endpoint POST /api/v1/auth/register para criar seu usuário.

Utilize o POST /api/v1/auth/login para obter o token.

Insira o token no Header Authorization das demais requisições.

### 🏗 Estrutura do Projeto
DeuxOrders.Domain: Entidades, Enums e Regras de Negócio.

DeuxOrders.Application: Interfaces, DTOs e lógica de aplicação.

DeuxOrders.Infrastructure: Implementação de repositórios, contexto do banco (EF) e serviços externos.

DeuxOrders.API: Endpoints, Controllers e configurações de Middleware.
