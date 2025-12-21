using System.CommandLine;
using Oproto.Lambda.OpenApi.Merge.Tool.Commands;

var rootCommand = new RootCommand("OpenAPI specification merge tool");

rootCommand.AddCommand(new MergeCommand());

return await rootCommand.InvokeAsync(args);
