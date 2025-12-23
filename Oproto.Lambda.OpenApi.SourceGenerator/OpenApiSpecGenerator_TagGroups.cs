using Microsoft.CodeAnalysis;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace Oproto.Lambda.OpenApi.SourceGenerator;

public partial class OpenApiSpecGenerator
{
    /// <summary>
    /// Reads [OpenApiTagGroup] attributes from the assembly to get tag group definitions.
    /// </summary>
    /// <param name="compilation">The compilation to read attributes from.</param>
    /// <returns>A list of TagGroupInfo objects preserving the order of attribute definitions.</returns>
    private List<TagGroupInfo> GetTagGroupsFromAssembly(Compilation? compilation)
    {
        var tagGroups = new List<TagGroupInfo>();

        if (compilation == null)
            return tagGroups;

        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "OpenApiTagGroupAttribute")
                continue;

            // Constructor args: (string name, params string[] tags)
            if (attr.ConstructorArguments.Length < 1)
                continue;

            var name = attr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrEmpty(name))
                continue;

            var tags = new List<string>();
            if (attr.ConstructorArguments.Length > 1)
            {
                var tagsArg = attr.ConstructorArguments[1];
                if (tagsArg.Kind == TypedConstantKind.Array)
                {
                    tags.AddRange(tagsArg.Values
                        .Select(v => v.Value as string)
                        .Where(t => !string.IsNullOrEmpty(t))!);
                }
            }

            tagGroups.Add(new TagGroupInfo { Name = name, Tags = tags });
        }

        return tagGroups;
    }

    /// <summary>
    /// Applies the x-tagGroups extension to the OpenAPI document.
    /// </summary>
    /// <param name="document">The OpenAPI document to add the extension to.</param>
    /// <param name="tagGroups">The list of tag groups to add.</param>
    private void ApplyTagGroupsExtension(OpenApiDocument document, List<TagGroupInfo> tagGroups)
    {
        if (tagGroups == null || tagGroups.Count == 0)
            return;

        var tagGroupsArray = new OpenApiArray();
        foreach (var group in tagGroups)
        {
            var tagsArray = new OpenApiArray();
            foreach (var tag in group.Tags)
            {
                tagsArray.Add(new OpenApiString(tag));
            }

            var groupObject = new OpenApiObject
            {
                ["name"] = new OpenApiString(group.Name),
                ["tags"] = tagsArray
            };
            tagGroupsArray.Add(groupObject);
        }

        document.Extensions["x-tagGroups"] = tagGroupsArray;
    }
}
