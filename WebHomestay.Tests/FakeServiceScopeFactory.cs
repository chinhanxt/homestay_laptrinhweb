using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebHomestay.Data;

namespace WebHomestay.Tests;

public class FakeServiceScopeFactory : IServiceScopeFactory
{
    private readonly ApplicationDbContext _context;
    private readonly Action<ServiceCollection>? _configureServices;

    public FakeServiceScopeFactory(ApplicationDbContext context, Action<ServiceCollection>? configureServices = null)
    {
        _context = context;
        _configureServices = configureServices;
    }

    public IServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_context);
        _configureServices?.Invoke(services);
        var provider = services.BuildServiceProvider();
        return new FakeScope(provider);
    }

    private class FakeScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; }

        public FakeScope(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public void Dispose() { }
    }
}
