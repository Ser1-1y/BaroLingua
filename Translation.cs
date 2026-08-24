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

	public static string? WriteXmlLine(string original, XmlAttribute? tags, bool showNodes, XmlNode node,
		TreePrefix prefix)
	{
		if (showNodes)
		{
			Console.Write($"{prefix.Branch}<Conversation line=\"{original}\"");
			Console.WriteLine(tags != null ? $" speakertags=\"{tags.Value}\">" : ">");
		}

		AnsiConsole.Markup(
			$"{prefix.Branch}[Green]Original:[/] {original} | [Green]Speaker:[/] \"{node.Attributes?["speaker"]?.Value}\"");
		AnsiConsole.MarkupLine(tags != null ? $" [Green]Tags:[/] {tags.Value}" : "");

		return Tui.Input(prefix.Continuation, escapeValue: ExitCommand);
	}

	public static void PrintAncestorContext(XmlNode node, ConversationTree tree)
	{
		foreach (var ancestor in tree.AncestorsOf(node))
		{
			var prefix = tree.PrefixFor(ancestor);
			var text = ancestor.Attributes?["line"]?.Value ?? "";
			var speaker = ancestor.Attributes?["speaker"]?.Value;
			AnsiConsole.MarkupLine(
				$"{prefix.Branch}[Green]{text.EscapeMarkup()}[/] | [Green]Speaker:[/] \"{speaker}\"");
		}
	}
}

internal readonly record struct TreePrefix(string Branch, string Continuation);

internal sealed class ConversationTree
{
	private readonly Dictionary<XmlNode, IReadOnlyList<XmlNode>> _ancestors = new();
	private readonly Dictionary<XmlNode, TreePrefix> _prefixes = new();

	public static ConversationTree Build(IEnumerable<XmlNode> conversationNodes)
	{
		var tree = new ConversationTree();
		var lastChildByParent = new Dictionary<XmlNode, XmlNode?>();

		foreach (var node in conversationNodes)
		{
			var ancestors = CollectAncestors(node);
			tree._ancestors[node] = ancestors;
			tree._prefixes[node] = BuildPrefix(ancestors, node, lastChildByParent);
		}

		return tree;
	}

	public TreePrefix PrefixFor(XmlNode node) => _prefixes[node];

	public IReadOnlyList<XmlNode> AncestorsOf(XmlNode node) => _ancestors[node];

	private static List<XmlNode> CollectAncestors(XmlNode node)
	{
		var ancestors = new List<XmlNode>();
		for (var current = node.ParentNode;
			 current is { NodeType: not XmlNodeType.Document };
			 current = current.ParentNode)
		{
			if (current.Name == "Conversation")
				ancestors.Add(current);
		}

		ancestors.Reverse();
		return ancestors;
	}

	private static TreePrefix BuildPrefix(IReadOnlyList<XmlNode> ancestors, XmlNode node,
		Dictionary<XmlNode, XmlNode?> lastChildByParent)
	{
		var indent = ancestors.Aggregate("", (acc, ancestor) =>
											 acc + (IsLastConversationSibling(ancestor, lastChildByParent)
												 ? "    "
												 : "│   "));

		var isLast = IsLastConversationSibling(node, lastChildByParent);
		var branch = indent + (isLast ? "└── " : "├── ");
		var continuation = indent + (isLast ? "    " : "│   ");
		return new TreePrefix(branch, continuation);
	}

	private static bool IsLastConversationSibling(XmlNode node, Dictionary<XmlNode, XmlNode?> lastChildByParent)
	{
		var parent = node.ParentNode;
		if (parent == null)
			return true;

		if (lastChildByParent.TryGetValue(parent, out var lastConversationChild))
			return ReferenceEquals(node, lastConversationChild);
		lastConversationChild = null;
		for (var sib = parent.LastChild; sib != null; sib = sib.PreviousSibling)
		{
			if (sib.Name != "Conversation")
				continue;
			lastConversationChild = sib;
			break;
		}

		lastChildByParent[parent] = lastConversationChild;

		return ReferenceEquals(node, lastConversationChild);
	}
}

public static class Translation
{
	private static readonly Regex Cyrillic = Count.CyrillicRegex();
	private static readonly Regex NumbersAndDots = Count.NumbersAndDotsRegex();

	public static TranslationResult Process(string filePath, Config config, string configPath)
	{
		Console.Clear();

		var xmlDoc = Document.Load(filePath);
		config.FirstTimeUsing = false;

		var conversationNodes = xmlDoc.SelectNodes("//Conversation")?.Cast<XmlNode>().ToList();
		if (conversationNodes is null or { Count: 0 })
		{
			Console.WriteLine("[ERROR] There are no nodes.");
			config.LastFile = null;
			Config.Write(configPath, config);
			Console.ReadKey();
			return TranslationResult.BackToMenu;
		}

		var tree = ConversationTree.Build(conversationNodes);
		var startTime = DateTime.Now;
		var translatedLinesCount = 0;
		var contextShown = false;

		foreach (var conv in conversationNodes)
		{
			var line = conv.Attributes?["line"];
			if (ShouldSkipLine(line, config))
				continue;

			if (!contextShown)
			{
				Document.PrintAncestorContext(conv, tree);
				contextShown = true;
			}

			var tags = conv.Attributes?["speakertags"];
			var prefix = tree.PrefixFor(conv);

			switch (TranslateOneLine(conv, line!, tags, config, prefix))
			{
				case LineOutcome.Translated:
					translatedLinesCount++;
					continue;

				case LineOutcome.OpenSettings:
					Document.Save(filePath, configPath, config, xmlDoc);
					Menu.SettingsMenu(configPath, config);
					return TranslationResult.BackToMenu;

				case LineOutcome.Exit:
					Console.Clear();
					Document.Save(filePath, configPath, config, xmlDoc);
					GetResults(filePath, translatedLinesCount, startTime);
					return TranslationResult.ExitRequested;
			}
		}

		Document.Save(filePath, configPath, config, xmlDoc);
		GetResults(filePath, translatedLinesCount, startTime);
		return TranslationResult.Completed;
	}

	private static LineOutcome TranslateOneLine(XmlNode conv, XmlAttribute line, XmlAttribute? tags, Config config,
		TreePrefix prefix)
	{
		while (true)
		{
			var input = Document.WriteXmlLine(line.Value, tags, config.ShowOriginalNodes, conv, prefix);

			if (input is null or Document.ExitCommand)
			{
				if (Menu.ExitMenu())
					return LineOutcome.Exit;

				Console.Clear();
				continue;
			}

			if (input.Equals(Document.SettingsCommand, StringComparison.OrdinalIgnoreCase))
				return LineOutcome.OpenSettings;

			line.Value = input;
			return LineOutcome.Translated;
		}
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

	private enum LineOutcome
	{
		Translated,
		OpenSettings,
		Exit,
	}
}