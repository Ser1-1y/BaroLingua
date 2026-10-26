using Spectre.Console;

namespace Translate;

public enum MenuResult
{
	Continue,
	Exit,
}

public enum YesNo
{
	Yes,
	No,
}

public enum OnOff
{
	On,
	Off,
}

public enum Settings
{
	Continue,
	ShowTranslated,
	ChangeFilePath,
	ChangeSearchDirectory,
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
	Search,
	Help,
	Exit,
}

public static class Menu
{
	private static readonly Option<YesNo>[] YesNoChoice =
	[
		new("Yes", YesNo.Yes),
		new("No", YesNo.No),
	];

	private static readonly Option<OnOff>[] OnOffChoice =
	[
		new("On", OnOff.On),
		new("Off", OnOff.Off),
	];

	private static readonly Option<Settings>[] BaseSettingsChoice =
	[
		new("Continue", Settings.Continue),
		new("Show Translated Strings", Settings.ShowTranslated),
		new("Change File Path", Settings.ChangeFilePath),
		new("Change Search Directory", Settings.ChangeSearchDirectory),
		new("Debug Mode", Settings.DebugMode),
		new("Save And Exit", Settings.SaveAndExit),
		new("Exit Without Saving", Settings.ExitWithoutSaving),
	];

	private static bool YesNoMenu(string title, bool defaultYes = true) =>
		Tui.Choice(YesNoChoice, title, defaultYes ? 0 : 1) == YesNo.Yes;

	private static bool OnOffMenu(string title, bool current)
	{
		var choice = Tui.Choice(OnOffChoice, title, current ? 0 : 1);
		return choice is null ? current : choice == OnOff.On;
	}

	public static Config ConfigMenu(string configPath)
	{
		Console.Clear();
		AnsiConsole.MarkupLine("[Red]No config file found[/]");

		var config = new Config();
		if (YesNoMenu("Do you want to change the config now?"))
			SettingsMenu(configPath, config);

		if (!File.Exists(configPath) && !config.RestartRequested)
			Config.Write(configPath, config);

		return config;
	}

	public static void SettingsMenu(string configPath, Config config)
	{
		try
		{
			var draft = config.Clone();

			while (true)
			{
				Console.Clear();

				var choices = new List<Option<Settings>>(BaseSettingsChoice);

				if (draft.DebugMode)
				{
					choices.Add(new Option<Settings>("Delete Config File", Settings.DeleteConfigFile));
					choices.Add(new Option<Settings>("Show Original Nodes", Settings.ShowOriginalNodes));
				}

				var choice = Tui.Choice([.. choices], $"Settings v{Config.CurrentVersion}") ?? Settings.Continue;

				// ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
				switch (choice)
				{
					case Settings.Continue:
					case Settings.SaveAndExit:
						config.CopyFrom(draft);
						Config.Write(configPath, config);
						return;

					case Settings.ShowTranslated:
						draft.ShowLoadedStrings = OnOffMenu(
							$"Show translated strings: {(draft.ShowLoadedStrings ? "[ON]" : "[OFF]")}",
							draft.ShowLoadedStrings);
						break;

					case Settings.ChangeFilePath:
						ChangeFilePath(draft);
						break;

					case Settings.ChangeSearchDirectory:
						ChangeSearchDirectory(draft);
						break;

					case Settings.DebugMode:
						draft.DebugMode = OnOffMenu(
							$"Debug Mode: {(draft.DebugMode ? "[ON]" : "[OFF]")}",
							draft.DebugMode);
						break;

					case Settings.ExitWithoutSaving:
						if (YesNoMenu("Discard your changes and leave settings?"))
							return;
						break;

					case Settings.DeleteConfigFile:
						if (YesNoMenu(
								"Are you sure you want to delete the config file?\nThis will restart the program.",
								defaultYes: false))
						{
							File.Delete(configPath);
							config.RestartRequested = true;
							return;
						}

						break;

					case Settings.ShowOriginalNodes:
						draft.ShowOriginalNodes = OnOffMenu(
							$"Show original nodes: {(draft.ShowOriginalNodes ? "[ON]" : "[OFF]")}",
							draft.ShowOriginalNodes);
						break;
				}
			}
		}
		catch (Exception ex)
		{
			Helpers.ShowError("Settings", ex);
		}
	}
	
