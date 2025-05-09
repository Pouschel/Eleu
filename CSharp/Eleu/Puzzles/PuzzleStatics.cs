using System.Text;
using Ts.IO;

namespace Eleu.Puzzles;

public enum ShapeColors : byte
{
  None,
  Blue,
  Green,
  Red,
  Yellow,
  Cyan,
  Magenta,
  Black,
}

public enum FieldShapes : byte
{
  None,
  Square,
  Circle,
  Diamond,
}

public enum FieldObjects : byte
{
  None,
  Wall,
  Mouse,
  Bowl,
  BowlWithMouse,
}

public enum Directions
{
  E,
  N,
  W,
  S,
}
public enum Turns
{
  Left,
  Around,
  Right,
}
public static class PuzzleStatics
{
  public static string GetObjectName(this FieldObjects fo)
  {
    return fo switch
    {
      FieldObjects.None => "leeres Feld",
      FieldObjects.Bowl => "Napf",
      FieldObjects.Mouse => "Maus",
      FieldObjects.Wall => "Wand",
      FieldObjects.BowlWithMouse => "Napf mit Maus",
      _ => "Unbekanntes Objekt"
    };
  }

  public static (int dx, int dy) GetOffsetForDirection(this Directions dir)
  {
    int dx = 0, dy = 0;
    switch (dir)
    {
      case Directions.E: dx = 1; break;
      case Directions.N: dy = -1; break;
      case Directions.W: dx = -1; break;
      case Directions.S: dy = 1; break;
    }
    return (dx, dy);
  }

  public static bool CanTake(this FieldObjects fo) => fo == FieldObjects.Mouse;

  public static bool CanPush(this FieldObjects fo) => fo == FieldObjects.Mouse;
  const string EncodingStart = ">:)";
  public static PuzzleBundle ParseBundle(string code)
  {
    code = code.Trim();
    if (code.StartsWith(EncodingStart))
    {
      code = code[EncodingStart.Length..];
      code = code.Replace("\r", "");
      code = code.Replace("\n", "");
      code = code.Trim();
      code = FileUtils.DecompressBase64(code);
    }
    var puzParser = new PuzzleParser(code, null);
    return puzParser.Parse();
  }
  public static string EncodePuzzle(string text)
  {
    var s = FileUtils.CompressBase64(text);
    const int split = 44;
    var sb = new StringBuilder();
    s = EncodingStart + s;
    while (s.Length > 0)
    {
      var n = Math.Min(split, s.Length);
      sb.Append(s[..n]);
      s = s[n..];
      while (s.StartsWith('+'))
      {
        sb.Append('+');
        s = s[1..];
      }
      sb.AppendLine();
    }
    return sb.ToString();
  }
}
public struct FieldState : IEquatable<FieldState>
{
  public ShapeColors Color;
  public FieldShapes Shape;
  public FieldObjects Object;
  public string SVal = "";

  public FieldState()
  {
  }
  // Zahl auf dem Feld
  public int? Num
  {
    get
    {
      if (SVal == null) return null;
      if (!int.TryParse(SVal, out int ival))
        return null;
      return ival;
    }
  }
  public void FixColor()
  {
    if (Color == ShapeColors.None) return;
    if (Object != FieldObjects.None || Shape != FieldShapes.None || SVal is not null) return;
    // put a square on the field, so that it ist visible
    Shape = FieldShapes.Square;
  }

  public override string ToString()
  {
    var sb = new StringBuilder();
    if (Object != FieldObjects.None)
      sb.Append(Object);
    if (SVal != null)
    {
      if (sb.Length > 0) sb.Append(", ");
      sb.Append(SVal);
    }
    if (Shape != FieldShapes.None)
    {
      if (sb.Length > 0) sb.Append(", ");
      sb.Append($"{Color} {Shape}");
    }
    if (sb.Length == 0) return "Empty";
    return sb.ToString();
  }

  public bool Equals(FieldState other) =>
    this.Color == other.Color && this.SVal == other.SVal && this.Shape == other.Shape && this.Object == other.Object;

  public static readonly FieldState RedWall = new() { Color = ShapeColors.Red, Object = FieldObjects.Wall };
}

public class Cat : IEquatable<Cat>
{
  public int Row, Col;
  public Directions LookAt = Directions.E;
  public FieldObjects Carrying = FieldObjects.None;

  internal Cat Copy() => (Cat)this.MemberwiseClone();
  public override string ToString() => $"({Col}|{Row})->{LookAt} {Carrying}";
  public bool Equals(Cat? other)
  {
    if (other == null) return false;
    return this.Row == other.Row && this.Col == other.Col && this.Carrying == other.Carrying 
      && this.LookAt == other.LookAt;
  }

  public (int x, int y) FieldInFront
  {
    get
    {
      var (dx, dy) = this.LookAt.GetOffsetForDirection();
      return (this.Col + dx, this.Row + dy);
    }
  }
}

class PuzzleParseException : EleuRuntimeError
{
  public PuzzleParseException(InputStatus? status, string msg) : base(status, msg)
  {
  }
}
