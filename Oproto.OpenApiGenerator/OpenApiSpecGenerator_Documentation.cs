using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Oproto.OpenApiGenerator;

public partial class OpenApiSpecGenerator
{
    // Add method to extract documentation from XML comments
    private DocumentationInfo GetDocumentation(IMethodSymbol methodSymbol)
    {
        var documentation = new DocumentationInfo();
        var xmlDoc = methodSymbol.GetDocumentationCommentXml();

        if (string.IsNullOrEmpty(xmlDoc))
            return documentation;

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
                if (name != null) documentation.ParameterDescriptions[name] = description;
            }

            // Get return value description
            documentation.Returns = members.Element("returns")?.Value.Trim();
        }

        return documentation;
    }
}
