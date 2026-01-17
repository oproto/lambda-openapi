namespace Oproto.Lambda.OpenApi.Merge.Lambda.Services;

/// <summary>
/// Interface for S3 operations used by the merge Lambda function.
/// </summary>
public interface IS3Service
{
    /// <summary>
    /// Reads a JSON file from S3 and deserializes it.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="bucket">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized object, or null if the object doesn't exist.</returns>
    Task<T?> ReadJsonAsync<T>(string bucket, string key, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Reads raw text content from S3.
    /// </summary>
    /// <param name="bucket">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The text content, or null if the object doesn't exist.</returns>
    Task<string?> ReadTextAsync(string bucket, string key, CancellationToken ct = default);

    /// <summary>
    /// Writes JSON content to S3.
    /// </summary>
    /// <typeparam name="T">The type to serialize.</typeparam>
    /// <param name="bucket">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <param name="content">The content to serialize and write.</param>
    /// <param name="ct">Cancellation token.</param>
    Task WriteJsonAsync<T>(string bucket, string key, T content, CancellationToken ct = default);

    /// <summary>
    /// Writes raw text content to S3.
    /// </summary>
    /// <param name="bucket">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <param name="content">The text content to write.</param>
    /// <param name="contentType">The content type (default: application/json).</param>
    /// <param name="ct">Cancellation token.</param>
    Task WriteTextAsync(string bucket, string key, string content, string contentType = "application/json", CancellationToken ct = default);

    /// <summary>
    /// Lists objects with a given prefix.
    /// </summary>
    /// <param name="bucket">The S3 bucket name.</param>
    /// <param name="prefix">The prefix to filter objects by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of object keys matching the prefix.</returns>
    Task<IReadOnlyList<string>> ListObjectsAsync(string bucket, string prefix, CancellationToken ct = default);

    /// <summary>
    /// Checks if an object exists in S3.
    /// </summary>
    /// <param name="bucket">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the object exists, false otherwise.</returns>
    Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct = default);
}
