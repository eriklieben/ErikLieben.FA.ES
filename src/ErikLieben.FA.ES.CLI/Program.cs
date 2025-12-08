#pragma warning disable S1192 // String literals should not be duplicated - command configuration

using System.Text;
using ErikLieben.FA.ES.CLI.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

// Enable UTF-8 for Unicode character support in console
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var app = new CommandApp();
app.SetDefaultCommand<GenerateCommand>();

app.Configure(config =>
 {
     config.SetApplicationName("faes");

     config
         .AddCommand<GenerateCommand>("generate")
         .WithAlias("g")
         .WithDescription("Generate supporting code for ErikLieben.ES.FA")
         .WithExample("generate", "Solution.sln")
         .WithExample("generate", "Solution.slnx");

     config
         .AddCommand<WatchCommand>("watch")
         .WithAlias("w")
         .WithDescription("Watch for file changes and automatically regenerate code")
         .WithExample("watch", "Solution.sln")
         .WithExample("watch", "--verbose");

     config
         .AddCommand<UpdateCommand>("update")
         .WithAlias("u")
         .WithDescription("Update ErikLieben.FA.ES packages and migrate code for breaking changes")
         .WithExample("update")
         .WithExample("update", "--version 2.0.0")
         .WithExample("update", "--dry-run");
});

await app.RunAsync(args);
