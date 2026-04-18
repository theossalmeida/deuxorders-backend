# Estágio de Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia os arquivos de projeto primeiro para otimizar cache de camadas
COPY ["DeuxERP.API/DeuxERP.API.csproj", "DeuxERP.API/"]
COPY ["DeuxERP.Application/DeuxERP.Application.csproj", "DeuxERP.Application/"]
COPY ["DeuxERP.Domain/DeuxERP.Domain.csproj", "DeuxERP.Domain/"]
COPY ["DeuxERP.Infrastructure/DeuxERP.Infrastructure.csproj", "DeuxERP.Infrastructure/"]

RUN dotnet restore "DeuxERP.API/DeuxERP.API.csproj"

# Copia o restante e publica
COPY . .
RUN dotnet publish "DeuxERP.API/DeuxERP.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio Final (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DeuxERP.API.dll"]