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


// Iconfiguration config es un parámetro que se le pasa a la función para poder acceder a la configuración de la aplicación y conectarse a la base de datos
app.MapGet("/listaprendas", async (IConfiguration config) =>
{

    var result = await UpdateClothes.ObtainClothes(connectionString);
    return Results.Ok(result); // Se Obtienen los datos actualizados de la base de datos

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
    var result = await UpdateClothes.ObtainClothes(connectionString);
    return Results.Ok(result);
});



app.MapGet("/reserve", async (HttpContext context, IConfiguration config) =>
{
    var reserves = new List<Dictionary<string, object>>();

    using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync();

    await using var command = new MySqlCommand(
        "SELECT cs.id,c.name,c.src_img,SUM(IFNULL(c.cost,0)) AS total_cost, cs.size, SUM(IFNULL(rc.amount,0)) AS total_amount FROM clothes c INNER JOIN clothes_sizes cs ON  c.id = cs.id_cloth INNER JOIN reserve_clothes rc ON cs.id = rc.id_clothes_sizes GROUP BY cs.id,c.name, c.src_img, cs.`size`;",
        connection
    );

    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var reserve = new Dictionary<string, object>
        {
            { "idClothesSizes", reader.GetInt32(0) },
            { "name", reader.GetString(1) },
            { "srcImg", reader.GetString(2) },
            { "totalCost", reader.GetDecimal(3) },
            { "size", reader.GetString(4) },
            { "totalAmount", reader.GetInt32(5) }
        };
        reserves.Add(reserve);
    }


    return Results.Ok(reserves);
});

app.MapPut("/update-reserve", async (HttpContext context, IConfiguration config) =>
{
    using var jsonReader = new StreamReader(context.Request.Body);
    var jsonString = await jsonReader.ReadToEndAsync();
    List<ReserveUpdateRequest> updatedReserves = JsonSerializer.Deserialize<List<ReserveUpdateRequest>>(jsonString);

    using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync();

    foreach (var reserve in updatedReserves)
    {
        await using var command = new MySqlCommand(
            "UPDATE reserve_clothes SET amount = @amount WHERE id_clothes_sizes = @idClothesSizes;",
            connection
        );
        command.Parameters.AddWithValue("@amount", reserve.TotalAmount);
        command.Parameters.AddWithValue("@idClothesSizes", reserve.IdClothesSizes);

        await command.ExecuteNonQueryAsync();
    }

    return Results.Ok(new { message = "Reserva actualizada correctamente" });
});







app.Run();

public class ReserveUpdateRequest
{
    public int IdClothesSizes { get; set; }
    public int TotalAmount { get; set; }
}

public class BuyRequest
{
    public int idClothesSizes { get; set; }
}

public class UpdateClothes
{

    public static async Task<List<Dictionary<string, object>>> ObtainClothes(string connectionString)
    {

        List<Dictionary<string, object>> productos = new List<Dictionary<string, object>>();
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

        return productos.ToList();

    }

}
