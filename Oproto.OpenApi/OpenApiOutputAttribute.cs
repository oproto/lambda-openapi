using System;

namespace Oproto.OpenApi
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class OpenApiOutputAttribute : Attribute
    {
        public OpenApiOutputAttribute(string specification, string outputPath)
        {
            Specification = specification;
            OutputPath = outputPath;
        }

        public string Specification { get; }
        public string OutputPath { get; }
    }
}
