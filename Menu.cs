using Spectre.Console;

namespace Translate;

public enum MenuResult
{
	Continue,
	Exit,
}

public enum Settings
{
	Continue,
	ShowTranslated,
	ChangeFilePath,
	DebugMode,
	SaveAndExit,
	ExitWithoutSaving,
	DeleteConfigFile,
	ShowOriginalNodes,
}

public enum StartMenuAction
{
	Start,
	Continue,
	Settings,
	ChangeFilePath,
	Help,
	Exit,
}

public static class Menu
{
	private static readonly Option<MenuResult>[] BoolChoice =
	[
		new("Yes", MenuResult.Continue),
		new("No", MenuResult.Exit),
	];

	private static readonly Option<MenuResult>[] OnOffChoice =
	[
		new("On", MenuResult.Continue),
		new("Off", MenuResult.Exit),
	];

	private static readonly Option<Settings>[] BaseSettingsChoice =
	[
		new("Continue", Settings.Continue),
		new("Show Translated Strings", Settings.ShowTranslated),
		new("Change File Path", Settings.ChangeFilePath),
		new("Debug Mode", Settings.DebugMode),
		new("Save And Exit", Settings.SaveAndExit),
		new("Exit Without Saving", Settings.ExitWithoutSaving),
	];

	private static bool VisualYesNoMenu(string title) =>
		Tui.ChoiceAsync(BoolChoice, title).Result == MenuResult.Continue;

	private static bool VisualOnOffMenu(string title) =>
		Tui.ChoiceAsync(OnOffChoice, title).Result == MenuResult.Continue;

	public static void ConfigMenu(string configPath)
	{
		Console.Clear();
		AnsiConsole.MarkupLine("[Red]No config file found[/]");
		var choice = VisualYesNoMenu("Do you want to change the config now?");
		var config = new Config();
		if (choice)
			SettingsMenu(configPath, config);
		else
			Config.Write(configPath, config);
	}

	public static async void SettingsMenu(string configPath, Config config)
	{
		try
		{
			var loop = true;
			while (loop)
			{
				Console.Clear();

				var choices = new List<Option<Settings>>(BaseSettingsChoice);

				if (config.DebugMode)
				{
					choices.Add(new Option<Settings>("Delete Config File", Settings.DeleteConfigFile));
					choices.Add(new Option<Settings>("Show Original Nodes", Settings.ShowOriginalNodes));
				}

				var choice = await Tui.ChoiceAsync([.. choices], $"Settings v{config.Version}");

				// ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
				switch (choice)
				{
					case Settings.Continue:
						Config.Write(configPath, config);
						loop = false;
						break;
					case Settings.ShowTranslated:
						config.ShowLoadedStrings =
							VisualOnOffMenu(
								$"Show translated strings: {(config.ShowLoadedStrings ? "[ON]" : "[OFF]")}");
						break;
					case Settings.ChangeFilePath:
						config.LastFile = ChangeFilePath(configPath, config, false);
						break;
					case Settings.DebugMode:
						config.DebugMode = VisualOnOffMenu($"Debug Mode: {(config.DebugMode ? "[ON]" : "[OFF]")}");
						break;
					case Settings.SaveAndExit:
						Config.Write(configPath, config);
						loop = false;
						break;
					case Settings.ExitWithoutSaving:
						if (VisualYesNoMenu("Are you sure you want to exit?"))
							loop = false;
						break;
					case Settings.DeleteConfigFile:
						if (VisualYesNoMenu(
								"Are you sure you want to delete the config file?\nThis will restart the program."))
						{
							File.Delete(configPath);
							return;
						}

						break;
					case Settings.ShowOriginalNodes:
						config.ShowOriginalNodes =
							VisualOnOffMenu($"Show original nodes: {(config.ShowOriginalNodes ? "[ON]" : "[OFF]")}");
						break;
				}
			}
		}
		catch (Exception)
		{
			//
		}
	}

	private static string? ChangeFilePath(string configPath, Config config, bool startMenuAfter)
	{
		Console.Clear();
		AnsiConsole.WriteLine("Enter path to the XML file:");

		var newFilePath = Tui.Input(config.LastFile ?? "");
		config.LastFile = newFilePath;

		Config.Write(configPath, config);
		if (startMenuAfter)
			StartMenu(config);
		return newFilePath;
	}

	public static string? ChangeFilePath()
	{
		Console.Clear();
		AnsiConsole.WriteLine("Enter path to the XML file:");
		return Tui.Input();
	}

	public static MenuResult StartMenu(Config config)
	{
		Console.Clear();
		const string configPath = Program.ConfigPath;

		var startOptions = new Option<StartMenuAction>[]
		{
			new(config.FirstTimeUsing ? "Start" : "Continue",
				config.FirstTimeUsing ? StartMenuAction.Start : StartMenuAction.Continue),
			new("Settings", StartMenuAction.Settings),
			new("Change File Path", StartMenuAction.ChangeFilePath),
			new("Help", StartMenuAction.Help),
			new("Exit", StartMenuAction.Exit),
		};

		var choice = Tui.ChoiceAsync(startOptions, "Menu").Result;

		switch (choice)
		{
			case StartMenuAction.Start:
			case StartMenuAction.Continue:
				var filePath = Helpers.GetFilePath(config);
				if (filePath is null)
					return MenuResult.Continue;
				var result = Translation.Process(filePath, config, configPath);
				return result == TranslationResult.ExitRequested ? MenuResult.Exit : MenuResult.Continue;

			case StartMenuAction.Settings:
				SettingsMenu(configPath, config);
				return MenuResult.Continue;

			case StartMenuAction.ChangeFilePath:
				config.LastFile = ChangeFilePath(configPath, config, true);
				return MenuResult.Continue;

			case StartMenuAction.Help:
				AnsiConsole.Markup($"[Green]Escape[/]      - Save and exit\n" +
								   $"[Green]\"{Document.ExitCommand}\"[/]      - Save and exit\n" +
								   $"[Green]\"{Document.SettingsCommand}\"[/] - Access Settings\n");
				AnsiConsole.WriteLine("Press any key to continue...");
				Console.ReadKey();
				return MenuResult.Continue;

			case StartMenuAction.Exit:
			case null:
				return VisualYesNoMenu("Are you sure you want to exit?") ? MenuResult.Exit : MenuResult.Continue;
			default:
				return MenuResult.Continue;
		}
	}

	public static bool ExitMenu()
	{
		Console.Clear();
		return VisualYesNoMenu("Are you sure you want to exit?");
	}
}