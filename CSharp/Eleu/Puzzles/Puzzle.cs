using System.Text;

namespace Eleu.Puzzles;
public class PuzzCode
{
  public readonly string Text;
  public readonly string Compressed = "";
  public readonly int TestIndex;

  public PuzzCode(string text, int testIndex)
  {
    this.Text = text; this.TestIndex = testIndex;
    Compressed = PuzzleStatics.EncodePuzzle(text);
  }
}


public class Puzzle
{
	private FieldState[,] Grid;
	private string[] funcs = Array.Empty<string>();
	/// <summary>
	/// Index in bundle.
	/// </summary>
	public int BundleIndex = 0;
	public int RowCount => Grid.GetLength(0);
	public int ColCount => Grid.GetLength(1);
	public Cat Cat = new();
	public int EnergyUsed = 0;
	public int ReadCount = 0;
	public string Name = "";
	public string Description = "";
	public string WinCond = "";
	public int Complexity { get; internal set; } = 1000;
	public string ImageNameHint { get; internal set; } = "";
	public PuzzleBundle Bundle { get; init; }
	internal Dictionary<string, object> Defs;
	internal Puzzle(PuzzleBundle bundle)
	{
		this.Bundle = bundle;
		this.Grid = new FieldState[0, 0];
		Defs = new();
	}
	public FieldState this[int row, int col]
	{
		get
		{
			if (row < 0 || row >= RowCount || col < 0 || col >= ColCount) return FieldState.RedWall;
			return Grid[row, col];
		}
		set
		{
			Grid[row, col] = value;
		}
	}
	public ref FieldState GetRefAt(int row, int col) => ref Grid[row, col];
	public void SetFuncs(string s) => funcs = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
	internal void SetGrid(FieldState[,] grid) => this.Grid = grid;
	public bool IsFuncAllowed(string funcName)
	{
		if (funcs.Length == 0) return true;
		if (funcName.Length > 0 && funcName[0] == '_') return true;
		return Array.IndexOf(funcs, funcName) >= 0;
	}
	public string GetAllowedFuncString(string seperator) => string.Join(seperator, funcs);
	public static void AddRange<T>(ICollection<T> set, IEnumerable<T> values)
	{
		if (values == null)
			return;
		foreach (var v in values)
		{
			set.Add(v);
		}
	}
	public Puzzle Copy()
	{
		var copy = (Puzzle)this.MemberwiseClone();
		copy.Grid = (FieldState[,])this.Grid.Clone();
		copy.Cat = this.Cat.Copy();
		copy.Defs = new();
		AddRange(copy.Defs, this.Defs);
		return copy;
	}
	public bool IsCatAt(int x, int y) => Cat.Row == y && Cat.Col == x;
	public FieldState FieldInFrontOfCat
	{
		get
		{
			var (x, y) = Cat.FieldInFront;
			return this[y, x];
		}
	}
	internal void CalcSignature(StringBuilder sb)
	{
		sb.Append(GetAllowedFuncString("-"));
		sb.Append(Cat.ToString());
		sb.Append(WinCond);
		if (Complexity != 1000) sb.Append(Complexity);
		sb.Append(RowCount); sb.Append(ColCount);
		for (int iy = 0; iy < RowCount; iy++)
		{
			for (int ix = 0; ix < ColCount; ix++)
			{
				var cell = this[iy, ix];
				sb.Append(cell.ToString());
			}
		}
	}
	bool? CheckSingleCondition(string wc)
	{
		var args = wc.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (args.Length == 0) return true;
		try
		{
			switch (args[0])
			{
				case "True": return true;
				case "CatAt":
					{
						int x = int.Parse(args[1]);
						int y = int.Parse(args[2]);
						return IsCatAt(x, y);
					}
				case "Val":
					{
						int x = int.Parse(args[1]);
						int y = int.Parse(args[2]);
						var cell = this[y, x];
						var cmp = args[3] ?? "";
						if (cmp.StartsWith("'"))
						{
							Defs.TryGetValue(cmp[1..], out var o);
							cmp = o?.ToString() ?? "";
							if (cmp.StartsWith("'"))
								cmp = cmp[1..];
						}
						return cell.SVal == cmp;
					}
				case "MiceInBowls":
					return CheckAllCells(cell => cell.Object != FieldObjects.Mouse && cell.Object != FieldObjects.Bowl);
				case "Pat": //Pattern
					{
						int x = int.Parse(args[1]);
						int y = int.Parse(args[2]);
						int nx = int.Parse(args[3]);
						int ny = int.Parse(args[4]);
						var s = args[5];
						for (int iy = 0; iy < ny; iy++)
						{
							for (int ix = 0; ix < nx; ix++)
							{
								char c = s[iy * nx + ix];
								var fs = PuzzleParser.ParseItem((string)Defs[c.ToString()]);
								var gval = Grid[y + iy, x + ix];
								if (fs.Color != gval.Color || fs.Shape != gval.Shape || gval.SVal != fs.SVal) return false;
							}
						}
						return true;
					}
				case "CC":
				case "ColorCount":// Farbenanzahl
					{
						var col = Enum.Parse<ShapeColors>(args[1], true);
						var shape = Enum.Parse<FieldShapes>(args[2], true);
						int count = int.Parse(args[3]);
						int sum = 0;
						CheckAllCells(cell => { if (cell.Color == col && cell.Shape == shape) sum++; return true; });
						return sum == count;
					}
				case "Nums": // Zahlenbelegung
					{
						int x = 0, y = 0;
						for (int i = 1; i < args.Length; i += 3)
						{
							EvalNumString(args[i], ref x);
							EvalNumString(args[i + 1], ref y);
							int n = int.Parse(args[i + 2]);
							var cell = this[y, x];
							if (cell.Num != n)
								return false;
						}
						return true;
					}
				case "Max":
					{
						int max = int.MinValue;
						return CheckCellMath(args, n => max = Math.Max(n, max), () => max);
					}
				case "Sum":
					{
						int sum = 0;
						return CheckCellMath(args, n => sum += n, () => sum);
					}
				case "MinReadCount": // Minimale Anzahl von Leseoperationen
					int mrc = int.Parse(args[1]);
					return this.ReadCount >= mrc;
			}
		}
		catch (Exception)
		{ }
		return null;
	}

