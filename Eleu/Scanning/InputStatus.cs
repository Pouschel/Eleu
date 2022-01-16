namespace Eleu.Scanning;

public struct InputStatus
{
	public string FileName = "";

	public int LineStart, LineEnd;

	public int ColStart, ColEnd;

	public InputStatus(string fileName) : this()
	{
		this.LineStart = this.LineEnd = 1;
		this.ColStart = this.ColEnd = 1;
		this.FileName = fileName;
	}

	public bool IsEmpty => LineStart == LineEnd && ColStart == ColEnd;

	internal void NextLine()
	{
		this.LineStart++; this.LineEnd++;
		this.ColStart = this.ColEnd = 1;
	}

	internal void NextChar()
	{
		this.ColStart++; this.ColEnd++;
	}

	public InputStatus Union(in InputStatus other)
	{
		var result = this;
		if (result.FileName != this.FileName)
			return new InputStatus();
		if (other.LineStart < this.LineStart)
		{
			result.LineStart = other.LineStart;
			result.ColStart = other.ColStart;
		}
		else if (other.LineStart == this.LineStart)
			result.ColStart = Math.Min(this.ColStart, other.ColStart);
		if (other.LineEnd > this.LineEnd)
		{
			result.LineEnd = other.LineEnd;
			result.ColEnd = other.ColEnd;
		}
		else if (other.LineEnd == this.LineEnd)
			result.ColEnd = Math.Max(this.ColEnd, other.ColEnd);
		return result;
	}

	public string Message => $"{FileName}({LineStart},{ColStart},{LineEnd},{ColEnd})";

	public override string ToString() => Message;

	public static readonly InputStatus Empty = new ();
	public static InputStatus Parse(string hint)
	{
		int idx = hint.IndexOf("): ");
		if (idx < 0) return Empty;
		int idx0 = hint.LastIndexOf('(', idx);
		if (idx0 < 0) return Empty;
		var fileName = hint[..idx0];
		var numbers = hint[(idx0 + 1)..idx].Split(',', StringSplitOptions.RemoveEmptyEntries)
			.Select(s => int.Parse(s)).ToArray();
		return new InputStatus(fileName)
		{
			LineStart = numbers[0],
			ColStart = numbers[1],
			LineEnd = numbers[2],
			ColEnd = numbers[3]
		};
	}
}
