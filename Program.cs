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





var app = builder.Build();
app.UseCors("AllowAllOrigins");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


var listaprendas = new List<Dictionary<string, string>>()
{
    new Dictionary<string, string>()
    {
        { "tittle", "camiseta" },
        { "cost", "450" },
        { "src", "https://http2.mlstatic.com/D_NQ_NP_805158-MLM75310632986_032024-O.webp" },
        { "categoria", "camiseta" }
    }
};

app.MapGet("/listaprendas", async (IConfiguration config) =>
{
    var connectionString = config.GetConnectionString("Default");
    using var connection = new MySqlConnection(connectionString);

    await connection.OpenAsync();

    await using var command = new MySqlCommand("SELECT id,color FROM colors;", connection);
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var value = reader.GetValue(1);
        Console.WriteLine(value);
    }

    return listaprendas;
})
.WithName("GetListaPrendas");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
