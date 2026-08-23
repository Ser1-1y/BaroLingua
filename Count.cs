using System.Text.RegularExpressions;
using System.Xml;

namespace Translate;

public static partial class Count
{
	public static int English(string path) => CountLines(path, false);
	public static int Translated(string path) => CountLines(path, true);

	private static int CountLines(string path, bool cyrillic)
	{
		var nodes = Document.Load(path);
		var conversations = nodes.SelectNodes("//Conversation");
		if (conversations is null)
			return 0;

		return conversations.Cast<XmlNode>().Count(node =>
		{
			var line = node.Attributes?["line"]?.Value ?? "";
			return CyrillicRegex().IsMatch(line) == cyrillic;
		});
	}

	[GeneratedRegex(@"\p{IsCyrillic}")]
	public static partial Regex CyrillicRegex();

	[GeneratedRegex("^[0-9.]+$")]
	public static partial Regex NumbersAndDotsRegex();
}