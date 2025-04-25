using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EleuTester.ParseComb;

public struct InputStatus
{
  public static readonly InputStatus Empty = new();
  public string FileName = "";
  public int LineStart, LineEnd;
  public int ColStart, ColEnd;

  public InputStatus(string fileName) : this()
  {
    this.LineStart = this.LineEnd = 1;
    this.ColStart = this.ColEnd = 1;
    this.FileName = fileName;
  }

  public readonly bool IsEmpty => LineStart == LineEnd && ColStart == ColEnd;

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
  public string ReadPartialText()
  {
    if (!File.Exists(FileName)) return "";
    var lines = File.ReadAllLines(FileName);
    var sb = new StringBuilder();
    for (int i = LineStart; i <= LineEnd; i++)
    {
      if (i <= 0 || i > lines.Length) continue;
      var line = lines[i - 1];
      if (LineStart == LineEnd)
        sb.Append(line[(ColStart - 1)..(ColEnd - ColStart + 1)]);
      else if (i == LineStart)
        sb.Append(lines[(ColStart - 1)..]);
      else if (i == LineEnd)
        sb.Append(lines[..(ColEnd - 1)]);
      else
        sb.AppendLine(line);
    }
    return sb.ToString();
  }
  public static (InputStatus, string) ParseWithMessage(string hint)
  {
    try
    {
      int idx = hint.IndexOf("): ");
      if (idx < 0) return (Empty, "");
      int idx0 = hint.LastIndexOf('(', idx);
      if (idx0 < 0) return (Empty, "");
      var fileName = hint[..idx0];
      var numbers = hint[(idx0 + 1)..idx].Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(s => int.Parse(s)).ToArray();
      var msg = hint[(idx + 2)..].Trim();
      return (new InputStatus(fileName)
      {
        LineStart = numbers[0],
        ColStart = numbers[1],
        LineEnd = numbers[2],
        ColEnd = numbers[3]
      }, msg);
    }
    catch (Exception)
    {
      return (Empty, "");
    }

  }
  public static InputStatus Parse(string hint) => ParseWithMessage(hint).Item1;
}

public struct Source
{
  public int Line { get; private set; }
  public int Col { get; private set; }

  public readonly string Text, FileName;
  private int Index;
  public bool AtEnd() => Index >= Text.Length;
  public Source(string text, string fileName = "")
  {
    this.Text = text;
    this.Line = this.Col = 1;
    this.FileName = fileName;
  }

  public string NextText(int maxChars)
  {
    var result = Text[Index..Math.Min(maxChars + Index, Text.Length)];
    if (Text.Length - Index > maxChars) result += "...";
    return result;
  }

  public override string ToString() => $"({Line},{Col}): {NextText(20)}";
  public ParseResult<string>? Match(string s)
  {
    InputStatus tokStat = new(FileName) { ColStart = Col, ColEnd = Col, LineStart = Line, LineEnd = Line };
    for (int i = 0; i < s.Length; i++)
    {
      if (i + Index >= Text.Length) return null;
      char c = Text[i + Index];
      if (s[i] != c) return null;
      if (c != '\n') tokStat.ColEnd++; else { tokStat.NextLine(); }
    }
    var cpy = this; cpy.Line = tokStat.LineEnd; cpy.Col = tokStat.ColEnd;
    cpy.Index += s.Length;
    return new(s, cpy, tokStat);
  }

  public ParseResult<string>? MatchRegex(string s)
  {
    var opst = RegexOptions.Compiled | RegexOptions.Singleline;
    var rex = new Regex("^" + s, opst);
    var match = rex.Match(Text, Index, Text.Length - Index);
    if (!match.Success) return null;
    var val = match.Value;
    var cpy = this;
    for (int i = 0; i < val.Length; i++)
    {
      if (val[i] == '\n') { cpy.Line++; cpy.Col = 1; } else cpy.Col++;
    }
    cpy.Index += val.Length;
    return new(val, cpy);
  }
}

public class ParseResult<T>
{
  public InputStatus Status;
  public readonly T Value;
  public readonly Source Source;

  public ParseResult(T value, Source source) : this(value, source, InputStatus.Empty)
  {
  }

  public ParseResult(T value, Source source, InputStatus status)
  {
    this.Value = value;
    this.Source = source;
  }

  public override string ToString() => $"{Value} | {Source.NextText(15)}";
}


class ParserError : Exception
{
  Source source;
  string msg;
  public ParserError(string msg, Source source)
  {
    this.msg = msg;
    this.source = source;
  }

  public override string Message => $"{source.FileName}({source.Line},{source.Col}): {msg}";
  public override string ToString() => Message;

}
