namespace Translate;

internal static class Logger
{
	public const string LogPath = "error.log";

	private static readonly object Gate = new();

	public static void Log(string context, Exception ex)
	{
		try
		{
			lock (Gate)
			{
				File.AppendAllText(LogPath,
								   $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
			}
		}
		catch (Exception logEx) when (logEx is IOException or UnauthorizedAccessException)
		{
			System.Diagnostics.Debug.WriteLine($"Could not write {LogPath}: {logEx.Message}");
		}
	}
}