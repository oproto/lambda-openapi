namespace Oproto.Lambda.OpenApi.Merge.Lambda;

using Amazon.CloudWatch;
using Amazon.Lambda.Annotations;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oproto.Lambda.OpenApi.Merge.Lambda.Functions;
using Oproto.Lambda.OpenApi.Merge.Lambda.Services;

/// <summary>
/// Startup class for configuring dependency injection in the Lambda function.
/// </summary>
[LambdaStartup]
public class Startup
{
    /// <summary>
    /// Configures services for dependency injection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public void ConfigureServices(IServiceCollection services)
    {
        // Register AWS services
        services.AddSingleton<IAmazonS3, AmazonS3Client>();
        services.AddSingleton<IAmazonCloudWatch, AmazonCloudWatchClient>();

        // Register application services
        services.AddSingleton<IS3Service, S3Service>();
        services.AddSingleton<IConfigLoader, ConfigLoader>();
        services.AddSingleton<ISourceDiscovery, SourceDiscovery>();
        services.AddSingleton<IOutputComparer, OutputComparer>();
        services.AddSingleton<IConditionalWriter, ConditionalWriter>();
        services.AddSingleton<IMetricsService, MetricsService>();

        // Register logging
        services.AddLogging(builder =>
        {
            builder.AddLambdaLogger();
            builder.SetMinimumLevel(LogLevel.Information);
        });
    }
}
