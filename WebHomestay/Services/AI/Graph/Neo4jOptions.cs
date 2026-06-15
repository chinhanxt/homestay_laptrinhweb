namespace WebHomestay.Services.AI.Graph
{
    public sealed class Neo4jOptions
    {
        public string Uri { get; set; } = "bolt://localhost:7687";
        public string Username { get; set; } = "neo4j";
        public string Password { get; set; } = "password";
        public string Database { get; set; } = "neo4j";
    }
}
