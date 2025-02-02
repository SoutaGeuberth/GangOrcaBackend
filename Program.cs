using System;
using System.Collections.Generic;
using MySqlConnector;
using System.Text.Json;

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
    var productos = new List<Dictionary<string, object>>();

    using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync();

    await using var command = new MySqlCommand(
        "SELECT a.id, a.name, a.cost, a.src_img, a.description, b.name, c.color " +
        "FROM clothes a " +
        "INNER JOIN categories b ON a.id_category = b.id " +
        "INNER JOIN colors c ON a.id_color = c.id;",
        connection
    );

    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        // Crear un diccionario en lugar de un objeto anónimo
        var producto = new Dictionary<string, object>
        {
            { "idClothesSizes", new List<int>() },
            { "name", reader.GetString(1) },
            { "cost", reader.GetDecimal(2) },
            { "srcImg", reader.GetString(3) },
            { "size", new List<string>() },  // Se inicializan listas vacías
            { "amount", new List<int>() },
            { "description", reader.GetString(4) },
            { "category", reader.GetString(5) },
            { "color", reader.GetString(6) }
        };

        // Ejecutar la segunda consulta para obtener tallas y cantidades
        using var connection2 = new MySqlConnection(connectionString); // Nueva conexión
        await connection2.OpenAsync();

        await using var command2 = new MySqlCommand(
            "SELECT a.id, a.size, a.amount - SUM(IFNULL(rc.amount,0)) FROM clothes_sizes a LEFT JOIN reserve_clothes rc ON a.id = rc.id_clothes_sizes WHERE id_cloth = @var2 GROUP BY a.id, a.size;",
            connection2
        );


        command2.Parameters.AddWithValue("@var2", reader.GetInt32(0));

        await using var reader2 = await command2.ExecuteReaderAsync();
        while (await reader2.ReadAsync())
        {
            ((List<int>)producto["idClothesSizes"]).Add(reader2.GetInt32(0));
            ((List<string>)producto["size"]).Add(reader2.GetString(1));
            ((List<int>)producto["amount"]).Add(reader2.GetInt32(2));
        }

        productos.Add(producto);
    }

    return Results.Ok(productos.ToList()); // Asegurar que la lista sea serializable

})
.WithName("GetListaPrendas");


app.MapGet("/listacategorias", async (IConfiguration config) =>
{
    var categorias = new List<Dictionary<string, object>>();

    using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync();

    await using var command = new MySqlCommand(
        "SELECT name, category_type FROM categories;",
        connection
    );

    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var category = new Dictionary<string, object>
        {
            { "category", reader.GetString(0) },
            { "category_type", reader.GetChar(1) },
        };
        categorias.Add(category);
    }


    return Results.Ok(categorias);

});

app.MapPost("/buy", async (HttpContext context, IConfiguration config) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var jsonString = await reader.ReadToEndAsync();

    return Results.Ok(new { message = "Compra procesada", data = jsonString });
});



app.MapPost("/reserve", async (HttpContext context, IConfiguration config) =>
{
    using var jsonReader = new StreamReader(context.Request.Body);
    var jsonString = await jsonReader.ReadToEndAsync();
    BuyRequest requestData = JsonSerializer.Deserialize<BuyRequest>(jsonString); //transforma el json a objeto de c#

    using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync();

    await using var command = new MySqlCommand(
        "INSERT INTO GangOrcaClothing.reserve_clothes(id_clothes_sizes, amount) VALUES(@var1,1);",
        connection
    );
    // Verificar si la conversión fue exitosa
    command.Parameters.AddWithValue("@var1", requestData.idClothesSizes);

    await using var reader = await command.ExecuteReaderAsync();
    return Results.Ok(new { message = "reserva procesada", data = jsonString });
});





app.Run();

public class BuyRequest
{
    public int idClothesSizes { get; set; }
}
