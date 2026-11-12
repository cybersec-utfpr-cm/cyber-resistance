using System;
using System.Collections.Generic;
using System.Text;
using Godot;

/// Gerador de layout de Palavras Cruzadas (Crossword) em C#,
/// inspirado em Rocket-007/Crossword-Layout-Generator-in-GDScript-better-verson-.
/// Recebe uma lista de palavras/dicas de cibersegurança e monta uma grade
/// interconectada (Horizontal / Vertical), retornando a matriz recortada e
/// os metadados de cada palavra posicionada.
public class CrosswordGenerator
{
	public class PlacedWord
	{
		public int OrigIndex;
		public int Number;
		public string Word = "";
		public string Prompt = "";
		public bool Placed;
		public int Row;
		public int Col;
		public bool Vertical;
	}

	public class CrosswordLayout
	{
		public int Rows = 1;
		public int Cols = 1;
		public List<PlacedWord> Words = new();
	}

	public int Cols { get; private set; }
	public int Rows { get; private set; }
	private string[,] _grid;
	private readonly List<PlacedWord> _placedWords = new();

	public CrosswordGenerator(int cols = 28, int rows = 28)
	{
		Cols = cols;
		Rows = rows;
		ClearGrid();
	}

	public void ClearGrid()
	{
		_grid = new string[Rows, Cols];
		for (int r = 0; r < Rows; r++)
		{
			for (int c = 0; c < Cols; c++)
			{
				_grid[r, c] = "";
			}
		}
		_placedWords.Clear();
	}

	public static string NormalizeWord(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return "";
		}
		string s = raw.Trim().ToUpperInvariant()
			.Replace("Á", "A").Replace("À", "A").Replace("Â", "A").Replace("Ã", "A")
			.Replace("É", "E").Replace("Ê", "E")
			.Replace("Í", "I")
			.Replace("Ó", "O").Replace("Ô", "O").Replace("Õ", "O")
			.Replace("Ú", "U").Replace("Ü", "U")
			.Replace("Ç", "C");

