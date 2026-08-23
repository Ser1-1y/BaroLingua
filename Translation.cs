using System.Text.RegularExpressions;
using System.Xml;
using Spectre.Console;

namespace Translate;

public enum TranslationResult
{
	Completed,
	ExitRequested,
	BackToMenu,
}

internal static class Document
{
	public const string ExitCommand = "/exit";
	public const string SettingsCommand = "/settings";

	internal static void Save(string filePath, string configPath, Config config, XmlDocument xmlDoc)
	{
		config.LastFile = filePath;
		Config.Write(configPath, config);
		xmlDoc.Save(filePath);
	}

	public static XmlDocument Load(string filePath)
	{
		var xmlDoc = new XmlDocument();
		xmlDoc.Load(filePath);
		return xmlDoc;
	}

	private static string BuildTreePrefix(XmlNode node, bool isLast)
	{
		var ancestorChain = new List<XmlNode>();
		var current = node.ParentNode;

		while (current != null && current.NodeType != XmlNodeType.Document)
		{
			if (current.Name == "Conversation")
				ancestorChain.Add(current);
			current = current.ParentNode;
		}

		ancestorChain.Reverse();

		var prefix = ancestorChain.Select(IsLastChildConversation)
								  .Aggregate("", (current1, isLastChild) 
												 => current1 + (isLastChild ? "    " : "│   "));

		prefix += isLast ? "└── " : "├── ";
		return prefix;
	}

	private static bool IsLastChildConversation(XmlNode node)
	{
		var parent = node.ParentNode;
		if (parent == null)
			return true;

		for (var sib = parent.LastChild; sib != null; sib = sib.PreviousSibling)
		{
			if (sib.Name == "Conversation")
				return ReferenceEquals(node, sib);
		}

		return true;
	}

	public static string? WriteXmlLine(string original, XmlAttribute? tags, bool showNodes, XmlNode node,
		bool isLast)
	{
		var prefix = BuildTreePrefix(node, isLast);

		if (showNodes)
		{
			Console.Write($"{prefix}<Conversation line=\"{original}\"");
			Console.WriteLine(tags != null ? $" speakertags=\"{tags.Value}\">" : ">");
		}

		AnsiConsole.Markup(
			$"{prefix}[Green]Original:[/] {original} | [Green]Speaker:[/] \"{node.Attributes?["speaker"]?.Value}\"");
		AnsiConsole.MarkupLine(tags != null ? $" [Green]Tags:[/] {tags.Value}" : "");

		return Tui.Input(prefix, escapeValue: ExitCommand);
	}
}

public static class Translation
{
	private static readonly Regex Cyrillic = Count.CyrillicRegex();
	private static readonly Regex NumbersAndDots = Count.NumbersAndDotsRegex();

	public static TranslationResult Process(string filePath, Config config, string configPath)
	{
		Console.Clear();

		var translatedLinesCount = 0;
		var xmlDoc = Document.Load(filePath);
		config.FirstTimeUsing = false;

		var nodes = xmlDoc.SelectNodes("//Conversation");
		if (nodes is null)
		{
			Console.WriteLine("[ERROR] There are no nodes.");
			config.LastFile = null;
			Config.Write(configPath, config);
			Console.ReadKey();
			return TranslationResult.BackToMenu;
		}

		var startTime = DateTime.Now;

		var lastSiblingMap = new Dictionary<XmlNode, bool>();
		var convNodes = nodes.Cast<XmlNode>().ToList();

		foreach (var conv in convNodes)
		{
			var parent = conv.ParentNode;
			if (parent == null)
			{
				lastSiblingMap[conv] = true;
				continue;
			}

			XmlNode? lastSibling = null;
			for (var sib = parent.LastChild; sib != null; sib = sib.PreviousSibling)
			{
				if (sib.Name != "Conversation") 
					continue;
				
				lastSibling = sib;
				break;
			}

			lastSiblingMap[conv] = ReferenceEquals(conv, lastSibling);
		}

		foreach (var conv in convNodes)
		{
			var line = conv.Attributes?["line"];
			var tags = conv.Attributes?["speakertags"];

			if (ShouldSkipLine(line, config))
				continue;

			while (true)
			{
				var isLast = lastSiblingMap[conv];
				var translatedLine = Document.WriteXmlLine(line!.Value, tags, config.ShowOriginalNodes, conv, isLast);

				if (translatedLine is null or Document.ExitCommand)
				{
					if (!Menu.ExitMenu())
					{
						Console.Clear();
						continue;
					}

					Console.Clear();
					Document.Save(filePath, configPath, config, xmlDoc);
					GetResults(filePath, translatedLinesCount, startTime);
					return TranslationResult.ExitRequested;
				}

				if (translatedLine.Equals(Document.SettingsCommand, StringComparison.OrdinalIgnoreCase))
				{
					Document.Save(filePath, configPath, config, xmlDoc);
					Menu.SettingsMenu(configPath, config);
					return TranslationResult.BackToMenu;
				}

				line.Value = translatedLine;
				translatedLinesCount++;
				break;
			}
		}

		Document.Save(filePath, configPath, config, xmlDoc);
		GetResults(filePath, translatedLinesCount, startTime);
		return TranslationResult.Completed;
	}

	private static bool ShouldSkipLine(XmlAttribute? line, Config config)
	{
		if (line == null || string.IsNullOrWhiteSpace(line.Value) || line.Value == "..." ||
			NumbersAndDots.IsMatch(line.Value))
			return true;

		if (!Cyrillic.IsMatch(line.Value))
			return false;
		if (config.ShowLoadedStrings)
			Console.WriteLine($"Skipping: {line.Value}");
		return true;
	}

	private static void GetResults(string filePath, int translatedLinesCount, DateTime startTime)
	{
		var transLines = Count.Translated(filePath);
		var engLines = Count.English(filePath);

		var elapsed = DateTime.Now - startTime;
		var secPerLine = translatedLinesCount > 0 ? Math.Round(elapsed.TotalSeconds / translatedLinesCount, 1) : 0;

		AnsiConsole.MarkupLine($"Saved as [Green]{filePath}[/]");
		AnsiConsole.MarkupLine($"Translated [Green]{translatedLinesCount}[/] lines, [Green]{transLines}[/] in total.");
		AnsiConsole.MarkupLine($"[Green]{engLines}[/] English lines left.");
		AnsiConsole.MarkupLine($"Time: [Green]{elapsed.TotalMinutes:F1}[/] minutes.");
		AnsiConsole.MarkupLine($"≈ [Green]{secPerLine}[/] sec/line.");

		Console.ReadKey();
	}
}