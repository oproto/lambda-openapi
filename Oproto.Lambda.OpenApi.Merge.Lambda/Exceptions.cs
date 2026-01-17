namespace Oproto.Lambda.OpenApi.Merge.Lambda;

/// <summary>
/// Exception thrown when the configuration file is not found in S3.
/// </summary>
public class ConfigNotFoundException : Exception
{
    /// <summary>
    /// The S3 bucket where the config was expected.
    /// </summary>
    public string Bucket { get; }

    /// <summary>
    /// The S3 key where the config was expected.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Creates a new ConfigNotFoundException.
    /// </summary>
    /// <param name="bucket">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    public ConfigNotFoundException(string bucket, string key)
        : base($"Configuration file not found at s3://{bucket}/{key}")
    {
        Bucket = bucket;
        Key = key;
    }
}

/// <summary>
/// Exception thrown when the configuration file contains invalid JSON or is missing required fields.
/// </summary>
public class InvalidConfigException : Exception
{
    /// <summary>
    /// The S3 bucket containing the invalid config.
    /// </summary>
    public string Bucket { get; }

    /// <summary>
    /// The S3 key of the invalid config.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Creates a new InvalidConfigException.
    /// </summary>
    /// <param name="bucket">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <param name="message">The error message describing what is invalid.</param>
    public InvalidConfigException(string bucket, string key, string message)
        : base($"Invalid configuration at s3://{bucket}/{key}: {message}")
    {
        Bucket = bucket;
        Key = key;
    }

    /// <summary>
    /// Creates a new InvalidConfigException with an inner exception.
    /// </summary>
    /// <param name="bucket">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <param name="message">The error message describing what is invalid.</param>
    /// <param name="innerException">The inner exception.</param>
    public InvalidConfigException(string bucket, string key, string message, Exception innerException)
        : base($"Invalid configuration at s3://{bucket}/{key}: {message}", innerException)
    {
        Bucket = bucket;
        Key = key;
    }
}

/// <summary>
/// Exception thrown when no valid source files are found for merging.
/// </summary>
public class NoValidSourcesException : Exception
{
    /// <summary>
    /// The S3 bucket that was searched.
    /// </summary>
    public string Bucket { get; }

    /// <summary>
    /// The prefix that was searched.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// Creates a new NoValidSourcesException.
    /// </summary>
    /// <param name="bucket">The S3 bucket name.</param>
    /// <param name="prefix">The prefix that was searched.</param>
    public NoValidSourcesException(string bucket, string prefix)
        : base($"No valid source files found in s3://{bucket}/{prefix}")
    {
        Bucket = bucket;
        Prefix = prefix;
    }
}

/// <summary>
/// Exception thrown when a merge conflict occurs that cannot be resolved.
/// </summary>
public class MergeConflictException : Exception
{
    /// <summary>
    /// The type of conflict that occurred.
    /// </summary>
    public string ConflictType { get; }

    /// <summary>
    /// Details about the conflict.
    /// </summary>
    public string Details { get; }

    /// <summary>
    /// Creates a new MergeConflictException.
    /// </summary>
    /// <param name="conflictType">The type of conflict (e.g., "Schema", "Path").</param>
    /// <param name="details">Details about the conflict.</param>
    public MergeConflictException(string conflictType, string details)
        : base($"Merge conflict ({conflictType}): {details}")
    {
        ConflictType = conflictType;
        Details = details;
    }
}

/// <summary>
/// Exception thrown when an S3 operation fails.
/// </summary>
public class S3OperationException : Exception
{
    /// <summary>
    /// The S3 bucket involved in the operation.
    /// </summary>
    public string Bucket { get; }

    /// <summary>
    /// The S3 key involved in the operation.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The operation that failed.
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Creates a new S3OperationException.
    /// </summary>
    /// <param name="operation">The operation that failed (e.g., "Read", "Write").</param>
    /// <param name="bucket">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <param name="innerException">The inner exception.</param>
    public S3OperationException(string operation, string bucket, string key, Exception innerException)
        : base($"S3 {operation} operation failed for s3://{bucket}/{key}: {innerException.Message}", innerException)
    {
        Operation = operation;
        Bucket = bucket;
        Key = key;
    }
}