	bool CheckCellMath(string[] args, Action<int> numberAction, Func<int> resultFunc)
	{
		int idx = 1;
		int c0 = int.Parse(args[idx++]);
		int r0 = int.Parse(args[idx++]);
		int c1 = int.Parse(args[idx++]);
		int r1 = int.Parse(args[idx++]);
		int dx = int.Parse(args[idx++]);
		int dy = int.Parse(args[idx++]);
		if (this[dy, dx].Num is not int resval)
			return false;

		for (int iy = r0; iy <= r1; iy++)
		{
			for (int ix = c0; ix <= c1; ix++)
			{
				var num = this[iy, ix].Num;
				if (!num.HasValue) return false;
				numberAction(num.Value);
			}
		}
		return resval == resultFunc();
	}

	static void EvalNumString(string s, ref int val)
	{
		if (s == ".") return;
		if (s == "+") { val++; return; }
		if (s == "-") { val--; return; }
		val = int.Parse(s);
	}

	private bool CheckAllCells(Func<FieldState, bool> cellFunc)
	{
		for (int iy = 0; iy < RowCount; iy++)
		{
			for (int ix = 0; ix < ColCount; ix++)
			{
				if (!cellFunc(this[iy, ix])) return false;
			}
		}
		return true;
	}
	public bool? CheckWin()
	{
		if (string.IsNullOrEmpty(WinCond))
			return null;
		var parts = WinCond.Split(',', StringSplitOptions.RemoveEmptyEntries);
		foreach (var wc in parts)
		{
			var b = CheckSingleCondition(wc);
			if (!b.HasValue || b.Value == false) return b;
		}
		return true;
	}
	public bool EqualsContent(Puzzle other)
	{
		if (this.RowCount !=other.RowCount || this.ColCount!=other.ColCount) return false;
		if (!this.Cat.Equals(other.Cat)) return false;
		for (int iy = 0; iy < this.RowCount; iy++)
		{
			for (int ix = 0; ix < this.ColCount; ix++)
			{
				if (!this[iy,ix].Equals(other[iy,ix])) return false;	
			}
		}
		return true;
	}
}
