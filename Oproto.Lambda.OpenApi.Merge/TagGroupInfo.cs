using System.Collections.Generic;

namespace Oproto.Lambda.OpenApi.Merge;

/// <summary>
/// Represents a tag group for the x-tagGroups extension.
/// </summary>
public class TagGroupInfo
{
    /// <summary>
    /// The name of the tag group.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The tags that belong to this group.
    /// </summary>
    public List<string> Tags { get; set; } = new List<string>();
}
