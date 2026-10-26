using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Translate;

public record Command(
	string FileName,
	string[] Arguments,
	string? WorkingDirectory = null,
	Dictionary<string, string>? EnvironmentVariables = null);

public record CommandResult(int ExitCode, string StandardOutput, string StandardError)
{
	public bool IsSuccess => ExitCode == 0;
}

public static class ProcessRunner
{
	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);
	private static readonly TimeSpan DrainGracePeriod = TimeSpan.FromSeconds(2);

	public static async Task<CommandResult> ExecuteAsync(
		Command command,
		CancellationToken cancellationToken = default)
	{
		using var timeoutCts = new CancellationTokenSource(DefaultTimeout);
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken, timeoutCts.Token);

		var psi = new ProcessStartInfo
		{
			FileName = command.FileName,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			// Windows
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8,
			CreateNoWindow = true,
		};

		foreach (var arg in command.Arguments)
			psi.ArgumentList.Add(arg);

		if (command.EnvironmentVariables != null)
		{
			foreach (var kvp in command.EnvironmentVariables)
				psi.Environment[kvp.Key] = kvp.Value;
		}

		if (command.WorkingDirectory != null)
			psi.WorkingDirectory = command.WorkingDirectory;

		using var process = new Process();
		process.StartInfo = psi;

		try
		{
			try
			{
				if (!process.Start())
					return new CommandResult(-1, string.Empty, "Failed to start process.");
			}
			catch (Win32Exception ex)
			{
				return new CommandResult(-1, string.Empty, $"Failed to start '{command.FileName}': {ex.Message}");
			}

			var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
			var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

			try
			{
				await process.WaitForExitAsync(linkedCts.Token);
			}
			catch (OperationCanceledException)
			{
				var timedOut = timeoutCts.IsCancellationRequested &&
							   !cancellationToken.IsCancellationRequested;

				var errorMessage = timedOut
					? $"Command timed out after {DefaultTimeout.TotalSeconds} seconds."
					: "Command was cancelled.";

				TryKill(process);

				var stdout = await DrainAsync(stdoutTask);
				var stderr = await DrainAsync(stderrTask);

				return new CommandResult(
					-1,
					stdout.Trim(),
					$"{errorMessage}\n{stderr}".Trim());
			}

			var output = await stdoutTask;
			var error = await stderrTask;

			return new CommandResult(
				process.ExitCode,
				output.Trim(),
				error.Trim());
		}
		catch (Exception ex)
		{
			TryKill(process);
			Logger.Log($"Running '{command.FileName}'", ex);
			return new CommandResult(-1, string.Empty, ex.ToString());
		}
	}

	private static void TryKill(Process process)
	{
		try
		{
			if (!process.HasExited)
				process.Kill(true);
		}
		catch (InvalidOperationException)
		{
		}
		catch (Win32Exception ex)
		{
			Logger.Log("Killing process", ex);
		}
	}

	private static async Task<string> DrainAsync(Task<string> readTask)
	{
		var finished = await Task.WhenAny(readTask, Task.Delay(DrainGracePeriod));
		if (finished != readTask)
			return string.Empty;

		try
		{
			return await readTask;
		}
		catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
		{
			Logger.Log("Reading process output", ex);
			return string.Empty;
		}
	}
}