using System.Collections;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Oproto.Lambda.OpenApi.Build;
using Oproto.Lambda.OpenApi.SourceGenerator;

namespace Oproto.Lambda.OpenApi.Tests;

public class ExtractOpenApiSpecTaskTests : IDisposable
{
    private readonly string _outputPath;
    private readonly string _tempDirectory;

    public ExtractOpenApiSpecTaskTests()
    {
        // Create temp directory for test outputs
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
        _outputPath = Path.Combine(_tempDirectory, "openapi.json");
    }

    public void Dispose()
    {
        // Give GC a chance to release any file handles
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Retry deletion with small delays to handle any lingering locks
        for (var i = 0; i < 3; i++)
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                    Directory.Delete(_tempDirectory, true);
                return;
            }
            catch (UnauthorizedAccessException) when (i < 2)
            {
                Thread.Sleep(100);
            }
            catch (IOException) when (i < 2)
            {
                Thread.Sleep(100);
            }
        }
    }

    [Fact]
    public void Execute_ValidAssembly_WritesSpecToFile()
    {
        // Arrange
        var source = @"
    using Amazon.Lambda.Annotations;
    using Amazon.Lambda.Annotations.APIGateway;
    using Amazon.Lambda.Core;
    using System.Threading.Tasks;
    
    namespace Test 
    {
        public class TestFunctions 
        {
            [LambdaFunction]
            [HttpApi(LambdaHttpMethod.Get, ""/items/{id}"")]
            public async Task<ItemResponse> GetItemHttp(
                [FromRoute] string id,
                [FromServices] ILambdaContext context,
                [FromQuery] string filter = null)
            {
                return new ItemResponse { Id = id };
            }
        }

        public class ItemResponse
        {
            public string Id { get; set; }
        }
    }";

        var compilation = CompilerHelper.CreateCompilation(source);
        var generator = new OpenApiSpecGenerator();

        // Run the generator
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation,
            out var outputCompilation,
            out var diagnostics);

        // Check generator diagnostics
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var assemblyPath = Path.Combine(_tempDirectory, "test.dll");

        // Use the outputCompilation instead of the original compilation
        using (var fs = File.Create(assemblyPath))
        {
            var emitResult = outputCompilation.Emit(fs);
            Assert.True(emitResult.Success,
                "Compilation failed: " + string.Join(Environment.NewLine,
                    emitResult.Diagnostics.Select(d => d.ToString())));
        }

        // Copy the Oproto.Lambda.OpenApi.dll to the temp directory so MetadataLoadContext can find it
        var openApiDllSource = typeof(Oproto.Lambda.OpenApi.Attributes.OpenApiOutputAttribute).Assembly.Location;
        var openApiDllDest = Path.Combine(_tempDirectory, "Oproto.Lambda.OpenApi.dll");
        File.Copy(openApiDllSource, openApiDllDest, true);

        // Load assembly from bytes to avoid file locking (diagnostic only)
        var assemblyBytes = File.ReadAllBytes(assemblyPath);
        var loadedAssembly = Assembly.Load(assemblyBytes);
        var attributes = loadedAssembly.GetCustomAttributes(true);
        foreach (var attr in attributes) Console.WriteLine($"Loaded assembly attribute: {attr.GetType().FullName}");


        var mockBuildEngine = new MockBuildEngine();
        var task = new ExtractOpenApiSpecTask
        {
            AssemblyPath = assemblyPath,
            OutputPath = _outputPath,
            BuildEngine = mockBuildEngine
        };

        // Add diagnostic logging
        mockBuildEngine.LogMessageEvent(new BuildMessageEventArgs(
            "Assembly Path: " + assemblyPath,
            "Diagnostic", "ExtractOpenApiSpec", MessageImportance.High));
        mockBuildEngine.LogMessageEvent(new BuildMessageEventArgs(
            "Output Path: " + _outputPath,
            "Diagnostic", "ExtractOpenApiSpec", MessageImportance.High));

        // Act
        var result = task.Execute();

        // Assert
        if (!result || !File.Exists(_outputPath))
        {
            var assemblyExists = File.Exists(assemblyPath);
            var outputDirExists = Directory.Exists(Path.GetDirectoryName(_outputPath));

            Assert.Fail(
                $"Task execution details:\n" +
                $"Task result: {result}\n" +
                $"Assembly exists: {assemblyExists}\n" +
                $"Assembly path: {assemblyPath}\n" +
                $"Output directory exists: {outputDirExists}\n" +
                $"Output path: {_outputPath}\n" +
                $"Errors: {string.Join(Environment.NewLine, mockBuildEngine.ErrorMessages)}\n" +
                $"Warnings: {string.Join(Environment.NewLine, mockBuildEngine.WarningMessages)}\n" +
                $"Messages: {string.Join(Environment.NewLine, mockBuildEngine.Messages)}");
        }

        var content = File.ReadAllText(_outputPath);
        Assert.Contains("\"openapi\": \"3.0.1\"", content);

        // Validate the OpenAPI document
        var openApiDocument = new OpenApiStringReader().Read(content, out var openApiDiagnostics);

        // Log any validation messages for debugging
        foreach (var diagnostic in openApiDiagnostics.Errors) Console.WriteLine($"{diagnostic}: {diagnostic.Message}");

        // Assert no errors in the OpenAPI spec
        Assert.Empty(openApiDiagnostics.Errors);

        // Additional structural validations
        Assert.NotNull(openApiDocument.Paths);

        // Verify the /items/{id} endpoint
        Assert.True(openApiDocument.Paths.ContainsKey("/items/{id}"));
        var pathItem = openApiDocument.Paths["/items/{id}"];
        Assert.NotNull(pathItem.Operations[OperationType.Get]);

        // Verify parameters
        var operation = pathItem.Operations[OperationType.Get];
        Assert.Contains(operation.Parameters, p => p.Name == "id" && p.In == ParameterLocation.Path);
        Assert.Contains(operation.Parameters, p => p.Name == "filter" && p.In == ParameterLocation.Query);
        Assert.DoesNotContain(operation.Parameters,
            p => p.Name == "context"); // FromServices parameter should be excluded
    }

    [Fact]
    public void Execute_InvalidAssemblyPath_ReturnsFalse()
    {
        // Arrange
        var task = new ExtractOpenApiSpecTask
        {
            AssemblyPath = "invalid.dll",
            OutputPath = _outputPath,
            BuildEngine = new MockBuildEngine()
        };

        // Act
        var result = task.Execute();

        // Assert
        Assert.False(result);
        Assert.False(File.Exists(_outputPath));
    }
}

// Mock build engine for testing MSBuild tasks
public class MockBuildEngine : IBuildEngine
{
    public List<string> ErrorMessages { get; } = new();
    public List<string> WarningMessages { get; } = new();
    public List<string> Messages { get; } = new();

    public void LogErrorEvent(BuildErrorEventArgs e)
    {
        ErrorMessages.Add(e.Message);
    }

    public void LogWarningEvent(BuildWarningEventArgs e)
    {
        WarningMessages.Add(e.Message);
    }

    public void LogMessageEvent(BuildMessageEventArgs e)
    {
        Messages.Add(e.Message);
    }

    public void LogCustomEvent(CustomBuildEventArgs e)
    {
    }

    public bool BuildProjectFile(string projectFileName, string[] targetNames,
        IDictionary globalProperties, IDictionary targetOutputs)
    {
        return true;
    }

    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => string.Empty;
}
