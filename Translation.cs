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
	public const string UndoCommand = "/undo";

	public const string LiteralPrefix = @"\/";

	internal static void Save(string filePath, XmlDocument xmlDoc) =>
		Helpers.WriteAtomic(filePath, xmlDoc.Save);

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

		var speaker = node.Attributes?["speaker"]?.Value ?? "";

		var header =
			$"{Markup.Escape(prefix.Branch)}[Green]Original:[/] {Markup.Escape(original)} | " +
			$"[Green]Speaker:[/] \"{Markup.Escape(speaker)}\"";

		if (tags != null)
			header += $" [Green]Tags:[/] {Markup.Escape(tags.Value)}";

		AnsiConsole.MarkupLine(header);

		return Tui.Input(prefix.Continuation, escapeValue: ExitCommand);
	}

	public static void PrintAncestorContext(XmlNode node, ConversationTree tree)
	{
		foreach (var ancestor in tree.AncestorsOf(node))
		{
			var prefix = tree.PrefixFor(ancestor);
			var text = ancestor.Attributes?["line"]?.Value ?? "";
			var speaker = ancestor.Attributes?["speaker"]?.Value ?? "";
			AnsiConsole.MarkupLine(
				$"{Markup.Escape(prefix.Branch)}[Green]{Markup.Escape(text)}[/] | " +
				$"[Green]Speaker:[/] \"{Markup.Escape(speaker)}\"");
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
	private const int AutosaveEvery = 10;

	public static TranslationResult Process(string filePath, Config config, string configPath)
	{
		Console.Clear();

		XmlDocument xmlDoc;
		try
		{
			xmlDoc = Document.Load(filePath);
		}
		catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
		{
			Helpers.ShowError($"Loading {filePath}", ex);
			return TranslationResult.BackToMenu;
		}

		config.FirstTimeUsing = false;

		var conversationNodes = xmlDoc.SelectNodes("//Conversation")?.Cast<XmlNode>().ToList();
		if (conversationNodes is null or { Count: 0 })
		{
			Console.WriteLine("[ERROR] No nodes found.");
			config.LastFile = null;
			Config.Write(configPath, config);
			Helpers.Pause();
			return TranslationResult.BackToMenu;
		}

		Session.Begin(filePath, configPath, config, xmlDoc);
		try
		{
			return RunLoop(filePath, configPath, config, xmlDoc, conversationNodes);
		}
		finally
		{
			Session.End();
		}
	}

	private static TranslationResult RunLoop(string filePath, string configPath, Config config,
		XmlDocument xmlDoc, List<XmlNode> conversationNodes)
	{
		var tree = ConversationTree.Build(conversationNodes);
		var startTime = DateTime.Now;
		var translatedLinesCount = 0;
		var linesSinceAutosave = 0;
		var contextShown = false;
		var history = new Stack<HistoryEntry>();

		for (var i = 0; i < conversationNodes.Count; i++)
		{
			var conv = conversationNodes[i];
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
			var originalValue = line!.Value;

			switch (TranslateOneLine(conv, line, tags, config, prefix))
			{
				case LineOutcome.Translated:
					history.Push(new HistoryEntry(i, line, originalValue));
					translatedLinesCount++;

					if (++linesSinceAutosave >= AutosaveEvery)
					{
						Session.Save();
						linesSinceAutosave = 0;
					}

					break;

				case LineOutcome.Skipped:
					break;

				case LineOutcome.Undo:
					if (history.TryPop(out var last))
					{
						var attribute = last.Attribute;
						var restore = last.OriginalValue;
						Session.Edit(() => attribute.Value = restore);
						translatedLinesCount--;

						i = last.Index - 1;
						contextShown = false;
						Console.Clear();
					}
					else
					{
						Console.WriteLine("Nothing to undo.");
						i--; // Same line.
					}

					break;

				case LineOutcome.OpenSettings:
					Session.Save();
					Menu.SettingsMenu(configPath, config);
					return TranslationResult.BackToMenu;

				case LineOutcome.Exit:
					Console.Clear();
					Session.Save();
					GetResults(filePath, xmlDoc, translatedLinesCount, startTime);
					return TranslationResult.ExitRequested;
			}
		}

		Session.Save();
		GetResults(filePath, xmlDoc, translatedLinesCount, startTime);
		return TranslationResult.Completed;
	}

	private static LineOutcome TranslateOneLine(XmlNode conv, XmlAttribute line, XmlAttribute? tags, Config config,
		TreePrefix prefix)
	{
		while (true)
		{
			var input = Document.WriteXmlLine(line.Value, tags, config.ShowOriginalNodes, conv, prefix);

			if (input is null || input.Trim().Equals(Document.ExitCommand, StringComparison.OrdinalIgnoreCase))
			{
				if (Menu.ExitMenu())
					return LineOutcome.Exit;

				Console.Clear();
				continue;
			}

			var command = input.Trim();

			if (command.Equals(Document.SettingsCommand, StringComparison.OrdinalIgnoreCase))
				return LineOutcome.OpenSettings;

			if (command.Equals(Document.UndoCommand, StringComparison.OrdinalIgnoreCase))
				return LineOutcome.Undo;

			if (command.Length == 0)
				return LineOutcome.Skipped;

			// "\/exit"
			if (input.StartsWith(Document.LiteralPrefix, StringComparison.Ordinal))
				input = input[1..];

			var translated = input;
			Session.Edit(() => line.Value = translated);
			return LineOutcome.Translated;
		}
	}

	private static bool ShouldSkipLine(XmlAttribute? line, Config config)
	{
		if (line == null || Count.IsIgnorable(line.Value))
			return true;

		if (!Count.IsTranslated(line.Value))
			return false;

		if (config.ShowLoadedStrings)
			Console.WriteLine($"Skipping: {line.Value}");
		return true;
	}

	private static void GetResults(string filePath, XmlDocument xmlDoc, int translatedLinesCount,
		DateTime startTime)
	{
		var transLines = Count.Translated(xmlDoc);
		var engLines = Count.English(xmlDoc);

		var elapsed = DateTime.Now - startTime;
		var secPerLine = translatedLinesCount > 0 ? Math.Round(elapsed.TotalSeconds / translatedLinesCount, 1) : 0;

		AnsiConsole.MarkupLine($"Saved as [Green]{Markup.Escape(filePath)}[/]");
		AnsiConsole.MarkupLine($"Translated [Green]{translatedLinesCount}[/] lines, [Green]{transLines}[/] in total.");
		AnsiConsole.MarkupLine($"[Green]{engLines}[/] English lines left.");
		AnsiConsole.MarkupLine($"Time: [Green]{elapsed.TotalMinutes:F1}[/] minutes.");
		AnsiConsole.MarkupLine($"≈ [Green]{secPerLine}[/] sec/line.");

		Helpers.Pause();
	}

	private readonly record struct HistoryEntry(int Index, XmlAttribute Attribute, string OriginalValue);

	private enum LineOutcome
	{
		Translated,
		Skipped,
		Undo,
		OpenSettings,
		Exit,
	}
}