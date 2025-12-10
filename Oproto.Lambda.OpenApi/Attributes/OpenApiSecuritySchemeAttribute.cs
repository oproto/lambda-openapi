using System;

namespace Oproto.Lambda.OpenApi.Attributes
{
    /// <summary>
    ///     Defines a security scheme for the OpenAPI specification.
    /// </summary>
    /// <remarks>
    ///     Apply this attribute at the assembly level to define security schemes that can be referenced by endpoints.
    ///     Multiple security schemes can be defined by applying this attribute multiple times.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     [assembly: OpenApiSecurityScheme("apiKey",
    ///         Type = SecuritySchemeType.ApiKey,
    ///         ApiKeyName = "x-api-key",
    ///         ApiKeyLocation = ApiKeyLocation.Header)]
    ///     
    ///     [assembly: OpenApiSecurityScheme("oauth2",
    ///         Type = SecuritySchemeType.OAuth2,
    ///         AuthorizationUrl = "https://auth.example.com/authorize",
    ///         TokenUrl = "https://auth.example.com/token",
    ///         Scopes = "read:Read access,write:Write access")]
    ///     </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class OpenApiSecuritySchemeAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="OpenApiSecuritySchemeAttribute"/> class.
        /// </summary>
        /// <param name="schemeId">The unique identifier for this security scheme.</param>
        public OpenApiSecuritySchemeAttribute(string schemeId)
        {
            SchemeId = schemeId ?? throw new ArgumentNullException(nameof(schemeId));
        }

        /// <summary>
        ///     Gets the unique identifier for this security scheme.
        /// </summary>
        public string SchemeId { get; }

        /// <summary>
        ///     Gets or sets the type of security scheme.
        /// </summary>
        public OpenApiSecuritySchemeType Type { get; set; } = OpenApiSecuritySchemeType.ApiKey;

        /// <summary>
        ///     Gets or sets the name of the API key (for ApiKey type).
        /// </summary>
        /// <remarks>
        ///     Required when Type is ApiKey. This is the name of the header, query parameter, or cookie.
        /// </remarks>
        public string ApiKeyName { get; set; }

        /// <summary>
        ///     Gets or sets the location of the API key (for ApiKey type).
        /// </summary>
        public ApiKeyLocation ApiKeyLocation { get; set; } = ApiKeyLocation.Header;

        /// <summary>
        ///     Gets or sets the authorization URL (for OAuth2 type).
        /// </summary>
        public string AuthorizationUrl { get; set; }

        /// <summary>
        ///     Gets or sets the token URL (for OAuth2 type).
        /// </summary>
        public string TokenUrl { get; set; }

        /// <summary>
        ///     Gets or sets the available scopes (for OAuth2 type).
        /// </summary>
        /// <remarks>
        ///     Format: "scope1:Description 1,scope2:Description 2"
        /// </remarks>
        public string Scopes { get; set; }

        /// <summary>
        ///     Gets or sets the HTTP authentication scheme name (for Http type).
        /// </summary>
        /// <remarks>
        ///     Common values: "bearer", "basic"
        /// </remarks>
        public string HttpScheme { get; set; }

        /// <summary>
        ///     Gets or sets the bearer format hint (for Http type with bearer scheme).
        /// </summary>
        /// <remarks>
        ///     Example: "JWT"
        /// </remarks>
        public string BearerFormat { get; set; }

        /// <summary>
        ///     Gets or sets the OpenID Connect URL (for OpenIdConnect type).
        /// </summary>
        public string OpenIdConnectUrl { get; set; }

        /// <summary>
        ///     Gets or sets a description of the security scheme.
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    ///     Specifies the type of security scheme.
    /// </summary>
    public enum OpenApiSecuritySchemeType
    {
        /// <summary>
        ///     API key authentication (header, query, or cookie).
        /// </summary>
        ApiKey,

        /// <summary>
        ///     HTTP authentication (basic, bearer, etc.).
        /// </summary>
        Http,

        /// <summary>
        ///     OAuth 2.0 authentication.
        /// </summary>
        OAuth2,

        /// <summary>
        ///     OpenID Connect authentication.
        /// </summary>
        OpenIdConnect
    }

    /// <summary>
    ///     Specifies the location of an API key.
    /// </summary>
    public enum ApiKeyLocation
    {
        /// <summary>
        ///     API key is passed in a header.
        /// </summary>
        Header,

        /// <summary>
        ///     API key is passed as a query parameter.
        /// </summary>
        Query,

        /// <summary>
        ///     API key is passed in a cookie.
        /// </summary>
        Cookie
    }
}
