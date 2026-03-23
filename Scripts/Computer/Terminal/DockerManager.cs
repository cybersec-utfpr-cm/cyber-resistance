using System;
using System.Diagnostics;
using System.Threading.Tasks;

public sealed class DockerManager : IDisposable
{
	private readonly string _containerName;
	private bool _isDisposed;

	public DockerManager(string containerName)
	{
		_containerName = containerName;
	}

	// ========================
	// PUBLIC API
	// ========================

	public async Task StartAsync()
	{
		EnsureNotDisposed();
		await RunDockerCommandAsync($"docker start {_containerName}");
	}

	public async Task StopAsync()
	{
		EnsureNotDisposed();
		await RunDockerCommandAsync($"docker stop {_containerName}");
	}

	public async Task RestartAsync()
	{
		EnsureNotDisposed();
		await RunDockerCommandAsync($"docker restart {_containerName}");
	}

	// ========================
	// CORE
	// ========================

	private async Task RunDockerCommandAsync(string command)
	{
		var psi = new ProcessStartInfo
		{
			FileName = "bash",
			Arguments = $"-c \"{command}\"",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		using var process = new Process { StartInfo = psi };

		process.Start();

		string stdout = await process.StandardOutput.ReadToEndAsync();
		string stderr = await process.StandardError.ReadToEndAsync();

		await process.WaitForExitAsync();

		if (process.ExitCode != 0)
			throw new InvalidOperationException(
				$"Docker command failed: {stderr}"
			);
	}

	private void EnsureNotDisposed()
	{
		if (_isDisposed)
			throw new ObjectDisposedException(nameof(DockerManager));
	}

	public void Dispose()
	{
		if (_isDisposed) return;
		_isDisposed = true;
	}
}
