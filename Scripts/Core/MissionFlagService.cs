using System;
using System.Collections.Generic;
using System.Security.Cryptography;

public static class MissionFlagService
{
	public const int TokenByteCount = 16;
	private const int TokenCharacterCount = TokenByteCount * 2;
	private const string FileIntroduction =
		"As respostas para a prova de SO só dependem de você...\n" +
		"Continue estudando!!\n\n" +
		"Flag da missão:\n";
	private const string InstallScript =
		"set -eu\n" +
		"umask 077\n" +
		"cat > \"$1\"\n" +
		"chown \"$2:$3\" \"$1\"\n" +
		"chmod \"$4\" \"$1\"";

	public static string GenerateToken()
	{
		byte[] bytes = RandomNumberGenerator.GetBytes(TokenByteCount);
		return Convert.ToHexString(bytes).ToLowerInvariant();
	}

	public static bool IsValidToken(string token)
	{
		if (token == null || token.Length != TokenCharacterCount)
			return false;

		foreach (char character in token)
		{
			bool isDigit = character >= '0' && character <= '9';
			bool isLowerHex = character >= 'a' && character <= 'f';
			bool isUpperHex = character >= 'A' && character <= 'F';

			if (!isDigit && !isLowerHex && !isUpperHex)
				return false;
		}

		return true;
	}

	public static string GetOrCreateToken(
		Dictionary<string, MissionRuntimeSaveData> runtimeStates,
		string questId,
		out bool created
	)
	{
		if (runtimeStates == null)
			throw new ArgumentNullException(nameof(runtimeStates));
		if (string.IsNullOrWhiteSpace(questId))
			throw new ArgumentException("O ID da missão é obrigatório.");

		if (runtimeStates.TryGetValue(questId, out var existingState))
		{
			if (IsValidToken(existingState?.FlagToken))
			{
				created = false;
				return existingState.FlagToken;
			}

			if (!string.IsNullOrEmpty(existingState?.FlagToken))
			{
				throw new InvalidOperationException(
					"Os dados persistentes da missão são inválidos."
				);
			}
		}

		string token = GenerateToken();
		runtimeStates[questId] = new MissionRuntimeSaveData
		{
			FlagToken = token
		};
		created = true;
		return token;
	}

	public static string BuildFileContent(string token)
	{
		if (!IsValidToken(token))
			throw new ArgumentException("A flag da missão é inválida.");

		return FileIntroduction + token + "\n";
	}

	public static IReadOnlyList<string> BuildInstallCommand(
		MissionFlagTarget target
	)
	{
		if (target == null)
			throw new ArgumentNullException(nameof(target));

		return new[]
		{
			"sh",
			"-c",
			InstallScript,
			"mission-flag",
			target.Path,
			target.Owner,
			target.Group,
			target.Mode
		};
	}

	public static bool Matches(string expectedToken, string candidate)
	{
		if (!IsValidToken(expectedToken) || candidate == null)
			return false;

		return string.Equals(
			expectedToken,
			candidate.Trim(),
			StringComparison.OrdinalIgnoreCase
		);
	}
}
