using System.Diagnostics;

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

public static class Bash
{
	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

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
			if (!process.Start())
				return new CommandResult(-1, string.Empty, "Failed to start process.");

			var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
			var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);

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

				try
				{
					if (!process.HasExited)
						process.Kill(true);
				}
				catch
				{
					//
				}

				try
				{
					await process.WaitForExitAsync(CancellationToken.None);
				}
				catch
				{
					//
				}

				string stdout;
				string stderr;

				try
				{
					stdout = await stdoutTask;
				}
				catch
				{
					stdout = string.Empty;
				}

				try
				{
					stderr = await stderrTask;
				}
				catch
				{
					stderr = string.Empty;
				}

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
			try
			{
				if (!process.HasExited)
					process.Kill(true);
			}
			catch
			{
				//
			}

			return new CommandResult(-1, string.Empty, ex.ToString());
		}
	}

	public static bool CommandExists(string command)
	{
		var psi = new ProcessStartInfo
		{
			FileName = "/bin/bash",
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};

		psi.ArgumentList.Add("-c");
		psi.ArgumentList.Add($"command -v '{command.Replace("'", "'\\''")}'");

		try
		{
			using var process = Process.Start(psi);

			if (process == null)
				return false;

			process.WaitForExit();

			return process.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}
}