using System.Text.RegularExpressions;
using System.Xml;

namespace Translate;

public static partial class Count
{
	public static int English(XmlDocument doc) =>
		CountLines(doc, line => !IsIgnorable(line) && !IsTranslated(line));

	public static int Translated(XmlDocument doc) =>
		CountLines(doc, IsTranslated);

	public static bool IsIgnorable(string? line) =>
		string.IsNullOrWhiteSpace(line) || NumbersAndDotsRegex().IsMatch(line);

	public static bool IsTranslated(string? line) =>
		!string.IsNullOrEmpty(line) && CyrillicRegex().IsMatch(line);

	private static int CountLines(XmlDocument doc, Func<string?, bool> predicate)
	{
		var conversations = doc.SelectNodes("//Conversation");
		if (conversations is null)
			return 0;

		return conversations.Cast<XmlNode>()
							.Count(node => predicate(node.Attributes?["line"]?.Value));
	}

	[GeneratedRegex(@"\p{IsCyrillic}")]
	public static partial Regex CyrillicRegex();

	[GeneratedRegex(@"\A[0-9.]+\z")]
	public static partial Regex NumbersAndDotsRegex();
}