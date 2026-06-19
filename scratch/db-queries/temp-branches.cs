using Npgsql;
var cs = "Host=localhost;Port=5432;Database=web_homestay;Username=postgres;Password=1510";
await using var conn = new NpgsqlConnection(cs);
await conn.OpenAsync();
await using var cmd = new NpgsqlCommand("select id, name, address, is_deleted from branches order by id", conn);
await using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    Console.WriteLine($"{reader.GetInt32(0)}|{reader.GetString(1)}|{reader.GetString(2)}|deleted={reader.GetBoolean(3)}");
}
