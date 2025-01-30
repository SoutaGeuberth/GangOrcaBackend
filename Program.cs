using System;
using System.Collections.Generic;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);



// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();


builder.Services.AddCors(options =>

{
    options.AddPolicy("AllowAllOrigins", policy =>
    {
        policy.AllowAnyOrigin() // Permite cualquier origen
              .AllowAnyMethod() // Permite cualquier método HTTP
              .AllowAnyHeader(); // Permite cualquier encabezado
    });

}
);
builder.Services.AddControllers();

//Register the database
builder.Services.AddMySqlDataSource(builder.Configuration.GetConnectionString("Default")!);

// Getting the connection string into a variable
var connectionString = builder.Configuration.GetConnectionString("Default");




var app = builder.Build();
app.UseCors("AllowAllOrigins");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();



app.MapGet("/listaprendas", async (IConfiguration config) =>
{
    var productos = new List<object>();

    using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync();

    await using var command = new MySqlCommand(
        "SELECT a.name, a.cost, a.src_img, a.size, a.amount, a.description, b.name, c.color" +
        " FROM clothes a INNER JOIN categories b ON a.id_category = b.id INNER JOIN colors c ON a.id_color = c.id", connection);
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var producto = new
        {
            name = reader.GetString(0),
            cost = reader.GetDecimal(1),
            srcImg = reader.GetString(2),
            size = reader.GetString(3),
            amount = reader.GetInt32(4),
            description = reader.GetString(5),
            category = reader.GetString(6),
            color = reader.GetString(7)
        };
        productos.Add(producto);
    }
    return Results.Ok(productos);
})
.WithName("GetListaPrendas");



app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
