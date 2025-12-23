namespace Oproto.Lambda.OpenApi.SourceGenerator;

/// <summary>
/// Represents a tag group for the x-tagGroups extension.
/// </summary>
public class TagGroupInfo
{
    /// <summary>
    /// The name of the tag group.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The tags that belong to this group.
    /// </summary>
    public List<string> Tags { get; set; } = new List<string>();
}
