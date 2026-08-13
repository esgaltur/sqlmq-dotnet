using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SqlMq.EntityFrameworkCore;

public static class SqlMqEfCoreExtensions
{
    /// <summary>
    /// Registers the EF Core specific ISqlMqEfCoreTemplate for seamless Outbox pattern support.
    /// </summary>
    public static IServiceCollection AddSqlMqEntityFrameworkCore<TContext>(this IServiceCollection services) 
        where TContext : DbContext
    {
        services.AddTransient<ISqlMqEfCoreTemplate<TContext>, SqlMqEfCoreTemplate<TContext>>();
        return services;
    }
}
