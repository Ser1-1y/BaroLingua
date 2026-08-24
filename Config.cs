using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

namespace Translate;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Config))]
internal partial class ConfigJsonContext : JsonSerializerContext;

public class Config
{
	public string Version { get; set; } = "3.0.0.0";
	public bool FirstTimeUsing { get; set; } = true;
	public bool ShowLoadedStrings { get; set; }
	public string? LastFile { get; set; }
	public bool DebugMode { get; set; }
	public bool ShowOriginalNodes { get; set; }
	public string? SearchDirectory { get; set; }

	public static Config TryGet()
	{
		const string configPath = Program.ConfigPath;

		if (File.Exists(configPath))
		{
			try
			{
				return Read(configPath);
			}
			catch
			{
				AnsiConsole.MarkupLine("[Red]Config file is corrupted. Resetting...[/]");
				return new Config();
			}
		}

		Menu.ConfigMenu(configPath);
		return Read(configPath);
	}

	private static Config Read(string path) =>
		JsonSerializer.Deserialize(File.ReadAllText(path), ConfigJsonContext.Default.Config) ?? new Config();

	public static void Write(string path, Config config) =>
		File.WriteAllText(path, JsonSerializer.Serialize(config, ConfigJsonContext.Default.Config));
}