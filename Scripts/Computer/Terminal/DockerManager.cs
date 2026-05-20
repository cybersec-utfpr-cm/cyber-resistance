using System;
using System.Diagnostics;
using System.Threading.Tasks;

public sealed class DockerManager : IDisposable
{
	private readonly string containerName;
	private bool isDisposed;

	public DockerManager(string containerName)
	{
		this.containerName = containerName;
	}

	public async Task StartAsync()
	{
		EnsureNotDisposed();

		if (await IsRunningAsync())
			return;

		await RunDockerCommandAsync($"docker start {containerName}");
	}

	public async Task StopAsync()
	{
		EnsureNotDisposed();

		if (!await IsRunningAsync())
			return;

		await RunDockerCommandAsync($"docker stop {containerName}");
	}

	public async Task RestartAsync()
	{
		EnsureNotDisposed();
		await RunDockerCommandAsync($"docker restart {containerName}");
	}

	private async Task<bool> IsRunningAsync()
	{
		string output = await RunDockerCommandAsync($"docker inspect -f '{{{{.State.Running}}}}' {containerName}");
		return output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
	}

	private async Task<string> RunDockerCommandAsync(string command)
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
			throw new InvalidOperationException($"Docker command failed: {stderr}");

		return stdout;
	}

	private void EnsureNotDisposed()
	{
		if (isDisposed)
			throw new ObjectDisposedException(nameof(DockerManager));
	}

	public void Dispose()
	{
		if (isDisposed) return;
		isDisposed = true;
	}
}
