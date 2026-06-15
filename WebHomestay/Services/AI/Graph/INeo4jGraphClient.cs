using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Neo4j.Driver;

namespace WebHomestay.Services.AI.Graph
{
    public interface INeo4jGraphClient : IAsyncDisposable
    {
        Task ExecuteWriteAsync(string cypher, object parameters, CancellationToken cancellationToken);
        Task<IReadOnlyList<T>> ExecuteReadAsync<T>(string cypher, object parameters, Func<IRecord, T> map, CancellationToken cancellationToken);
    }
}
