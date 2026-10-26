using System.Xml;
using Spectre.Console;

namespace Translate;

internal static class Session
{
	private static readonly Lock Gate = new();

	private static string? _filePath;
	private static string? _configPath;
	private static Config? _config;
	private static XmlDocument? _doc;
	private static bool _dirty;

	public static void Begin(string filePath, string configPath, Config config, XmlDocument doc)
	{
		lock (Gate)
		{
			_filePath = filePath;
			_configPath = configPath;
			_config = config;
			_doc = doc;
			_dirty = false;
		}
	}

	public static void Edit(Action edit)
	{
		lock (Gate)
		{
			edit();
			_dirty = true;
		}
	}

	public static void Save()
	{
		lock (Gate)
		{
			if (_doc is null || _filePath is null || _configPath is null || _config is null)
				return;

			if (_dirty)
			{
				Document.Save(_filePath, _doc);
				_dirty = false;
			}

			if (_config.RestartRequested)
				return;

			_config.LastFile = _filePath;
			Config.Write(_configPath, _config);
		}
	}

	public static void End()
	{
		lock (Gate)
		{
			try
			{
				if (_dirty)
					Save();
			}
			catch (Exception ex)
			{
				Logger.Log("Saving - end of session", ex);
				AnsiConsole.MarkupLine($"[Red]Could not save:[/] {Markup.Escape(ex.Message)}");
			}
			finally
			{
				_doc = null;
				_filePath = null;
				_configPath = null;
				_config = null;
				_dirty = false;
			}
		}
	}
}