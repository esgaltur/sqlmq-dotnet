using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SqlMq.Abstractions;
using SqlMq.Attributes;
using SqlMq.Core;
using SqlMq.Hosting;
using SqlMq.Models;

namespace SqlMq.DependencyInjection;

public static class SqlMqServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SqlMq messaging infrastructure into the dependency injection container.
    /// </summary>
    public static IServiceCollection AddSqlMq(
        this IServiceCollection services, 
        Action<SqlMqOptions> configureOptions,
        params Assembly[] scanAssemblies)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configureOptions == null) throw new ArgumentNullException(nameof(configureOptions));

        services.Configure(configureOptions);

        services.TryAddSingleton<ISqlMqSerializer, SqlMqSerializer>();
        services.TryAddSingleton<ISqlMqConnectionFactory, SqlMqConnectionFactory>();
        services.TryAddTransient<ISqlMqTemplate, SqlMqTemplate>();

        // Scan and register listeners
        var registry = new SqlMqListenerRegistry();
        
        var assembliesToScan = scanAssemblies.Length > 0 ? scanAssemblies : new[] { Assembly.GetCallingAssembly() };
        
        foreach (var assembly in assembliesToScan)
        {
            var consumerTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract);

            foreach (var type in consumerTypes)
            {
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                foreach (var method in methods)
                {
                    var attribute = method.GetCustomAttribute<SqlMqListenerAttribute>();
                    if (attribute != null)
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length != 1)
                            throw new InvalidOperationException($"Method {method.Name} with [SqlMqListener] must have exactly one parameter representing the payload.");

                        var payloadType = parameters[0].ParameterType;
                        
                        registry.Register(attribute.Queue, type, method, payloadType, attribute.MaxRetries);
                        
                        // Register the consumer class itself in DI so we can resolve it
                        services.TryAddTransient(type);
                    }
                }
            }
        }

        services.AddSingleton(registry);
        services.AddHostedService<SqlMqWorker>();

        return services;
    }
}
