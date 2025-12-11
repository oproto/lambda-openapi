using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Oproto.Lambda.OpenApi.SourceGenerator;

public partial class OpenApiSpecGenerator
{
    // Add method to extract documentation from XML comments
    private DocumentationInfo GetDocumentation(IMethodSymbol methodSymbol)
    {
        var documentation = new DocumentationInfo();
        var xmlDoc = methodSymbol.GetDocumentationCommentXml();

        if (string.IsNullOrEmpty(xmlDoc))
            return documentation;

        try
        {
            var xmlDocument = XDocument.Parse(xmlDoc);
            var members = xmlDocument.Root?.Element("member");

            if (members != null)
            {
                // Get summary
                documentation.Summary = members.Element("summary")?.Value.Trim();

                // Get remarks as description
                documentation.Description = members.Element("remarks")?.Value.Trim();

                // Get parameter descriptions
                foreach (var param in members.Elements("param"))
                {
                    var name = param.Attribute("name")?.Value;
                    var description = param.Value.Trim();
                    if (name != null)
                        documentation.ParameterDescriptions[name] = description;
                }

                // Get return value description
                documentation.Returns = members.Element("returns")?.Value.Trim();

                // Get examples from <example> tags
                foreach (var example in members.Elements("example"))
                {
                    var exampleInfo = new XmlExampleInfo();

                    // Get the example content (could be in a <code> element or directly)
                    var codeElement = example.Element("code");
                    exampleInfo.Value = codeElement != null
                        ? codeElement.Value.Trim()
                        : example.Value.Trim();

                    // Check for name attribute
                    exampleInfo.Name = example.Attribute("name")?.Value ?? "Example";

                    // Check for request attribute (defaults to false = response example)
                    var requestAttr = example.Attribute("request")?.Value;
                    exampleInfo.IsRequestExample = requestAttr != null &&
                        (requestAttr.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                         requestAttr.Equals("1", StringComparison.Ordinal));

                    // Check for status code attribute (defaults to 200)
                    var statusAttr = example.Attribute("statusCode")?.Value;
                    if (int.TryParse(statusAttr, out var statusCode))
                    {
                        exampleInfo.StatusCode = statusCode;
                    }

                    if (!string.IsNullOrEmpty(exampleInfo.Value))
                    {
                        documentation.Examples.Add(exampleInfo);
                    }
                }
            }
        }
        catch
        {
            // If XML parsing fails, return empty documentation
        }

        return documentation;
    }
}
