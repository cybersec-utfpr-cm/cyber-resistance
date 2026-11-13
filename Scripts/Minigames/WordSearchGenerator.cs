using System;
using System.Collections.Generic;
using Godot;

/// Gerador determinístico de Caça-Palavras (Word Search) em C#.
/// Posiciona os termos de cibersegurança na grade (Horizontal, Vertical e Diagonal)
/// e preenche as células restantes com letras A-Z usando semente determinística
/// para que Host e Clientes gerem exatamente o mesmo tabuleiro.
public class WordSearchGenerator
{
	public class PlacedWord
	{
		public int OrigIndex;
		public int Number;
		public string Word = "";
		public string Prompt = "";
		public int StartRow;
		public int StartCol;
		public int Dr;
		public int Dc;
		public string DirectionName = "";
	}

	public class WordSearchBoard
	{
		public int Rows = 12;
		public int Cols = 12;
		public string[,] Grid = new string[12, 12];
		public List<PlacedWord> Words = new();
	}

	private static readonly int[][] Directions =
	{
		new[] { 0, 1 },   // Horizontal (→)
		new[] { 1, 0 },   // Vertical (↓)
		new[] { 1, 1 },   // Diagonal (↘)
		new[] { -1, 1 }   // Diagonal (↗)
	};

	private static readonly string[] DirectionLabels =
	{
		"Horizontal →",
		"Vertical ↓",
		"Diagonal ↘",
		"Diagonal ↗"
	};

	public int Rows { get; private set; }
	public int Cols { get; private set; }

	public WordSearchGenerator(int rows = 12, int cols = 12)
	{
		Rows = rows;
		Cols = cols;
	}

	public WordSearchBoard GenerateFromQuestions(Godot.Collections.Array questions)
	{
		var board = new WordSearchBoard
		{
			Rows = Rows,
			Cols = Cols,
			Grid = new string[Rows, Cols]
		};

		for (int r = 0; r < Rows; r++)
		{
			for (int c = 0; c < Cols; c++)
			{
				board.Grid[r, c] = "";
			}
		}

		var entries = new List<PlacedWord>();
		uint seed = 2166136261u;
		for (int i = 0; i < questions.Count; i++)
		{
			if (questions[i].VariantType != Variant.Type.Dictionary)
			{
				continue;
			}
			Godot.Collections.Dictionary q = questions[i].AsGodotDictionary();
			string rawWord = q.TryGetValue("word", out Variant wVar)
				? wVar.AsString()
				: (q.TryGetValue("answer", out Variant aVar) ? aVar.AsString() : "");
			string cleanWord = CrosswordGenerator.NormalizeWord(rawWord);
			if (cleanWord.Length == 0)
			{
				continue;
			}
			foreach (char ch in cleanWord)
			{
				seed = (seed ^ ch) * 16777619u;
			}
			string prompt = q.TryGetValue("prompt", out Variant pVar) ? pVar.AsString() : "";
			entries.Add(new PlacedWord
			{
				OrigIndex = i,
				Number = i + 1,
				Word = cleanWord,
				Prompt = prompt
			});
		}

		// Ordena por tamanho decrescente para encaixar palavras maiores primeiro.
		var sorted = new List<PlacedWord>(entries);
		sorted.Sort((a, b) =>
		{
			if (a.Word.Length != b.Word.Length)
			{
				return b.Word.Length.CompareTo(a.Word.Length);
			}
			return a.OrigIndex.CompareTo(b.OrigIndex);
		});

		foreach (PlacedWord item in sorted)
		{
			PlaceWordDeterministic(board.Grid, item, ref seed);
		}

		// Reordena pela ordem original das questões (1..N)
		entries.Sort((a, b) => a.OrigIndex.CompareTo(b.OrigIndex));
		board.Words = entries;

		// Preenche células vazias com letras pseudoaleatórias determinísticas
		const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
		for (int r = 0; r < Rows; r++)
		{
			for (int c = 0; c < Cols; c++)
			{
				if (string.IsNullOrEmpty(board.Grid[r, c]))
				{
					int idx = (int)(NextRand(ref seed) % (uint)alphabet.Length);
					board.Grid[r, c] = alphabet.Substring(idx, 1);
				}
			}
		}

		return board;
	}

	private void PlaceWordDeterministic(string[,] grid, PlacedWord item, ref uint seed)
	{
		string word = item.Word;
		int totalCells = Rows * Cols;
		int startDirIdx = (int)(NextRand(ref seed) % (uint)Directions.Length);
		int startCellIdx = (int)(NextRand(ref seed) % (uint)totalCells);

		// Prioriza posições com interseção válida ou células livres
		for (int pass = 0; pass < 2; pass++)
		{
			for (int dOffset = 0; dOffset < Directions.Length; dOffset++)
			{
				int dirIdx = (startDirIdx + dOffset) % Directions.Length;
				int dr = Directions[dirIdx][0];
				int dc = Directions[dirIdx][1];

				for (int cOffset = 0; cOffset < totalCells; cOffset++)
				{
					int cellIdx = (startCellIdx + cOffset * 7) % totalCells;
					int r = cellIdx / Cols;
					int c = cellIdx % Cols;

					int fit = CanPlace(grid, word, r, c, dr, dc);
					if (pass == 0 && fit >= 2)
					{
						CommitWord(grid, item, r, c, dr, dc, DirectionLabels[dirIdx]);
						return;
					}
					if (pass == 1 && fit >= 1)
					{
						CommitWord(grid, item, r, c, dr, dc, DirectionLabels[dirIdx]);
						return;
					}
				}
			}
		}

		// Fallback linear caso a grade esteja muito densa
		int fallbackRow = item.OrigIndex % Rows;
		int maxLen = Math.Min(word.Length, Cols);
		for (int i = 0; i < maxLen; i++)
		{
			grid[fallbackRow, i] = word.Substring(i, 1);
		}
		item.StartRow = fallbackRow;
		item.StartCol = 0;
		item.Dr = 0;
		item.Dc = 1;
		item.DirectionName = DirectionLabels[0];
	}

	private int CanPlace(string[,] grid, string word, int startR, int startC, int dr, int dc)
	{
		int len = word.Length;
		int endR = startR + dr * (len - 1);
		int endC = startC + dc * (len - 1);
		if (endR < 0 || endR >= Rows || endC < 0 || endC >= Cols)
		{
			return 0;
		}

		int score = 1;
		for (int i = 0; i < len; i++)
		{
			int r = startR + dr * i;
			int c = startC + dc * i;
			string ch = word.Substring(i, 1);
			string existing = grid[r, c];
			if (!string.IsNullOrEmpty(existing) && existing != ch)
			{
				return 0;
			}
			if (existing == ch)
			{
				score += 1;
			}
		}
		return score;
	}

	private static void CommitWord(
		string[,] grid,
		PlacedWord item,
		int startR,
		int startC,
		int dr,
		int dc,
		string dirLabel
	)
	{
		string word = item.Word;
		for (int i = 0; i < word.Length; i++)
		{
			int r = startR + dr * i;
			int c = startC + dc * i;
			grid[r, c] = word.Substring(i, 1);
		}
		item.StartRow = startR;
		item.StartCol = startC;
		item.Dr = dr;
		item.Dc = dc;
		item.DirectionName = dirLabel;
	}

	private static uint NextRand(ref uint state)
	{
		state ^= state << 13;
		state ^= state >> 17;
		state ^= state << 5;
		return state;
	}
}
