using FsCheck;
using FsCheck.Xunit;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Oproto.Lambda.OpenApi.Merge;

namespace Oproto.Lambda.OpenApi.Merge.Tests;

/// <summary>
/// Property-based tests for tag group merging in OpenApiMerger.
/// </summary>
public class TagGroupMergePropertyTests
{
    /// <summary>
    /// Generators for tag group test data.
    /// </summary>
    private static class TagGroupGenerators
    {
        public static Gen<string> TagGroupNameGen()
        {
            return Gen.Elements(
                "User Management", "Products", "Orders", "Authentication",
                "Admin", "Reports", "Settings", "Notifications", "Payments");
        }

        public static Gen<string> TagNameGen()
        {
            return Gen.Elements(
                "Users", "Auth", "Products", "Orders", "Items", "Admin",
                "Reports", "Settings", "Notifications", "Payments", "Inventory");
        }

        public static Gen<TagGroupInfo> TagGroupInfoGen()
        {
            return from name in TagGroupNameGen()
                   from tagCount in Gen.Choose(1, 4)
                   from tags in Gen.ListOf(tagCount, TagNameGen())
                   let uniqueTags = tags.Distinct().ToList()
                   select new TagGroupInfo { Name = name, Tags = uniqueTags };
        }

        public static Gen<List<TagGroupInfo>> TagGroupListGen()
        {
            return from count in Gen.Choose(1, 3)
                   from groups in Gen.ListOf(count, TagGroupInfoGen())
                   let uniqueGroups = groups
                       .GroupBy(g => g.Name)
                       .Select(g => g.First())
                       .ToList()
                   select uniqueGroups;
        }

        public static Gen<string> SourceNameGen()
        {
            return Gen.Elements("ServiceA", "ServiceB", "Users", "Products", "Orders");
        }

        public static OpenApiDocument CreateDocumentWithTagGroups(List<TagGroupInfo> tagGroups)
        {
            var doc = new OpenApiDocument
            {
                Info = new OpenApiInfo { Title = "Test API", Version = "1.0.0" },
                Paths = new OpenApiPaths
                {
                    ["/test"] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = new OpenApiOperation
                            {
                                OperationId = "getTest",
                                Summary = "Test endpoint"
                            }
                        }
                    }
                }
            };

            if (tagGroups.Count > 0)
            {
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
                doc.Extensions["x-tagGroups"] = tagGroupsArray;
            }

