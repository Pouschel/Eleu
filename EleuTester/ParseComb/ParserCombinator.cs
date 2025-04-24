using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
  int line, col;
  string Text, FileName;
  private int Index;
  public bool AtEnd() => Index >= Text.Length;
  public Source(string text, string fileName = "")
  {
    this.Text = text;
    this.line = this.col = 1;
    this.FileName = fileName;
  }

  public ParseResult<string>? Match(string s)
  {
    InputStatus tokStat = new(FileName) { ColStart = col, ColEnd = col, LineStart = line, LineEnd = line };
    for (int i = 0; i < s.Length; i++)
    {
      if (i + Index >= Text.Length) return null;
      char c = Text[i + Index];
      if (s[i] != c) return null;
      if (c != '\n') tokStat.ColEnd++; else { tokStat.NextLine(); }
    }
    var cpy = this; cpy.line = tokStat.LineEnd; cpy.col = tokStat.ColEnd;
    cpy.Index += s.Length;
    return new(s, cpy, tokStat);
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
}

public interface IParser<T>
{
  public ParseResult<T>? Parse(Source source);
}

class ParserError : Exception
{

}

public class Parser<T> : IParser<T>
{
  Func<Source, ParseResult<T>?> parse;
  public Parser(Func<Source, ParseResult<T>?> parse)
  {
    this.parse = parse;
  }

  public static Parser<string> Match(string str) => new(source => source.Match(str));

  public static Parser<U> Constant<U>(U value)
  {
    return new Parser<U>(source => new ParseResult<U>(value, source));
  }

  public Parser<T> Or(IParser<T> other)
  {
    return new(source =>
    {
      var res = this.Parse(source);
      if (res != null) return res;
      else return other.Parse(source);
    });
  }

  public T ParseString(string str)
  {
    var source = new Source(str);
    var result = this.Parse(source) ?? throw new ParserError();
    if (!result.Source.AtEnd()) throw new ParserError();

    return result.Value;
  }

  public ParseResult<T>? Parse(Source source) => parse(source);

}
