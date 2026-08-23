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
			var rawPath = Menu.ChangeFilePath();
			if (rawPath is null)
				return config.LastFile;

			validPath = TryProcessPath(rawPath);

			if (validPath != null)
				return validPath;

			Console.WriteLine("Could not find such file. Press any key to retry, or Escape to go back.");
			var key = Console.ReadKey(true);
			if (key.Key == ConsoleKey.Escape)
				return config.LastFile;
		}
	}

	private static string? TryProcessPath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return null;

		if (path.StartsWith('"') && path.EndsWith('"'))
			path = path[1..^1];

		if (!path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
			path += ".xml";

		return !File.Exists(path)
			? null
			: path;
	}
}