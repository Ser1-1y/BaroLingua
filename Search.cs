using System.Text.RegularExpressions;
using Spectre.Console;

namespace Translate;

public record SearchResult(string FilePath, int LineNumber, string Content);

public static partial class Search
{
	[GeneratedRegex(@"^(?<file>.+):(?<line>\d+):(?<content>.*)$")]
	private static partial Regex RgLineRegex();

	public static bool IsRgAvailable() => Bash.CommandExists("rg");

	public static async Task<SearchResult[]> SearchAsync(string query, string gameDirectory)
	{
		var command = new Command(
			"rg",
			[
				"-i",
				"-F",
				"--no-heading",
				"-n",
				query,
				gameDirectory,
				"--glob", "**/English/**/*.xml",
				"--glob", "**/Russian/**/*.xml",
			]);

		var result = await Bash.ExecuteAsync(command);

		if (string.IsNullOrEmpty(result.StandardOutput))
			return [];

		var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

		return [.. lines.Select(ParseRgLine).OfType<SearchResult>()];
	}

	private static SearchResult? ParseRgLine(string line)
	{
		var match = RgLineRegex().Match(line);
		if (!match.Success)
			return null;

		var filePath = match.Groups["file"].Value;
		if (!int.TryParse(match.Groups["line"].Value, out var lineNumber))
			return null;

		var content = match.Groups["content"].Value;
		return new SearchResult(filePath, lineNumber, content);
	}

	public static void DisplayResults(SearchResult[] results, string query)
	{
		if (results.Length == 0)
		{
			AnsiConsole.MarkupLine("[Yellow]No matches found.[/]");
			return;
		}

		AnsiConsole.MarkupLine(
			"[Green]" + results.Length + "[/] match(es) found for [Green]\"" + Markup.Escape(query) + "\"[/]:\n");

		foreach (var result in results)
		{
			AnsiConsole.MarkupLine(
				"  [Grey]" + Markup.Escape(result.FilePath) + "[/]:" + result.LineNumber);
			AnsiConsole.MarkupLine("    " + Markup.Escape(result.Content));
		}
	}
}