            return doc;
        }

        public static MergeConfiguration CreateMergeConfig()
        {
            return new MergeConfiguration
            {
                Info = new MergeInfoConfiguration
                {
                    Title = "Merged API",
                    Version = "1.0.0"
                },
                SchemaConflict = SchemaConflictStrategy.Rename
            };
        }
    }


    /// <summary>
    /// Feature: tag-groups-extension, Property 5: Tag group merge combination
    /// For any set of OpenAPI documents with tag groups, merging them SHALL result in 
    /// all tag groups from all sources appearing in the merged output.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TagGroups_FromAllSources_ShouldAppearInMergedOutput()
    {
        var twoGroupListsGen = from groups1 in TagGroupGenerators.TagGroupListGen()
                               from groups2 in TagGroupGenerators.TagGroupListGen()
                               select (groups1, groups2);

        return Prop.ForAll(
            twoGroupListsGen.ToArbitrary(),
            tuple =>
            {
                var (groups1, groups2) = tuple;

                var doc1 = TagGroupGenerators.CreateDocumentWithTagGroups(groups1);
                var doc2 = TagGroupGenerators.CreateDocumentWithTagGroups(groups2);

                var source1 = new SourceConfiguration
                {
                    Path = "source1.json",
                    Name = "ServiceA",
                    PathPrefix = "/service1"
                };

                var source2 = new SourceConfiguration
                {
                    Path = "source2.json",
                    Name = "ServiceB",
                    PathPrefix = "/service2"
                };

                var config = TagGroupGenerators.CreateMergeConfig();
                var merger = new OpenApiMerger();
                var documents = new List<(SourceConfiguration, OpenApiDocument)>
                {
                    (source1, doc1),
                    (source2, doc2)
                };

                var result = merger.Merge(config, documents);

                // Get all unique group names from both sources
                var allGroupNames = groups1.Select(g => g.Name)
                    .Concat(groups2.Select(g => g.Name))
                    .Distinct()
                    .ToHashSet();

                // Read merged tag groups
                var mergedGroups = OpenApiMerger.ReadTagGroupsExtension(result.Document);
                var mergedGroupNames = mergedGroups.Select(g => g.Name).ToHashSet();

                // All group names should be present
                var allGroupsPresent = allGroupNames.All(name => mergedGroupNames.Contains(name));

                return allGroupsPresent
                    .Label($"All tag groups should be present. Expected: {string.Join(", ", allGroupNames)}, Got: {string.Join(", ", mergedGroupNames)}")
                    .And(result.Success)
                    .Label("Merge should succeed");
            });
    }

    /// <summary>
    /// Feature: tag-groups-extension, Property 6: Tag group merge same-name merging
    /// For any two OpenAPI documents that define tag groups with the same name, 
    /// the Merge_Tool SHALL combine the tags from both groups into a single group with that name.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SameNamedTagGroups_ShouldBeMerged_IntoSingleGroup()
    {
        var sameNameGroupsGen = from name in TagGroupGenerators.TagGroupNameGen()
                                from tags1Count in Gen.Choose(1, 3)
                                from tags1 in Gen.ListOf(tags1Count, TagGroupGenerators.TagNameGen())
                                from tags2Count in Gen.Choose(1, 3)
                                from tags2 in Gen.ListOf(tags2Count, TagGroupGenerators.TagNameGen())
                                let uniqueTags1 = tags1.Distinct().ToList()
                                let uniqueTags2 = tags2.Distinct().ToList()
                                select (name, uniqueTags1, uniqueTags2);

        return Prop.ForAll(
            sameNameGroupsGen.ToArbitrary(),
            tuple =>
            {
                var (groupName, tags1, tags2) = tuple;

                var groups1 = new List<TagGroupInfo>
                {
                    new TagGroupInfo { Name = groupName, Tags = tags1 }
                };
                var groups2 = new List<TagGroupInfo>
                {
                    new TagGroupInfo { Name = groupName, Tags = tags2 }
                };

                var doc1 = TagGroupGenerators.CreateDocumentWithTagGroups(groups1);
                var doc2 = TagGroupGenerators.CreateDocumentWithTagGroups(groups2);

                var source1 = new SourceConfiguration { Path = "source1.json", Name = "ServiceA", PathPrefix = "/service1" };
                var source2 = new SourceConfiguration { Path = "source2.json", Name = "ServiceB", PathPrefix = "/service2" };

                var config = TagGroupGenerators.CreateMergeConfig();
                var merger = new OpenApiMerger();
                var documents = new List<(SourceConfiguration, OpenApiDocument)>
                {
                    (source1, doc1),
                    (source2, doc2)
                };

                var result = merger.Merge(config, documents);
                var mergedGroups = OpenApiMerger.ReadTagGroupsExtension(result.Document);

                // Should have exactly one group with this name
                var groupsWithName = mergedGroups.Where(g => g.Name == groupName).ToList();
                var hasSingleGroup = groupsWithName.Count == 1;

                // The merged group should contain all tags from both sources
                var allExpectedTags = tags1.Concat(tags2).Distinct().ToHashSet();
                var mergedTags = groupsWithName.FirstOrDefault()?.Tags.ToHashSet() ?? new HashSet<string>();
                var allTagsPresent = allExpectedTags.All(t => mergedTags.Contains(t));

                return hasSingleGroup
                    .Label($"Should have exactly one group named '{groupName}', got {groupsWithName.Count}")
                    .And(allTagsPresent)
                    .Label($"All tags should be merged. Expected: {string.Join(", ", allExpectedTags)}, Got: {string.Join(", ", mergedTags)}")
                    .And(result.Success)
                    .Label("Merge should succeed");
            });
    }


    /// <summary>
    /// Feature: tag-groups-extension, Property 7: Tag group merge deduplication
    /// For any merged tag group, the tags array SHALL contain no duplicate entries.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MergedTagGroups_ShouldHaveNoDuplicateTags()
    {
        var overlappingTagsGen = from name in TagGroupGenerators.TagGroupNameGen()
                                 from sharedTag in TagGroupGenerators.TagNameGen()
                                 from otherTags1 in Gen.ListOf(2, TagGroupGenerators.TagNameGen())
                                 from otherTags2 in Gen.ListOf(2, TagGroupGenerators.TagNameGen())
                                 let tags1 = new List<string> { sharedTag }.Concat(otherTags1).Distinct().ToList()
                                 let tags2 = new List<string> { sharedTag }.Concat(otherTags2).Distinct().ToList()
                                 select (name, tags1, tags2);

        return Prop.ForAll(
            overlappingTagsGen.ToArbitrary(),
            tuple =>
            {
                var (groupName, tags1, tags2) = tuple;

                var groups1 = new List<TagGroupInfo>
                {
                    new TagGroupInfo { Name = groupName, Tags = tags1 }
                };
                var groups2 = new List<TagGroupInfo>
                {
                    new TagGroupInfo { Name = groupName, Tags = tags2 }
                };

                var doc1 = TagGroupGenerators.CreateDocumentWithTagGroups(groups1);
                var doc2 = TagGroupGenerators.CreateDocumentWithTagGroups(groups2);

                var source1 = new SourceConfiguration { Path = "source1.json", Name = "ServiceA", PathPrefix = "/service1" };
                var source2 = new SourceConfiguration { Path = "source2.json", Name = "ServiceB", PathPrefix = "/service2" };

                var config = TagGroupGenerators.CreateMergeConfig();
                var merger = new OpenApiMerger();
                var documents = new List<(SourceConfiguration, OpenApiDocument)>
                {
                    (source1, doc1),
                    (source2, doc2)
                };

                var result = merger.Merge(config, documents);
                var mergedGroups = OpenApiMerger.ReadTagGroupsExtension(result.Document);

                // Check that no group has duplicate tags
                var noDuplicates = mergedGroups.All(g =>
                    g.Tags.Count == g.Tags.Distinct().Count());

                return noDuplicates
                    .Label("No tag group should have duplicate tags")
                    .And(result.Success)
                    .Label("Merge should succeed");
            });
    }

    /// <summary>
    /// Feature: tag-groups-extension, Property 8: Tag group merge preservation
    /// For any set of OpenAPI documents where some have tag groups and others do not, 
    /// all tag groups from documents that have them SHALL be preserved in the merged output.
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TagGroups_ShouldBePreserved_WhenSomeDocsHaveNone()
    {
        return Prop.ForAll(
            TagGroupGenerators.TagGroupListGen().ToArbitrary(),
            groups =>
            {
                // Doc1 has tag groups, Doc2 has none
                var doc1 = TagGroupGenerators.CreateDocumentWithTagGroups(groups);
                var doc2 = TagGroupGenerators.CreateDocumentWithTagGroups(new List<TagGroupInfo>());

                var source1 = new SourceConfiguration { Path = "source1.json", Name = "ServiceA", PathPrefix = "/service1" };
                var source2 = new SourceConfiguration { Path = "source2.json", Name = "ServiceB", PathPrefix = "/service2" };

                var config = TagGroupGenerators.CreateMergeConfig();
                var merger = new OpenApiMerger();
                var documents = new List<(SourceConfiguration, OpenApiDocument)>
                {
                    (source1, doc1),
                    (source2, doc2)
                };

                var result = merger.Merge(config, documents);
                var mergedGroups = OpenApiMerger.ReadTagGroupsExtension(result.Document);

                // All original groups should be preserved
                var originalGroupNames = groups.Select(g => g.Name).ToHashSet();
                var mergedGroupNames = mergedGroups.Select(g => g.Name).ToHashSet();
                var allPreserved = originalGroupNames.SetEquals(mergedGroupNames);

                return allPreserved
                    .Label($"All tag groups should be preserved. Expected: {string.Join(", ", originalGroupNames)}, Got: {string.Join(", ", mergedGroupNames)}")
                    .And(result.Success)
                    .Label("Merge should succeed");
            });
    }


    /// <summary>
    /// Feature: tag-groups-extension, Property 9: Tag group merge order
    /// For any sequence of OpenAPI documents being merged, tag groups SHALL appear in the order 
    /// of their first occurrence across all documents, with new groups appended after existing ones.
    /// **Validates: Requirements 4.1, 4.2, 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TagGroups_ShouldPreserveFirstOccurrenceOrder()
    {
        var orderedGroupsGen = from name1 in Gen.Constant("Group_A")
                               from name2 in Gen.Constant("Group_B")
                               from name3 in Gen.Constant("Group_C")
                               from tags in Gen.ListOf(2, TagGroupGenerators.TagNameGen())
                               let uniqueTags = tags.Distinct().ToList()
                               select (name1, name2, name3, uniqueTags);

        return Prop.ForAll(
            orderedGroupsGen.ToArbitrary(),
            tuple =>
            {
                var (name1, name2, name3, tags) = tuple;

                // Doc1 has Group_A and Group_B
                var groups1 = new List<TagGroupInfo>
                {
                    new TagGroupInfo { Name = name1, Tags = tags },
                    new TagGroupInfo { Name = name2, Tags = tags }
                };

                // Doc2 has Group_B and Group_C (Group_B already exists, Group_C is new)
                var groups2 = new List<TagGroupInfo>
                {
                    new TagGroupInfo { Name = name2, Tags = tags },
                    new TagGroupInfo { Name = name3, Tags = tags }
                };

                var doc1 = TagGroupGenerators.CreateDocumentWithTagGroups(groups1);
                var doc2 = TagGroupGenerators.CreateDocumentWithTagGroups(groups2);

                var source1 = new SourceConfiguration { Path = "source1.json", Name = "ServiceA", PathPrefix = "/service1" };
                var source2 = new SourceConfiguration { Path = "source2.json", Name = "ServiceB", PathPrefix = "/service2" };

                var config = TagGroupGenerators.CreateMergeConfig();
                var merger = new OpenApiMerger();
                var documents = new List<(SourceConfiguration, OpenApiDocument)>
                {
                    (source1, doc1),
                    (source2, doc2)
                };

                var result = merger.Merge(config, documents);
                var mergedGroups = OpenApiMerger.ReadTagGroupsExtension(result.Document);
                var mergedGroupNames = mergedGroups.Select(g => g.Name).ToList();

                // Expected order: Group_A, Group_B (from doc1), then Group_C (new from doc2)
                var expectedOrder = new List<string> { name1, name2, name3 };
                var orderCorrect = mergedGroupNames.SequenceEqual(expectedOrder);

                return orderCorrect
                    .Label($"Tag groups should be in order of first occurrence. Expected: {string.Join(", ", expectedOrder)}, Got: {string.Join(", ", mergedGroupNames)}")
                    .And(result.Success)
                    .Label("Merge should succeed");
            });
    }

    /// <summary>
    /// Feature: tag-groups-extension, Property 10: Tag group round-trip
    /// For any valid x-tagGroups extension in an OpenAPI document, reading and then writing 
    /// the document SHALL produce an equivalent x-tagGroups structure.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TagGroups_ShouldRoundTrip_ReadWriteRead()
    {
        return Prop.ForAll(
            TagGroupGenerators.TagGroupListGen().ToArbitrary(),
            originalGroups =>
            {
                // Create a document with tag groups
                var doc = TagGroupGenerators.CreateDocumentWithTagGroups(originalGroups);

                // Read the tag groups
                var readGroups = OpenApiMerger.ReadTagGroupsExtension(doc);

                // Write to a new document
                var newDoc = new OpenApiDocument
                {
                    Info = new OpenApiInfo { Title = "Test", Version = "1.0.0" },
                    Paths = new OpenApiPaths()
                };
                OpenApiMerger.WriteTagGroupsExtension(newDoc, readGroups);

                // Read again
                var reReadGroups = OpenApiMerger.ReadTagGroupsExtension(newDoc);

                // Compare
                var sameCount = originalGroups.Count == reReadGroups.Count;
                var sameContent = originalGroups.All(og =>
                {
                    var matching = reReadGroups.FirstOrDefault(rg => rg.Name == og.Name);
                    if (matching == null) return false;
                    return og.Tags.OrderBy(t => t).SequenceEqual(matching.Tags.OrderBy(t => t));
                });

                return sameCount
                    .Label($"Should have same number of groups. Original: {originalGroups.Count}, After round-trip: {reReadGroups.Count}")
                    .And(sameContent)
                    .Label("All groups should have same content after round-trip")
                    .When(originalGroups.Count > 0);
            });
    }

    /// <summary>
    /// Feature: tag-groups-extension, Property 5 (additional): No tag groups when none defined
    /// When no source specs define tag groups, the merged output SHALL NOT include the x-tagGroups extension.
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoTagGroups_WhenNoneDefinedInSources()
    {
        return Prop.ForAll(
            TagGroupGenerators.SourceNameGen().ToArbitrary(),
            sourceName =>
            {
                // Both documents have no tag groups
                var doc1 = TagGroupGenerators.CreateDocumentWithTagGroups(new List<TagGroupInfo>());
                var doc2 = TagGroupGenerators.CreateDocumentWithTagGroups(new List<TagGroupInfo>());

                var source1 = new SourceConfiguration { Path = "source1.json", Name = sourceName + "_1", PathPrefix = "/service1" };
                var source2 = new SourceConfiguration { Path = "source2.json", Name = sourceName + "_2", PathPrefix = "/service2" };

                var config = TagGroupGenerators.CreateMergeConfig();
                var merger = new OpenApiMerger();
                var documents = new List<(SourceConfiguration, OpenApiDocument)>
                {
                    (source1, doc1),
                    (source2, doc2)
                };

                var result = merger.Merge(config, documents);

                // Should not have x-tagGroups extension
                var hasTagGroups = result.Document.Extensions.ContainsKey("x-tagGroups");

                return (!hasTagGroups)
                    .Label("Merged document should not have x-tagGroups when no sources define them")
                    .And(result.Success)
                    .Label("Merge should succeed");
            });
    }
}
