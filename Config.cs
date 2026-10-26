using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

namespace Translate;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Config))]
internal partial class ConfigJsonContext : JsonSerializerContext;

public class Config
{
	public const string CurrentVersion = "3.0.0.0";

	public bool FirstTimeUsing { get; set; } = true;
	public bool ShowLoadedStrings { get; set; }
	public string? LastFile { get; set; }
	public bool DebugMode { get; set; }
	public bool ShowOriginalNodes { get; set; }
	public string? SearchDirectory { get; set; }

	[JsonIgnore]
	public bool RestartRequested { get; set; }

	public Config Clone() => (Config)MemberwiseClone();

	public void CopyFrom(Config other)
	{
		FirstTimeUsing = other.FirstTimeUsing;
		ShowLoadedStrings = other.ShowLoadedStrings;
		LastFile = other.LastFile;
		DebugMode = other.DebugMode;
		ShowOriginalNodes = other.ShowOriginalNodes;
		SearchDirectory = other.SearchDirectory;
	}

	public static Config TryGet()
	{
		const string configPath = Program.ConfigPath;

		if (!File.Exists(configPath))
			return Menu.ConfigMenu(configPath);

		try
		{
			return Read(configPath);
		}
		catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
		{
			Logger.Log("Reading config", ex);
			BackUpCorruptConfig(configPath);
			AnsiConsole.MarkupLine("[Red]Config file is corrupted. Resetting...[/]");
			AnsiConsole.MarkupLine($"[Grey]Old file saved as {Markup.Escape(configPath)}.corrupt[/]");
			Helpers.Pause();
			return new Config();
		}
	}

	private static void BackUpCorruptConfig(string path)
	{
		try
		{
			File.Copy(path, path + ".corrupt", overwrite: true);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Logger.Log("Corrupt config backed up.", ex);
		}
	}

	private static Config Read(string path) =>
		JsonSerializer.Deserialize(File.ReadAllText(path), ConfigJsonContext.Default.Config) ?? new Config();

	public static void Write(string path, Config config) =>
		Helpers.WriteAtomic(path,
			tempPath => File.WriteAllText(tempPath,
				JsonSerializer.Serialize(config, ConfigJsonContext.Default.Config)));
}