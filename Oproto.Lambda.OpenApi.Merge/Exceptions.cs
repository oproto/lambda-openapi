namespace Oproto.Lambda.OpenApi.Merge;

using System;

/// <summary>
/// Exception thrown when there is an error in the merge configuration.
/// </summary>
public class ConfigurationException : Exception
{
    /// <summary>
    /// Creates a new configuration exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ConfigurationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new configuration exception with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when an OpenAPI specification fails validation.
/// </summary>
public class OpenApiValidationException : Exception
{
    /// <summary>
    /// Creates a new OpenAPI validation exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    public OpenApiValidationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new OpenAPI validation exception with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public OpenApiValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
