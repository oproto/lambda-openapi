using System;

namespace Oproto.OpenApi
{
    [AttributeUsage(AttributeTargets.Class)]
    public class GenerateOpenApiSpecAttribute : Attribute
    {
        public GenerateOpenApiSpecAttribute(string serviceName, string version = "1.0")
        {
            ServiceName = serviceName;
            Version = version;
        }

        public string ServiceName { get; }
        public string Version { get; }
    }
}
