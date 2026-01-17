using System.Reflection;
using System.Text.RegularExpressions;

namespace Oproto.Lambda.OpenApi.Merge.Cdk;

/// <summary>
/// Loads and processes the Step Functions state machine definition.
/// </summary>
public static class StateMachineDefinitionLoader
{
    private const string StateMachineResourceName = "Oproto.Lambda.OpenApi.Merge.Cdk.StateMachines.debounce-state-machine.json";

    /// <summary>
    /// Loads the state machine definition from embedded resources and substitutes placeholders.
    /// </summary>
    /// <param name="substitutions">Dictionary of placeholder names to values (without ${} wrapper).</param>
    /// <returns>The state machine definition JSON with substitutions applied.</returns>
    public static string LoadDefinition(IDictionary<string, string> substitutions)
    {
        var definition = LoadEmbeddedResource();
        return ApplySubstitutions(definition, substitutions);
    }

    /// <summary>
    /// Loads the raw state machine definition from embedded resources.
    /// </summary>
    /// <returns>The raw state machine definition JSON.</returns>
    public static string LoadRawDefinition()
    {
        return LoadEmbeddedResource();
    }

    private static string LoadEmbeddedResource()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(StateMachineResourceName);
        
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Could not find embedded resource '{StateMachineResourceName}'. " +
                "Ensure the state machine JSON file is included as an embedded resource.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string ApplySubstitutions(string definition, IDictionary<string, string> substitutions)
    {
        var result = definition;
        
        foreach (var (key, value) in substitutions)
        {
            // Replace ${Key} with the value
            result = result.Replace($"${{{key}}}", value);
        }

        return result;
    }

    /// <summary>
    /// Creates the standard substitutions dictionary for the state machine.
    /// </summary>
    /// <param name="debounceTableName">The DynamoDB table name for debounce state.</param>
    /// <param name="mergeFunctionArn">The ARN of the merge Lambda function.</param>
    /// <param name="outputBucket">The name of the output S3 bucket.</param>
    /// <param name="debounceSeconds">The debounce wait duration in seconds.</param>
    /// <returns>A dictionary of substitutions.</returns>
    public static IDictionary<string, string> CreateSubstitutions(
        string debounceTableName,
        string mergeFunctionArn,
        string outputBucket,
        int debounceSeconds)
    {
        return new Dictionary<string, string>
        {
            ["DebounceTable"] = debounceTableName,
            ["MergeFunctionArn"] = mergeFunctionArn,
            ["OutputBucket"] = outputBucket,
            ["DebounceSeconds"] = debounceSeconds.ToString()
        };
    }
}
