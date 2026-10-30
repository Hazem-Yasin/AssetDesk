using AssetDesk.Application.Abstractions;
using AssetDesk.Application.Services;
using AssetDesk.Infrastructure.Persistence;
using AssetDesk.Infrastructure.Security;
using AssetDesk.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AssetDesk.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers persistence (MySQL or InMemory), security, file storage and the business layer.</summary>
    public static IServiceCollection AddAssetDesk(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["Database:Provider"] ?? "MySql";
        var connection = config.GetConnectionString("Default");

        services.AddDbContext<AppDbContext>(options =>
        {
            switch (provider.ToLowerInvariant())
            {
                case "mysql":
                    options.UseMySql(connection, ServerVersion.Parse("8.0.36-mysql"), o => o.EnableRetryOnFailure());
                    break;
                case "inmemory":
                    options.UseInMemoryDatabase(connection ?? "assetdesk");
                    break;
                default:
                    throw new InvalidOperationException($"Unknown Database:Provider '{provider}'. Use MySql or InMemory.");
            }
        });

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
        services.Configure<StorageOptions>(config.GetSection(StorageOptions.Section));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        var storage = config[$"{StorageOptions.Section}:Provider"] ?? "Local";
        if (storage.Equals("S3", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IFileStore, S3FileStore>();
        else
            services.AddSingleton<IFileStore, LocalFileStore>();

        services.AddScoped<AuthService>();
        services.AddScoped<AssetService>();
        services.AddScoped<AttachmentService>();
        return services;
    }

    /// <summary>Applies MySQL migrations (InMemory just creates the model) and optionally seeds demo data.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        if (config.GetValue("Database:Seed", true))
            await DataSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
                config["Seed:AdminPassword"] ?? "Admin#12345");
    }
}
