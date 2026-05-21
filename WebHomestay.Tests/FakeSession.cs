using Microsoft.AspNetCore.Http;
using System.Text;

namespace WebHomestay.Tests;

public class FakeSession : ISession
{
    private readonly Dictionary<string, byte[]> _data = new(StringComparer.OrdinalIgnoreCase);

    public string Id => "test-session-id";
    public bool IsAvailable => true;
    public IEnumerable<string> Keys => _data.Keys;

    public void Clear() => _data.Clear();
    public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Remove(string key) => _data.Remove(key);
    public void Set(string key, byte[] value) => _data[key] = value;
    public bool TryGetValue(string key, out byte[]? value) => _data.TryGetValue(key, out value);

    public void SetString(string key, string value) => Set(key, Encoding.UTF8.GetBytes(value));
    public string? GetString(string key) => TryGetValue(key, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;
    public void SetInt32(string key, int value) => Set(key, BitConverter.GetBytes(value));
    public int? GetInt32(string key) => TryGetValue(key, out var bytes) && bytes != null && bytes.Length >= 4 ? BitConverter.ToInt32(bytes) : null;
}
