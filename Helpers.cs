namespace Translate;

public static class Helpers
{
	public static string GetFilePath(Config config, string configPath)
	{
		var validPath = TryProcessPath(config.LastFile, config, configPath);
		if (validPath != null)
			return validPath;

		while (true)
		{
			var rawPath = Menu.ChangeFilePath();
			validPath = TryProcessPath(rawPath, config, configPath);

			if (validPath != null)
				return validPath;

			Console.WriteLine("Could not find such file. Press any key to retry.");
			Console.ReadKey();
		}
	}

	private static string? TryProcessPath(string? path, Config config, string configPath)
	{
		if (string.IsNullOrWhiteSpace(path))
			return null;

		if (path.StartsWith('"') && path.EndsWith('"'))
			path = path[1..^1];

		if (!path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
			path += ".xml";

		if (!File.Exists(path))
			return null;
		config.LastFile = path;
		Config.Write(configPath, config);
		return path;
	}
}