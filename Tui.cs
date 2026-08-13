using System.Text;
using Spectre.Console;

namespace Translate;

public static class Tui
{
	public static bool ShowMenuNumbers { get; set; } = true;

	public static async Task<T?> ChoiceAsync<T>(Option<T>[] options, string title = "",
		bool titlePadding = false, CancellationToken ct = default)
		where T : struct, Enum
	{
		var stringOptions = options.Select(o => o.Title).ToArray();
		var index = await RunMenuAsync(stringOptions, 0, title, titlePadding, ct);

		return index.HasValue ? options[index.Value].Id : null;
	}

	private static async Task<int?> RunMenuAsync(string[] options, int defaultIndex, string title, bool titlePadding,
		CancellationToken ct)
	{
		if (options.Length == 0 || options.All(string.IsNullOrEmpty))
			return null;

		var selected = Math.Clamp(defaultIndex, 0, options.Length - 1);

		if (string.IsNullOrEmpty(options[selected]))
			selected = GetNextValidIndex(options, selected, 1) ?? 0;

		Console.CursorVisible = false;
		AnsiConsole.Clear();

		var startTop = Console.CursorTop;

		try
		{
			while (true)
			{
				ct.ThrowIfCancellationRequested();
				Console.SetCursorPosition(0, startTop);

				if (!string.IsNullOrWhiteSpace(title))
				{
					var displayTitle = titlePadding ? $"--- {title} ---" : title;
					AnsiConsole.MarkupLine(displayTitle);
				}

				var consoleWidth = Console.IsOutputRedirected ? 80 : Console.WindowWidth;
				var displayNumber = 1;

				for (var i = 0; i < options.Length; i++)
				{
					if (string.IsNullOrEmpty(options[i]))
					{
						Console.WriteLine(new string(' ', Math.Max(0, consoleWidth - 1)));
						continue;
					}

					var prefix = "";
					if (ShowMenuNumbers)
					{
						prefix = displayNumber <= 9
							? $"{displayNumber}. "
							: $"↑{displayNumber - 9}.";
					}

					var itemText = Markup.Escape(options[i]);

					var line = i == selected ? $"[blue] > {prefix}{itemText} [/]" : $"   {prefix}{itemText}";

					var plainTextLength = 3 + prefix.Length + options[i].Length + (i == selected ? 1 : 0);
					var padding = Math.Max(0, consoleWidth - 1 - plainTextLength);

					AnsiConsole.MarkupLine(line + new string(' ', padding));
					displayNumber++;
				}

				while (!Console.KeyAvailable)
				{
					await Task.Delay(20, ct);
					ct.ThrowIfCancellationRequested();
				}

				var key = Console.ReadKey(true);

				if (key.Key == ConsoleKey.Escape
					|| (key.Modifiers.HasFlag(ConsoleModifiers.Control)
						&& key.Key == ConsoleKey.C))
					return null;
				switch (key.Key)
				{
					case ConsoleKey.Enter:
						return selected;
					case ConsoleKey.UpArrow:
					case ConsoleKey.LeftArrow:
						selected = GetNextValidIndex(options, selected, -1) ?? selected;
						break;
					case ConsoleKey.DownArrow:
					case ConsoleKey.RightArrow:
						selected = GetNextValidIndex(options, selected, 1) ?? selected;
						break;
				}

				if (!ShowMenuNumbers)
					continue;
				int? targetDigit = null;

				switch (key.KeyChar)
				{
					case '!': targetDigit = 10; break;
					case '@':
					case '"': targetDigit = 11; break;
					case '#':
					case '№': targetDigit = 12; break;
					case '$':
					case ';': targetDigit = 13; break;
					case '%': targetDigit = 14; break;
					case '^':
					case ':': targetDigit = 15; break;
					case '&':
					case '?': targetDigit = 16; break;
					case '*': targetDigit = 17; break;
					case '(': targetDigit = 18; break;
				}

				if (!targetDigit.HasValue)
				{
					var isShift = (key.Modifiers & ConsoleModifiers.Shift) != 0;

					switch (key.Key)
					{
						case >= ConsoleKey.D1 and <= ConsoleKey.D9:
						{
							targetDigit = key.Key - ConsoleKey.D1 + 1;
							if (isShift) targetDigit += 9;
							break;
						}
						case >= ConsoleKey.NumPad1 and <= ConsoleKey.NumPad9:
						{
							targetDigit = key.Key - ConsoleKey.NumPad1 + 1;
							if (isShift) targetDigit += 9;
							break;
						}
					}
				}

				if (!targetDigit.HasValue)
					continue;
				var targetIndex = GetIndexByDisplayNumber(options, targetDigit.Value);
				if (targetIndex != -1)
					return targetIndex;
			}
		}
		catch (OperationCanceledException)
		{
			return null;
		}
		finally
		{
			Console.CursorVisible = true;
		}
	}

