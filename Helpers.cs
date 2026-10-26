using Spectre.Console;

namespace Translate;

public static class Helpers
{
	public static string? GetFilePath(Config config)
	{
		var validPath = TryProcessPath(config.LastFile);
		if (validPath != null)
			return validPath;

		while (true)
		{
			var rawPath = Menu.PromptFilePath(config.LastFile);
			if (rawPath is null)
				return null;

			validPath = TryProcessPath(rawPath);

			if (validPath != null)
				return validPath;

			Console.WriteLine("Could not find file. Press any key to retry.");
			var key = Console.ReadKey(true);
			if (key.Key == ConsoleKey.Escape)
				return null;
		}
	}

	private static string? TryProcessPath(string? path)
	{
		path = StripQuotes(path);
		if (path.Length == 0)
			return null;

		if (!path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
			path += ".xml";

		return File.Exists(path) ? path : null;
	}

	public static string StripQuotes(string? text)
	{
		text = text?.Trim() ?? "";
		return text.Length >= 2 && text[0] == '"' && text[^1] == '"'
			? text[1..^1].Trim()
			: text;
	}

	public static void Pause(string message = "Press any key to continue...")
	{
		Console.WriteLine(message);
		Console.ReadKey(true);
	}

	public static void ShowError(string context, Exception ex)
	{
		Logger.Log(context, ex);
		AnsiConsole.MarkupLine($"[Red]{Markup.Escape(context)} failed:[/] {Markup.Escape(ex.Message)}");
		AnsiConsole.MarkupLine($"[Grey]Details written to {Markup.Escape(Logger.LogPath)}[/]");
		Pause();
	}

	public static void WriteAtomic(string path, Action<string> writeTo)
	{
		var tempPath = path + ".tmp";
		try
		{
			writeTo(tempPath);

			if (File.Exists(path))
				File.Replace(tempPath, path, null);
			else
				File.Move(tempPath, path);
		}
		catch
		{
			try
			{
				File.Delete(tempPath);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				Logger.Log($"Cleaning up {tempPath}", ex);
			}

			throw;
		}
	}
}