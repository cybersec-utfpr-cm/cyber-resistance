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

	public async Task EnsureNetworkExistsAsync(string networkName, string driver = "bridge")
	{
		EnsureNotDisposed();

		if (await NetworkExistsAsync(networkName))
			return;

		await RunDockerCommandAsync($"docker network create --driver {driver} {networkName}");
	}

	public async Task ConnectToNetworkAsync(string networkName, string containerName, string alias = "")
	{
		EnsureNotDisposed();

		if (await IsContainerConnectedToNetworkAsync(networkName, containerName))
			return;

		string aliasArg = string.IsNullOrWhiteSpace(alias) ? "" : $"--alias {alias}";
		await RunDockerCommandAsync($"docker network connect {aliasArg} {networkName} {containerName}");
	}

	public async Task EnsureContainerRunningFromImageAsync(
		string containerName,
		string image,
		string hostname,
		string networkName,
		string networkAlias)
	{
		EnsureNotDisposed();

		if (await ContainerExistsAsync(containerName))
		{
			if (!await IsRunningAsync(containerName))
			{
				await RunDockerCommandAsync($"docker rm -f {containerName}");
			}
			else
			{
				if (!await IsContainerConnectedToNetworkAsync(networkName, containerName))
					await ConnectToNetworkAsync(networkName, containerName, networkAlias);

				return;
			}
		}

		string hostnameArg = string.IsNullOrWhiteSpace(hostname) ? "" : $"--hostname {hostname}";
		string aliasArg = string.IsNullOrWhiteSpace(networkAlias) ? "" : $"--network-alias {networkAlias}";

		await RunDockerCommandAsync(
			$"docker run -d --name {containerName} {hostnameArg} --network {networkName} {aliasArg} {image}"
		);
	}

	public async Task StopAndRemoveContainerAsync(string containerName)
	{
		EnsureNotDisposed();

		if (!await ContainerExistsAsync(containerName))
			return;

		await RunDockerCommandAsync($"docker rm -f {containerName}");
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

	private async Task<bool> IsRunningAsync(string name)
	{
		string output = await RunDockerCommandAsync($"docker inspect -f '{{{{.State.Running}}}}' {name}");
		return output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
	}

	private async Task<bool> IsRunningAsync()
	{
		string output = await RunDockerCommandAsync($"docker inspect -f '{{{{.State.Running}}}}' {containerName}");
		return output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
	}

	private async Task<bool> ContainerExistsAsync(string name)
	{
		try
		{
			await RunDockerCommandAsync($"docker inspect {name}");
			return true;
		}
		catch
		{
			return false;
		}
	}

	private async Task<bool> NetworkExistsAsync(string networkName)
	{
		try
		{
			await RunDockerCommandAsync($"docker network inspect {networkName}");
			return true;
		}
		catch
		{
			return false;
		}
	}

	private async Task<bool> IsContainerConnectedToNetworkAsync(string networkName, string containerName)
	{
		try
		{
			string output = await RunDockerCommandAsync(
				$"docker inspect -f '{{{{json .NetworkSettings.Networks}}}}' {containerName}"
			);

			return output.Contains($"\"{networkName}\"");
		}
		catch
		{
			return false;
		}
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
