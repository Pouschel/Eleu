using System.Text;
using Ts.IO;

namespace Eleu.Puzzles;

class PuzzleParser
{
	InputStatus? status;
	string[] lines;
	int lineNr;
	// value: string or  Func<FieldState,FieldState>
	Dictionary<string, object> defs =>puzzle.Defs;
	int colCount;
	Puzzle puzzle;
	Cat cat => puzzle.Cat;
	PuzzleBundle bundle;
	Random rand = new();

	string? CurLine => lineNr >= lines.Length ? null : lines[lineNr].TrimEnd();
	public PuzzleParser(string txt, InputStatus? status)
	{
		this.bundle = new PuzzleBundle(txt);
		this.puzzle = new(bundle);
		this.status = status;
		this.lines = txt.Split('\n');
	}
	void MoveNext() => lineNr++;
	public PuzzleBundle Parse()
	{
		for (int i = 0; i < 10; i++)
		{
			defs[i.ToString()] = i.ToString();
		}
		defs["."] = "None None";
		while (true)
		{
			var line = CurLine;
			if (line == null) break;
			if (line == ":def") { ParseDefs(); continue; }
			if (line == ":grid") { ParseGrid(); continue; }
			if (line == ":meta") { ParseMeta(); continue; }
			if (line == ":info") { ParseInfo(); continue; }
			if (line.Length == 0) { MoveNext(); continue; }
			throw CreateError("Unbekannte Sektion " + line);
		}
		return bundle;
	}
	private void ParseInfo()
	{
		MoveNext();
		var sb = new StringBuilder();
		for (; ; MoveNext())
		{
			var line = CurLine;
			if (line == null) break;
			if (line.Length > 0 && line[0] == ':') break;
			sb.AppendLine(line);
		}
		puzzle.Description = sb.ToString();
	}
	private void ParseMeta()
	{
		if (puzzle.RowCount == 0)
			throw CreateError("Vor der ':meta'-Sektion muss die ':grid'-Sektion kommen.");
		MoveNext();
		for (; ; MoveNext())
		{
			var line = CurLine;
			if (line == null) break;
			if (line.Length == 0) continue;
			if (line[0] == ':') break;
			var (key, val) = ParseKeyValue(line);
			switch (key)
			{
				case "name": puzzle.Name = val; break;
				case "win":
					puzzle.WinCond = val;
					if (!puzzle.CheckWin().HasValue)
						throw CreateError($"Ungültige Siegbedingung: '{val}'");
					break;
				case "hash": break;
				case "funcs": puzzle.SetFuncs(val); break;
				case "score":
					if (!int.TryParse(val, out var score) || score <= 0)
						throw CreateError("Der Score muss eine positive Zahl sein.");
					puzzle.Complexity = score;
					break;
				default: throw CreateError($"Unsupported key: {key}");
			}
		}
	}

