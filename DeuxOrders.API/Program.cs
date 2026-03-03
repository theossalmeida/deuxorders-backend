using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Infrastructure.Data;
using DeuxOrders.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Service config
builder.Services.AddOpenApi();

// DB config
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repository config
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();

// Controllers config
builder.Services.AddControllers();

// Application builder
var app = builder.Build();

// Middlewares config
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();