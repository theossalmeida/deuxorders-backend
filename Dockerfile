# Estágio de Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia os arquivos de projeto primeiro para otimizar cache de camadas
COPY ["DeuxOrders.API/DeuxOrders.API.csproj", "DeuxOrders.API/"]
COPY ["DeuxOrders.Application/DeuxOrders.Application.csproj", "DeuxOrders.Application/"]
COPY ["DeuxOrders.Domain/DeuxOrders.Domain.csproj", "DeuxOrders.Domain/"]
COPY ["DeuxOrders.Infrastructure/DeuxOrders.Infrastructure.csproj", "DeuxOrders.Infrastructure/"]

RUN dotnet restore "DeuxOrders.API/DeuxOrders.API.csproj"

# Copia o restante e publica
COPY . .
RUN dotnet publish "DeuxOrders.API/DeuxOrders.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio Final (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DeuxOrders.API.dll"]