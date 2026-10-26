using System.Text;

namespace Translate;

public static class Program
{
	internal const string ConfigPath = "config.json";

	private const int MaxConsecutiveFailures = 3;

	public static int Main()
	{
		if (Console.IsInputRedirected || Console.IsOutputRedirected)
		{
			Console.Error.WriteLine("This program needs an interactive terminal.");
			return 1;
		}

		// Windows
		Console.OutputEncoding = Encoding.UTF8;
		Console.CancelKeyPress += OnCancelKeyPress;

		try
		{
			Run();
			return 0;
		}
		catch (Exception ex)
		{
			Helpers.ShowError("Unexpected error", ex);
			return 1;
		}
	}

	private static void Run()
	{
		var failures = 0;

		while (true)
		{
			var config = Config.TryGet();

			while (!config.RestartRequested)
			{
				try
				{
					if (Menu.StartMenu(config) == MenuResult.Exit)
						return;

					failures = 0;
				}
				catch (Exception ex)
				{
					if (++failures >= MaxConsecutiveFailures)
						throw;

					Helpers.ShowError("That action", ex);
				}
			}
		}
	}

	private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
	{
		e.Cancel = true;

		try
		{
			Session.Save();
		}
		catch (Exception ex)
		{
			Logger.Log("Saving Ctrl+C", ex);
		}

		try
		{
			Console.CursorVisible = true;
		}
		catch (IOException)
		{
			// No console
		}

		Environment.Exit(130);
	}
}