	private static void ChangeFilePath(Config config)
	{
		Console.Clear();
		AnsiConsole.WriteLine("Enter path to the XML file:");

		var input = Helpers.StripQuotes(Tui.Input(defaultText: config.LastFile ?? ""));
		if (input.Length > 0)
			config.LastFile = input;
	}

	private static void ChangeSearchDirectory(Config config)
	{
		Console.Clear();
		AnsiConsole.WriteLine("Enter path to the game directory:");

		var input = Helpers.StripQuotes(Tui.Input(defaultText: config.SearchDirectory ?? ""));
		if (input.Length > 0)
			config.SearchDirectory = input;
	}

	public static string? PromptFilePath(string? defaultText = null)
	{
		Console.Clear();
		AnsiConsole.WriteLine("Enter path to the XML file:");
		return Tui.Input(defaultText: defaultText ?? "");
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
			new("Search", StartMenuAction.Search),
			new("Help", StartMenuAction.Help),
			new("Exit", StartMenuAction.Exit),
		};

		var choice = Tui.Choice(startOptions, "Menu");

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
				ChangeFilePath(config);
				Config.Write(configPath, config);
				return MenuResult.Continue;

			case StartMenuAction.Search:
				SearchMenu(configPath, config);
				return MenuResult.Continue;

			case StartMenuAction.Help:
				ShowHelp();
				return MenuResult.Continue;

			case StartMenuAction.Exit:
			case null:
				return YesNoMenu("Are you sure you want to exit?") ? MenuResult.Exit : MenuResult.Continue;
			default:
				return MenuResult.Continue;
		}
	}

	private static void ShowHelp()
	{
		AnsiConsole.Markup($"[Green]Escape[/]      - Save and exit\n" +
						   $"[Green]Ctrl+C[/]      - Save and exit immediately\n" +
						   $"[Green]\"{Document.ExitCommand}\"[/]     - Save and exit\n" +
						   $"[Green]\"{Document.SettingsCommand}\"[/] - Access Settings\n" +
						   $"[Green]\"{Document.UndoCommand}\"[/]     - Undo line\n" +
						   $"[Green]Enter on empty[/] - Skip\n" +
						   $"[Green]\\/text[/]      - Type a literal \"/text\" ");
		Helpers.Pause();
	}

	private static void SearchMenu(string configPath, Config config)
	{
		try
		{
			Console.Clear();

			if (!Search.IsRgAvailable())
			{
				AnsiConsole.MarkupLine("[Red]rg not found in PATH.[/]");
				Helpers.Pause();
				return;
			}

			if (string.IsNullOrWhiteSpace(config.SearchDirectory) || !Directory.Exists(config.SearchDirectory))
			{
				AnsiConsole.MarkupLine("[Yellow]Search directory invalid.[/]");
				var dir = Helpers.StripQuotes(
					Tui.Input("Enter path to the game directory: ", config.SearchDirectory ?? ""));
				if (dir.Length == 0 || !Directory.Exists(dir))
				{
					AnsiConsole.MarkupLine("[Red]Invalid directory.[/]");
					Helpers.Pause();
					return;
				}

				config.SearchDirectory = dir;
				Config.Write(configPath, config);
			}

			Console.Clear();
			var query = Tui.Input("Search query: ");
			if (string.IsNullOrWhiteSpace(query))
				return;

			AnsiConsole.MarkupLine("[Grey]Searching...[/]");
			var outcome = Search.SearchAsync(query, config.SearchDirectory!).GetAwaiter().GetResult();

			Search.DisplayResults(outcome, query);
		}
		catch (Exception ex)
		{
			Helpers.ShowError("Search", ex);
		}
	}

	public static bool ExitMenu()
	{
		Console.Clear();
		return YesNoMenu("Are you sure you want to exit?");
	}
}