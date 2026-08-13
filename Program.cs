namespace Translate;

public static class Program
{
	internal const string ConfigPath = "config.json";

	public static void Main()
	{
		var config = Config.TryGet();

		var running = true;
		while (running)
		{
			var result = Menu.StartMenu(config);
			if (result == MenuResult.Exit)
				running = false;
		}
	}
}