var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
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
              .AllowCredentials();
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

// Comment out HTTPS redirection for development
// app.UseHttpsRedirection();

// CORS must be before UseAuthorization and MapControllers
app.UseCors("AllowAngular");

app.UseAuthorization();

app.MapControllers();

app.Run();