		var sb = new StringBuilder(s.Length);
		foreach (char ch in s)
		{
			if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))
			{
				sb.Append(ch);
			}
		}
		return sb.ToString();
	}

	public CrosswordLayout GenerateFromQuestions(Godot.Collections.Array questions)
	{
		ClearGrid();
		var entries = new List<PlacedWord>();
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
			string cleanWord = NormalizeWord(rawWord);
			if (cleanWord.Length == 0)
			{
				continue;
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

		// Ordena por tamanho decrescente (determinístico) para posicionar palavras longas primeiro.
		var sorted = new List<PlacedWord>(entries);
		sorted.Sort((a, b) =>
		{
			if (a.Word.Length != b.Word.Length)
			{
				return b.Word.Length.CompareTo(a.Word.Length);
			}
			return a.OrigIndex.CompareTo(b.OrigIndex);
		});

		var unplaced = new List<PlacedWord>();
		foreach (PlacedWord item in sorted)
		{
			if (_placedWords.Count == 0)
			{
				int startR = Rows / 2;
				int startC = Math.Max(1, (Cols - item.Word.Length) / 2);
				PlaceWord(item, startR, startC, false);
			}
			else if (!TryPlaceBestIntersection(item))
			{
				unplaced.Add(item);
			}
		}

		// Segunda passagem para palavras que não cruzaram na primeira tentativa.
		var stillUnplaced = new List<PlacedWord>();
		foreach (PlacedWord item in unplaced)
		{
			if (!TryPlaceBestIntersection(item))
			{
				stillUnplaced.Add(item);
			}
		}

		// Fallback determinístico caso alguma palavra não tenha interseção.
		foreach (PlacedWord item in stillUnplaced)
		{
			PlaceFallback(item);
		}

		return CropAndBuildResult();
	}

	private bool TryPlaceBestIntersection(PlacedWord item)
	{
		string word = item.Word;
		int bestScore = -1;
		int bestR = -1;
		int bestC = -1;
		bool bestVert = false;
		float centerR = Rows / 2.0f;
		float centerC = Cols / 2.0f;
		float bestDist = float.MaxValue;

		foreach (PlacedWord placed in _placedWords)
		{
			string pWord = placed.Word;
			int pR = placed.Row;
			int pC = placed.Col;
			bool pVert = placed.Vertical;
			bool candidateVert = !pVert;

			for (int i = 0; i < word.Length; i++)
			{
				string ch = word.Substring(i, 1);
				for (int j = 0; j < pWord.Length; j++)
				{
					if (pWord.Substring(j, 1) != ch)
					{
						continue;
					}
					int crossR = pR + (pVert ? j : 0);
					int crossC = pC + (pVert ? 0 : j);
					int startR = crossR - (candidateVert ? i : 0);
					int startC = crossC - (candidateVert ? 0 : i);

					int score = CheckFitScore(word, startR, startC, candidateVert);
					if (score > 0)
					{
						float midR = startR + (candidateVert ? word.Length / 2.0f : 0.0f);
						float midC = startC + (candidateVert ? 0.0f : word.Length / 2.0f);
						float dist = Math.Abs(midR - centerR) + Math.Abs(midC - centerC);
						if (score > bestScore || (score == bestScore && dist < bestDist))
						{
							bestScore = score;
							bestR = startR;
							bestC = startC;
							bestVert = candidateVert;
							bestDist = dist;
						}
					}
				}
			}
		}

		if (bestScore > 0)
		{
			PlaceWord(item, bestR, bestC, bestVert);
			return true;
		}
		return false;
	}

	public int CheckFitScore(string word, int row, int col, bool vertical)
	{
		int length = word.Length;
		if (row < 0 || col < 0)
		{
			return 0;
		}
		if (vertical && row + length > Rows)
		{
			return 0;
		}
		if (!vertical && col + length > Cols)
		{
			return 0;
		}

		int beforeR = row - (vertical ? 1 : 0);
		int beforeC = col - (vertical ? 0 : 1);
		if (InBounds(beforeR, beforeC) && _grid[beforeR, beforeC].Length > 0)
		{
			return 0;
		}

		int afterR = row + (vertical ? length : 0);
		int afterC = col + (vertical ? 0 : length);
		if (InBounds(afterR, afterC) && _grid[afterR, afterC].Length > 0)
		{
			return 0;
		}

		int score = 1;
		for (int i = 0; i < length; i++)
		{
			int r = row + (vertical ? i : 0);
			int c = col + (vertical ? 0 : i);
			string ch = word.Substring(i, 1);
			string currentCell = _grid[r, c];

			if (currentCell.Length > 0 && currentCell != ch)
			{
				return 0;
			}

			if (currentCell == ch)
			{
				foreach (PlacedWord placed in _placedWords)
				{
					if (placed.Vertical == vertical)
					{
						int pr = placed.Row;
						int pc = placed.Col;
						int plen = placed.Word.Length;
						if (vertical && pc == c && r >= pr && r < pr + plen)
						{
							return 0;
						}
						if (!vertical && pr == r && c >= pc && c < pc + plen)
						{
							return 0;
						}
					}
				}
				score += 1;
			}
			else
			{
				if (vertical)
				{
					if (InBounds(r, c - 1) && _grid[r, c - 1].Length > 0)
					{
						return 0;
					}
					if (InBounds(r, c + 1) && _grid[r, c + 1].Length > 0)
					{
						return 0;
					}
				}
				else
				{
					if (InBounds(r - 1, c) && _grid[r - 1, c].Length > 0)
					{
						return 0;
					}
					if (InBounds(r + 1, c) && _grid[r + 1, c].Length > 0)
					{
						return 0;
					}
				}
			}
		}

		return score;
	}

	private void PlaceWord(PlacedWord item, int row, int col, bool vertical)
	{
		string word = item.Word;
		for (int i = 0; i < word.Length; i++)
		{
			int r = row + (vertical ? i : 0);
			int c = col + (vertical ? 0 : i);
			_grid[r, c] = word.Substring(i, 1);
		}
		item.Row = row;
		item.Col = col;
		item.Vertical = vertical;
		item.Placed = true;
		_placedWords.Add(item);
	}

	private void PlaceFallback(PlacedWord item)
	{
		string word = item.Word;
		for (int r = 1; r < Rows - 1; r++)
		{
			int startC = Math.Max(1, (Cols - word.Length) / 2);
			if (CheckFitScore(word, r, startC, false) > 0)
			{
				PlaceWord(item, r, startC, false);
				return;
			}
		}
	}

	private CrosswordLayout CropAndBuildResult()
	{
		var layout = new CrosswordLayout();
		if (_placedWords.Count == 0)
		{
			return layout;
		}

		int minR = Rows;
		int maxR = 0;
		int minC = Cols;
		int maxC = 0;

		foreach (PlacedWord item in _placedWords)
		{
			int r = item.Row;
			int c = item.Col;
			int len = item.Word.Length;
			int endR = r + (item.Vertical ? len - 1 : 0);
			int endC = c + (item.Vertical ? 0 : len - 1);
			minR = Math.Min(minR, r);
			maxR = Math.Max(maxR, endR);
			minC = Math.Min(minC, c);
			maxC = Math.Max(maxC, endC);
		}

		layout.Rows = maxR - minR + 1;
		layout.Cols = maxC - minC + 1;

		var ordered = new List<PlacedWord>(_placedWords);
		ordered.Sort((a, b) => a.OrigIndex.CompareTo(b.OrigIndex));
		foreach (PlacedWord item in ordered)
		{
			item.Row -= minR;
			item.Col -= minC;
		}
		layout.Words = ordered;
		return layout;
	}

	private bool InBounds(int r, int c) => r >= 0 && r < Rows && c >= 0 && c < Cols;
}
