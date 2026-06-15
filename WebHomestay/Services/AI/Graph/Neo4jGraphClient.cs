using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Neo4j.Driver;

namespace WebHomestay.Services.AI.Graph
{
    public sealed class Neo4jGraphClient : INeo4jGraphClient
    {
        private readonly IDriver _driver;
        private readonly string _database;

        public Neo4jGraphClient(IOptions<Neo4jOptions> options)
        {
            var opt = options.Value;
            _driver = GraphDatabase.Driver(opt.Uri, AuthTokens.Basic(opt.Username, opt.Password));
            _database = opt.Database;
        }

        public async Task ExecuteWriteAsync(string cypher, object parameters, CancellationToken cancellationToken)
        {
            await using var session = _driver.AsyncSession(o => o.WithDatabase(_database));
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(cypher, parameters);
            });
        }

        public async Task<IReadOnlyList<T>> ExecuteReadAsync<T>(string cypher, object parameters, Func<IRecord, T> map, CancellationToken cancellationToken)
        {
            await using var session = _driver.AsyncSession(o => o.WithDatabase(_database));
            return await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, parameters);
                var records = await cursor.ToListAsync(cancellationToken);
                return records.Select(map).ToList();
            });
        }

        public async ValueTask DisposeAsync()
        {
            await _driver.DisposeAsync();
        }
    }
}
