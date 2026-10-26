using System.Globalization;
using System.Text.Json;
using Spectre.Console;

namespace Translate;

public record SearchResult(string FilePath, int LineNumber, string Content);

public record SearchOutcome(SearchResult[] Results, bool Truncated, string? Error);

public static class Search
{
	public const int MaxResults = 2000;
	private const int MaxMatchesPerFile = 200;
	private const int MaxContentLength = 300;

	public static bool IsRgAvailable() =>
		ProcessRunner.ExecuteAsync(new Command("rg", ["--version"])).GetAwaiter().GetResult().IsSuccess;

	public static async Task<SearchOutcome> SearchAsync(string query, string gameDirectory,
		CancellationToken cancellationToken = default)
	{
		var command = new Command(
			"rg",
			[
				"--json",
				"-i",
				"-F",
				"--max-count", MaxMatchesPerFile.ToString(CultureInfo.InvariantCulture),
				"--glob", "**/English/**/*.xml",
				"--glob", "**/Russian/**/*.xml",
				"-e", query,
				"--",
				gameDirectory,
			]);

		var result = await ProcessRunner.ExecuteAsync(command, cancellationToken);

		var matches = new List<SearchResult>();
		var truncated = false;

		foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
		{
			var match = ParseRgJsonLine(line);
			if (match is null)
				continue;

			if (matches.Count >= MaxResults)
			{
				truncated = true;
				break;
			}

			matches.Add(match);
		}

		string? error = null;
		if (!result.IsSuccess && result.ExitCode != 1)
		{
			error = string.IsNullOrWhiteSpace(result.StandardError)
				? $"rg exited with code {result.ExitCode}."
				: result.StandardError;
		}

		return new SearchOutcome([.. matches], truncated, error);
	}

	private static SearchResult? ParseRgJsonLine(string line)
	{
		try
		{
			using var doc = JsonDocument.Parse(line);
			var root = doc.RootElement;

			if (!root.TryGetProperty("type", out var type) || type.GetString() != "match")
				return null;

			if (!root.TryGetProperty("data", out var data))
				return null;

			if (!data.TryGetProperty("path", out var pathObj) ||
				!pathObj.TryGetProperty("text", out var path) ||
				!data.TryGetProperty("lines", out var linesObj) ||
				!linesObj.TryGetProperty("text", out var text) ||
				!data.TryGetProperty("line_number", out var number) ||
				!number.TryGetInt32(out var lineNumber))
				return null;

			var filePath = path.GetString();
			var content = text.GetString();
			if (filePath is null || content is null)
				return null;

			return new SearchResult(filePath, lineNumber, content.TrimEnd('\r', '\n'));
		}
		catch (JsonException)
		{
			return null;
		}
	}

	public static void DisplayResults(SearchOutcome outcome, string query)
	{
		var results = outcome.Results;

		if (results.Length == 0)
		{
			Console.Clear();
			if (outcome.Error is not null)
				AnsiConsole.MarkupLine("[Red]Search failed:[/] " + Markup.Escape(Shorten(outcome.Error, 500)));
			else
				AnsiConsole.MarkupLine("[Yellow]No matches found.[/]");

			Helpers.Pause();
			return;
		}

		var pageSize = Math.Max(3, (Console.WindowHeight - 8) / 2);
		var pageCount = (results.Length + pageSize - 1) / pageSize;
		var page = 0;

		while (true)
		{
			Console.Clear();

			AnsiConsole.MarkupLine(
				$"[Green]{results.Length}[/] matches found for [Green]\"{Markup.Escape(query)}\"[/]");

			if (outcome.Truncated)
			{
				AnsiConsole.MarkupLine(
					$"[Yellow]First {MaxResults} matches shown.[/]");
			}

			if (outcome.Error is not null)
			{
				AnsiConsole.MarkupLine(
					"[Yellow]rg reported problems:[/] " +
					Markup.Escape(Shorten(outcome.Error, 300)));
			}

			AnsiConsole.WriteLine();

			foreach (var result in results.Skip(page * pageSize).Take(pageSize))
			{
				AnsiConsole.MarkupLine(
					"  [Grey]" + Markup.Escape(result.FilePath) + "[/]:" + result.LineNumber);
				AnsiConsole.MarkupLine("    " + Markup.Escape(Shorten(result.Content.Trim(), MaxContentLength)));
			}

			AnsiConsole.MarkupLine(
				$"\n[Grey]Page {page + 1}/{pageCount} - any: next, Left/PgUp: back, Home/End: first/last, Esc/Q: close[/]");

			switch (Console.ReadKey(true).Key)
			{
				case ConsoleKey.Escape:
				case ConsoleKey.Q:
					return;
				case ConsoleKey.LeftArrow:
				case ConsoleKey.UpArrow:
				case ConsoleKey.PageUp:
					page = Math.Max(0, page - 1);
					break;
				case ConsoleKey.Home:
					page = 0;
					break;
				case ConsoleKey.End:
					page = pageCount - 1;
					break;
				default:
					if (page >= pageCount - 1)
						return;
					page++;
					break;
			}
		}
	}

	private static string Shorten(string text, int maxLength) =>
		text.Length <= maxLength ? text : text[..maxLength] + "…";
}