	private void SetGrid(List<List<FieldState>> fields)
	{
		var grid = new FieldState[fields.Count, colCount];
		for (int i = 0; i < fields.Count; i++)
		{
			var row = fields[i];
			for (int j = 0; j < colCount; j++)
			{
				var f = j < row.Count ? row[j] : new();
				grid[i, j] = f;
			}
		}
		puzzle.SetGrid(grid);
	}
	private void ParseGrid()
	{
		puzzle = puzzle.Copy(); bundle.AddPuzzle(puzzle);
		List<List<FieldState>> fields = new List<List<FieldState>>();
		MoveNext();
		cat.Row = -1;
		for (; ; MoveNext())
		{
			var line = CurLine;
			if (line == null) break;
			if (line.Length > 0 && line[0] == ':') break;
			var list = ParseFieldLine(fields.Count, line);
			colCount = Math.Max(colCount, list.Count);
			fields.Add(list);
		}
		if (cat.Row < 0) throw CreateError("Das Feld muss eine Katze enthalten.");
		SetGrid(fields);
	}
	List<FieldState> ParseFieldLine(int row, string line)
	{
		var result = new List<FieldState>();
		for (int i = 0; i < line.Length; i++)
		{
			var ch = line[i..(i + 1)];
			var fs = new FieldState();
			if (ch != " " && ch != ".")
			{
				if (!defs.TryGetValue(ch, out var o))
					throw CreateError($"Definition für '{ch}' nicht gefunden");
				if (o is string fill)
				{
					if (fill.StartsWith("'")) // field with long string
						fs.SVal = fill[1..];
					else if (fill.StartsWith("Cat"))
					{
						if (cat.Row >= 0) throw CreateError("Feld hat schon eine Katze");
						cat.Row = row; cat.Col = i;
						var catDir = fill[3..];
						var ctDir = Directions.E;
						if (catDir.Length > 0 && !Enum.TryParse(catDir, true, out ctDir))
							throw CreateError($"Ungültige Blickrichtung für Katze: '{catDir}'");
						cat.LookAt = ctDir;
					}
					else
					{
						ParseItem(ref fs, fill);
					}
				}
				else if (o is Func<FieldState, FieldState> fieldAction)
				{
					fs = fieldAction(fs);
				}
				else throw CreateError("Can't interpret: " + ch);
			}
			fs.FixColor();
			result.Add(fs);
		}
		return result;
	}
	public static FieldState ParseItem( string s)
	{
		var fs = new FieldState();
		ParseItem(ref fs, s);
		return fs;
	}

	static void ParseItem(ref FieldState fs, string s)
	{
		var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		foreach (var item in parts)
		{
  		if (int.TryParse(item, out var n))
			{
				fs.SVal = n.ToString(); continue;
			}
			if (!SetItem(ref fs, item))
				fs.SVal = item;
		}
	}
	static bool SetItem(ref FieldState fs, string item)
	{
		if (Enum.TryParse<ShapeColors>(item, out var col))
		{
			fs.Color = col; return true;
		}
		if (Enum.TryParse<FieldShapes>(item, out var shape))
		{
			fs.Shape = shape; return true;
		}
		if (Enum.TryParse<FieldObjects>(item, out var obj))
		{
			fs.Object = obj; return true;
		}
		return false;
	}
	private void ParseDefs()
	{
		MoveNext();
		for (; ; MoveNext())
		{
			var line = CurLine;
			if (line == null) break;
			if (line.Length == 0) continue;
			if (line[0] == ':') break;
			var (key, val) = ParseKeyValue(line);
			if (key == "seed")
			{
				if (!int.TryParse(val, out var ival))
					throw CreateError("Ungültiger seed-Wert: " + val);
				rand = new Random(ival);
				continue;
			}
			if (val.StartsWith("rnd "))
			{
				try
				{
					var parts = val.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					int s1 = int.Parse(parts[1]);
					int s2 = int.Parse(parts[2]);

					FieldState SetRandNumber(FieldState cell, int a, int b)
					{
						cell.SVal = (a + rand.Next(b - a + 1)).ToString();
						return cell;
					}
					Func<FieldState, FieldState> action = cell => SetRandNumber(cell, s1, s2);
					defs[key] = action;
					continue;
				}
				catch (Exception)
				{
					throw CreateError("Ungültige rnd-Zeile: " + val);
				}
			}
			defs[key] = val;
		}
	}
	private (string, string) ParseKeyValue(string line)
	{
		int idx = line.IndexOf('=');
		if (idx < 0) throw CreateError("Key = Value erwartet");
		string key = line[0..idx].Trim();
		string val = line[(idx + 1)..].Trim();
		if (string.IsNullOrEmpty(key)) throw CreateError("Ein leerer Schlüsselwert ist nicht erlaubt.");
		return (key, val);
	}
	PuzzleParseException CreateError(string msg)
	{
		InputStatus? stat = null;
		if (status.HasValue)
		{
			var ss = status.Value;
			var shiftedLine = ss.LineStart + lineNr;

			stat = new InputStatus(ss.FileName)
			{
				LineStart = shiftedLine,
				LineEnd = shiftedLine,
				ColStart = 1,
				ColEnd = lines![lineNr].Length,
			};
		}
		return new PuzzleParseException(stat, "Fehler beim Einlesen des Puzzles: " + msg);
	}

}
