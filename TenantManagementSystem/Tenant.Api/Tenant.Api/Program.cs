using Microsoft.EntityFrameworkCore;
using Tenant.Api.Data;
using Tenant.Api.Hubs;
using Tenant.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//builder.Services.AddControllers()
//    .AddJsonOptions(options =>
//        options.JsonSerializerOptions.ReferenceHandler =
//            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

builder.Services.AddControllers();
builder.Services.AddSignalR();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS for Angular frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR WebSocket from Angular
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    
    // Add development CORS logging
    app.Use(async (context, next) =>
    {
        Console.WriteLine($"Request: {context.Request.Method} {context.Request.Path}");
        Console.WriteLine($"Origin: {context.Request.Headers.Origin}");
        await next();
        Console.WriteLine($"Response: {context.Response.StatusCode}");
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// CORS must be before UseAuthorization and MapControllers
app.UseCors("AllowAngular");

app.UseAuthorization();

app.MapControllers();
app.MapHub<SharedDashboardHub>("/hubs/shared-dashboard");

app.Run();
