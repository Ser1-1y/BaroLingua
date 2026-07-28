using System.Text;

namespace Translate;

public static class Tui
{
    public enum BoolAction
    {
        True,
        False,
    }
    
    public static T? Choice<T>(Option<T>[] options, int selected = 0, string title = "") 
        where T : struct, Enum
    {
        Console.CursorVisible = false;

        while (true)
        {
            DrawChoiceMenu(options, selected, title);

            var key = Console.ReadKey(true).Key;

            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (key)
            {
                case ConsoleKey.RightArrow:
                case ConsoleKey.UpArrow:
                    selected = (selected - 1 + options.Length) % options.Length;
                    break;

                case ConsoleKey.LeftArrow:
                case ConsoleKey.DownArrow:
                    selected = (selected + 1) % options.Length;
                    break;

                case ConsoleKey.Enter:
                    Console.CursorVisible = true;
                    Console.Clear();
                    return options[selected].Id;
                case ConsoleKey.Escape:
                    return null;
            }
        }
    }

    private static void DrawChoiceMenu<T>(
        Option<T>[] options,
        int selected,
        string title
    ) where T : struct, Enum
    {
        Console.Clear();

        if (!string.IsNullOrWhiteSpace(title))
            Console.WriteLine($"--- {title} ---\n");

        for (var i = 0; i < options.Length; i++)
            DrawSelectableItem(options[i].Title, i == selected);
    }

    private static void DrawSelectableItem(string text, bool selected)
    {
        if (selected)
        {
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            Console.ForegroundColor = ConsoleColor.White;

            Console.WriteLine($"> {text} <");

            Console.ResetColor();
        }
        else
            Console.WriteLine($"  {text}");
    }
    
    public static string Input(string defaultText = "", string? escapeValue = null)
    {
        var buffer = new StringBuilder(defaultText);
        var cursorPos = buffer.Length;

        void RedrawInput()
        {
            Console.CursorVisible = false;
            Console.Write("\r" + buffer + "\e[K");
            Console.SetCursorPosition(cursorPos, Console.CursorTop);
            Console.CursorVisible = true;
        }

        bool IsDelimiter(char c) => 
            char.IsWhiteSpace(c) || c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;

        int GetPrevWordPos(int pos)
        {
            while (pos > 0 && IsDelimiter(buffer[pos - 1])) pos--;
            while (pos > 0 && !IsDelimiter(buffer[pos - 1])) pos--;
            return pos;
        }

        int GetNextWordPos(int pos)
        {
            while (pos < buffer.Length && IsDelimiter(buffer[pos])) pos++;
            while (pos < buffer.Length && !IsDelimiter(buffer[pos])) pos++;
            return pos;
        }

        Console.Write(buffer.ToString());

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            var isCtrl = key.Modifiers.HasFlag(ConsoleModifiers.Control);

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return buffer.ToString();

                case ConsoleKey.Escape:
                    Console.WriteLine();
                    return escapeValue ?? defaultText;

                case ConsoleKey.Backspace when cursorPos > 0:
                    var backTo = isCtrl ? GetPrevWordPos(cursorPos) : cursorPos - 1;
                    buffer.Remove(backTo, cursorPos - backTo);
                    cursorPos = backTo;
                    RedrawInput();
                    break;

                case ConsoleKey.Delete when cursorPos < buffer.Length:
                    var delTo = isCtrl ? GetNextWordPos(cursorPos) : cursorPos + 1;
                    buffer.Remove(cursorPos, delTo - cursorPos);
                    RedrawInput();
                    break;

                case ConsoleKey.LeftArrow when cursorPos > 0:
                    cursorPos = isCtrl ? GetPrevWordPos(cursorPos) : cursorPos - 1;
                    RedrawInput();
                    break;

                case ConsoleKey.RightArrow when cursorPos < buffer.Length:
                    cursorPos = isCtrl ? GetNextWordPos(cursorPos) : cursorPos + 1;
                    RedrawInput();
                    break;

                case ConsoleKey.Home:
                    cursorPos = 0;
                    RedrawInput();
                    break;

                case ConsoleKey.End:
                    cursorPos = buffer.Length;
                    RedrawInput();
                    break;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        buffer.Insert(cursorPos++, key.KeyChar);
                        RedrawInput();
                    }
                    break;
            }
        }
    }
    
    public class Option<T>(string title, T id)
        where T : struct, Enum
    {
        public string Title { get; } = title;
        public T Id { get; } = id;
    }
}