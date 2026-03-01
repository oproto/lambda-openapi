using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;

// Register the Lambda JSON serializer
[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
