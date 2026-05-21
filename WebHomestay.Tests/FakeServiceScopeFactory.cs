using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebHomestay.Data;

namespace WebHomestay.Tests;

public class FakeServiceScopeFactory : IServiceScopeFactory
{
    private readonly ApplicationDbContext _context;

    public FakeServiceScopeFactory(ApplicationDbContext context)
    {
        _context = context;
    }

    public IServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_context);
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
