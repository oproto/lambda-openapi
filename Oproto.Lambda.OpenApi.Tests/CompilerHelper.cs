using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.Lambda.OpenApi.Attributes;
using Oproto.Lambda.OpenApi.SourceGenerator;
using RequiredAttribute = System.ComponentModel.DataAnnotations.RequiredAttribute;
using Task = Microsoft.Build.Utilities.Task;

namespace Oproto.Lambda.OpenApi.Tests;

public class CompilerHelper
{
    public static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(CompilerGeneratedAttribute).Assembly
                .Location),

            // System references
            MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(RequiredAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ITaskItem).Assembly.Location),


            // Lambda Annotations references
            MetadataReference.CreateFromFile(
                typeof(LambdaFunctionAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(HttpApiAttribute).Assembly
                .Location),
            MetadataReference.CreateFromFile(typeof(ILambdaContext).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(RestApiAttribute).Assembly
                .Location),
            MetadataReference.CreateFromFile(typeof(LambdaHttpMethod).Assembly
                .Location),
            // Add reference to the assembly containing OpenApiInfoAttribute
            MetadataReference.CreateFromFile(typeof(OpenApiInfoAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(OpenApiSpecGenerator).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(OpenApiOutputAttribute).Assembly.Location)
        };

        // Add Microsoft.OpenApi references if you're using them
        try
        {
            var openApiAssembly = Assembly.Load("Microsoft.OpenApi");
            if (openApiAssembly != null) references.Add(MetadataReference.CreateFromFile(openApiAssembly.Location));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Warning: Could not load Microsoft.OpenApi assembly: {ex.Message}");
        }

        var compilation = CSharpCompilation.Create(
            "Tests",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable)
                .WithOptimizationLevel(OptimizationLevel.Debug));

        // Verify all references are loaded correctly
        var diagnostics = compilation.GetDiagnostics();
        foreach (var diagnostic in diagnostics)
            Debug.WriteLine($"Compilation diagnostic: {diagnostic.Id} - {diagnostic.GetMessage()}");

        return compilation;
    }
}