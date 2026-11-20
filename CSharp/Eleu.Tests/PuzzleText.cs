using System.Text;
using Eleu.Puzzles;

namespace Eleu.Tests;

/// <summary>A puzzle state as text (Puzzle.Dump writes nothing yet).</summary>
internal static class PuzzleText
{
  /// <summary>The state in one line, and the grid (one line per row).</summary>
  public static (string State, string Grid) Describe(Puzzle p)
  {
    var state = $"'{p.Name}' test {p.BundleIndex + 1}/{p.Bundle.Count}; cat {p.Cat}; energy {p.EnergyUsed}; read {p.ReadCount}";
    var sb = new StringBuilder();
    for (int row = 0; row < p.RowCount; row++)
    {
      if (row > 0) sb.Append('\n');
      sb.Append("  |");
      for (int col = 0; col < p.ColCount; col++)
      {
        var fs = p[row, col];
        sb.Append(' ').Append(fs.Equals(new FieldState()) ? "." : fs.ToString()).Append(" |");
      }
    }
    return (state, sb.ToString());
  }
}
