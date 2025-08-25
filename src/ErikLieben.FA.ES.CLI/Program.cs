using ErikLieben.FA.ES.CLI.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();
app.SetDefaultCommand<GenerateCommand>();

app.Configure(config =>
 {
     config.SetApplicationName("faes");

     config
         .AddCommand<GenerateCommand>("generate")
         .WithAlias("g")
         .WithDescription("Generate supporting code for ErikLieben.ES.FA")
         .WithExample("generate", "Solution.sln");

});

AnsiConsole.Write(new Rule("[yellow]EL.FA.ES[/]").RuleStyle("yellow"));

if (args.Length == 0)
{

}

await app.RunAsync(args);
