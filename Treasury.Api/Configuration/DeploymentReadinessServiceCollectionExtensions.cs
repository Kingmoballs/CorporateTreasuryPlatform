namespace Treasury.Api.Configuration;

public static class
    DeploymentReadinessServiceCollectionExtensions
{
    public static IServiceCollection AddTreasuryCors(
        this IServiceCollection services,
        DeploymentReadinessOptions options)
    {
        services.AddCors(corsOptions =>
        {
            corsOptions.AddPolicy(
                DeploymentReadinessOptions
                    .CorsPolicyName,
                policy =>
                {
                    var origins =
                        options
                            .GetNormalizedAllowedOrigins();

                    if (origins.Count > 0)
                    {
                        policy.WithOrigins(
                            origins.ToArray());
                    }

                    policy
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
        });

        return services;
    }
}
