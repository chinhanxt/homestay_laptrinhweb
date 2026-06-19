using Npgsql;
var cs = "Host=localhost;Port=5432;Database=web_homestay;Username=postgres;Password=1510";
await using var conn = new NpgsqlConnection(cs);
await conn.OpenAsync();
await using var cmd = new NpgsqlCommand("select id, customer_name, id_card_front_path, id_card_front_masked_path, id_card_back_path, id_card_back_masked_path from bookings where id = 5615", conn);
await using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    Console.WriteLine($"id={reader.GetInt32(0)}");
    Console.WriteLine($"customer={reader.GetString(1)}");
    Console.WriteLine($"front={reader.IsDBNull(2) ? "<null>" : reader.GetString(2)}");
    Console.WriteLine($"front_masked={reader.IsDBNull(3) ? "<null>" : reader.GetString(3)}");
    Console.WriteLine($"back={reader.IsDBNull(4) ? "<null>" : reader.GetString(4)}");
    Console.WriteLine($"back_masked={reader.IsDBNull(5) ? "<null>" : reader.GetString(5)}");
}