	private static int? GetNextValidIndex(string[] options, int current, int step)
	{
		var next = current + step;

		while (next >= 0 && next < options.Length)
		{
			if (!string.IsNullOrEmpty(options[next]))
				return next;
			next += step;
		}

		if (step > 0)
		{
			for (var i = 0; i < current; i++)
			{
				if (!string.IsNullOrEmpty(options[i]))
					return i;
			}
		}
		else
		{
			for (var i = options.Length - 1; i > current; i--)
			{
				if (!string.IsNullOrEmpty(options[i]))
					return i;
			}
		}

		return null;
	}

	private static int GetIndexByDisplayNumber(string[] options, int targetNumber)
	{
		var currentNumber = 1;
		for (var i = 0; i < options.Length; i++)
		{
			if (string.IsNullOrEmpty(options[i]))
				continue;
			if (currentNumber == targetNumber)
				return i;
			currentNumber++;
		}

		return -1;
	}

	public static string? Input(string title = "", string defaultText = "", string? escapeValue = null)
	{
		var buffer = new StringBuilder(defaultText);
		var cursorPos = buffer.Length;

		void RedrawInput()
		{
			Console.CursorVisible = false;
			Console.Write($"\r{title}{buffer}\e[K");
			Console.SetCursorPosition(title.Length + cursorPos, Console.CursorTop);
			Console.CursorVisible = true;
		}

		RedrawInput();

		while (true)
		{
			var key = Console.ReadKey(true);
			switch (key.Key)
			{
				case ConsoleKey.Enter:
					Console.WriteLine();
					Console.CursorVisible = true;
					return buffer.ToString();

				case ConsoleKey.Escape:
					Console.WriteLine();
					Console.CursorVisible = true;
					return escapeValue;

				case ConsoleKey.C when (key.Modifiers & ConsoleModifiers.Control) != 0:
					Console.WriteLine();
					Console.CursorVisible = true;
					return null;

				case ConsoleKey.Backspace:
					if (cursorPos > 0)
					{
						buffer.Remove(cursorPos - 1, 1);
						cursorPos--;
						RedrawInput();
					}

					break;

				case ConsoleKey.Delete:
					if (cursorPos < buffer.Length)
					{
						buffer.Remove(cursorPos, 1);
						RedrawInput();
					}

					break;

				case ConsoleKey.LeftArrow:
					if (cursorPos > 0)
					{
						cursorPos--;
						RedrawInput();
					}

					break;

				case ConsoleKey.RightArrow:
					if (cursorPos < buffer.Length)
					{
						cursorPos++;
						RedrawInput();
					}

					break;

				case ConsoleKey.Home:
					cursorPos = 0;
					RedrawInput();
					break;

				case ConsoleKey.End:
					cursorPos = buffer.Length;
					RedrawInput();
					break;

				case ConsoleKey.Tab:
					break;

				default:
					if (!char.IsControl(key.KeyChar))
					{
						buffer.Insert(cursorPos, key.KeyChar);
						cursorPos++;
						RedrawInput();
					}

					break;
			}
		}
	}
}

public record Option<T>(string Title, T Id) where T
	: struct, Enum;
