using System.Diagnostics;
using System.Runtime.CompilerServices;
using Eleu.Puzzles;
using Eleu.Types;
using Ts;
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE1006 // Naming Styles

namespace Eleu.Puzzles;

public class PuzzleFunctions : NativeFunctionBase
{
  class PException : EleuRuntimeError
  {
    public PException(string msg) : base(msg)
    {
    }
  }

  int FrameTime => Vm.FrameTimeMs;

  void Animate(Puzzle puzzle)
  {
    Stopwatch watch = Stopwatch.StartNew();
    puzzle = puzzle.Copy();
    const int steps = 10;
    int aniTime = FrameTime / steps;
    if (FrameTime > 0)
    {
      for (int i = 1; i < steps; i++)
      {
        Vm.NotifyPuzzleChange(puzzle, (float)i / steps);
        int remainingTime = FrameTime - (steps - i) * aniTime - (int)watch.ElapsedMilliseconds;
        if (remainingTime > 5)
          Thread.Sleep(remainingTime);
      }
    }
    Vm.NotifyPuzzleChange(puzzle, 1);
  }
  private Puzzle CheckPuzzleActive([CallerMemberName] string name = "")
  {
    var puz = Vm.Puzzle;
    if (puz == null)
      throw new PException($"Die Funktion '{name}' kann nur bei einem aktivem Puzzle verwendet werden.");
    if (!puz.IsFuncAllowed(name))
      throw new PException($"Die Funktion '{name}' darf bei diesem Puzzle nicht verwendet werden.");
    return puz;
  }
  private Directions CheckDirection(string s, bool onlyNSEW)
  {
    bool b = Enum.TryParse<Directions>(s, true, out var edir);
    if (b && onlyNSEW && edir > Directions.S)
      b = false;
    if (!b)
      throw new PException($"'{s}' ist keine gültige Richtung");
    return edir;
  }
  private object _puzzle(object[] args)
  {
    var s = CheckArgType<string>(0, args);
    int index = 0;
    if (args.Length >= 2)
      index = CheckIntArg(1, args);
    var puzParser = new PuzzleParser(s, Vm.currentStatus);
    var bundle = puzParser.Parse();
    if (index >= bundle.Count || index < 0)
      throw new PException($"Der Test {index + 1} ist nicht vorhanden.");

    var fn = Vm.currentStatus.FileName;
    if (!string.IsNullOrEmpty(fn))
      bundle.SetImageNameHints(fn);
    var puzzle = Vm.Puzzle = bundle[index].Copy();
    Vm.PuzzleSet?.Invoke(bundle.Code);
    Vm.NotifyPuzzleChange(puzzle, -1);
    puzzle.ImageNameHint = "";
    Animate(puzzle);
    return true;
  }
  private object _isSolved(object[] _)
  {
    var puzzle = CheckPuzzleActive();
    bool? b = puzzle.CheckWin();
    if (!b.HasValue) return NilValue;
    return b.Value;
  }
  void CheckObstacle(FieldState field, int row, int col)
  {
    bool obst = false;
    if (field.Object == FieldObjects.Wall)
      obst = true;
    if (obst)
      throw new PException($"Die Katze ist bei den Koordinaten ({col}|{row}) " +
        $"gegen folgendes Hindernis gelaufen: {field.Object.GetObjectName()}."); 
  }
  private void moveDir(Directions dir, int dist, [CallerMemberName] string funcname = "")
  {
    var puzzle = CheckPuzzleActive(funcname);
    var (dx, dy) = dir.GetOffsetForDirection();
    if (dist < 0)
    {
      dx = -dx; dy = -dy; dist = -dist;
    }
    var cat = puzzle.Cat;
    var col = cat.Col;
    var row = cat.Row;
    for (int i = 1; i <= dist; i++)
    {
      col += dx; row += dy;
      var field = puzzle[row, col];
      CheckObstacle(field, row, col);
      cat.Row = row; cat.Col = col;
      Animate(puzzle);
    }
  }
  private object move(object[] args)
  {
    var puzzle = CheckPuzzleActive();
    var edir = puzzle.Cat.LookAt;
    if (args.Length > 0)
    {
      var dir = CheckArgType<string>(0, args);
      edir = CheckDirection(dir, true);
    }
    var (dx, dy) = edir.GetOffsetForDirection();
    var cat = puzzle.Cat;
    var row = cat.Row; var col = cat.Col;
    col += dx; row += dy;
    var field = puzzle[row, col];
    CheckObstacle(field, row, col);
    cat.Row = row; cat.Col = col;
    var e = 0;
    var delta = Math.Abs((int)cat.LookAt - (int)edir);
    switch (delta)
    {
      case 0: e = 1; break;
      case 1:
      case 3: e = 2; break;
      case 2: e = 4; break;
    }
    int add = 0;
    if (cat.Carrying != FieldObjects.None)
      add += delta == 0 ? 1 : 2;
    puzzle.EnergyUsed += e + add;
    Animate(puzzle);
    return NilValue;
  }
  private object push(object[] a)
  {
    CheckArgLen(a, 0);
    var puzzle = CheckPuzzleActive();
    var cat = puzzle.Cat;
    var edir = cat.LookAt;
    var (dx, dy) = edir.GetOffsetForDirection();
    var x = cat.Col + dx; var y = cat.Row + dy;
    var cell = puzzle[y, x];
    if (cell.Object == FieldObjects.None)
      return move(a);
    if (!cell.Object.CanPush())
      throw new PException($"Das Objekt ({cell.Object.GetObjectName()}) kann nicht verschoben werden.");
    int ox = x + dx, oy = y + dy;
    var cellDest = puzzle[oy, ox];
    if (!(cellDest.Object == FieldObjects.None || cellDest.Object == FieldObjects.Bowl))
      throw new PException("Das Objekt kann nur in einen Napf oder auf ein leeres Feld geschoben werden.");
    cellDest.Object = cellDest.Object == FieldObjects.Bowl ? FieldObjects.BowlWithMouse : cell.Object;
    cell.Object = FieldObjects.None;
    cat.Col = x; cat.Row = y;
    puzzle[oy, ox] = cellDest;
    puzzle[y, x] = cell;
    puzzle.EnergyUsed += cat.Carrying == FieldObjects.None ? 6 : 8;
    Animate(puzzle);
    return NilValue;
  }

  private object take(object[] a)
  {
    CheckArgLen(a, 0);
    var puzzle = CheckPuzzleActive();
    var cat = puzzle.Cat;
    var (x, y) = cat.FieldInFront;
    var fstate = puzzle[y, x];
    if (cat.Carrying != FieldObjects.None)
      throw new PException($"Die Katze trägt bereits ein Objekt ({fstate.Object.GetObjectName()}).");
    if (!fstate.Object.CanTake())
      throw new PException($"Die Katze kann das Objekt ({fstate.Object.GetObjectName()}) nicht aufnehmen.");
    cat.Carrying = fstate.Object;
    fstate.Object = FieldObjects.None;
    puzzle[y, x] = fstate;
    puzzle.EnergyUsed += 5;
    Animate(puzzle);
    return NilValue;
  }
  private object drop(object[] a)
  {
    CheckArgLen(a, 0);
    var puzzle = CheckPuzzleActive();
    var cat = puzzle.Cat;
    var (x, y) = cat.FieldInFront;
    var fstate = puzzle[y, x];
    if (cat.Carrying == FieldObjects.None)
      throw new PException($"Die Katze kann nichts ablegen, da sie kein Objekt trägt.");
    if (cat.Carrying != FieldObjects.Mouse)
      throw new PException($"Die Katze sollte ein Maus tragen!");
    switch (fstate.Object)
    {
      case FieldObjects.Bowl:
        fstate.Object = FieldObjects.BowlWithMouse;
        break;
      case FieldObjects.None: fstate.Object = cat.Carrying; break;
      default: throw new PException($"Die Katze kann das Objekt ({cat.Carrying.GetObjectName()}) hier nicht ablegen.");
    }
    cat.Carrying = FieldObjects.None;
    puzzle[y, x] = fstate;
    puzzle.EnergyUsed += 2;
    Animate(puzzle);
    return NilValue;
  }
  private object turn(object[] args)
  {
    var puzzle = CheckPuzzleActive();
    var sdir = CheckArgType<string>(0, args);
    if (!Enum.TryParse<Turns>(sdir, true, out var turnDir) || int.TryParse(sdir, out var _))
      throw new PException($"'{sdir}' ist keine gültige Drehrichtung.");
    var cat = puzzle.Cat;
    int add = (int)turnDir + 1 + (int)cat.LookAt;
    add %= 4;
    cat.LookAt = (Directions)add;
    puzzle.EnergyUsed += cat.Carrying != FieldObjects.None ? 2 : 1;
    Animate(puzzle);
    return cat.LookAt.ToString();
  }

  private void SetColor(Puzzle puzzle, string sdir)
  {
    if (!Enum.TryParse<ShapeColors>(sdir, true, out var color))
      throw new PException($"'{sdir}' ist keine gültige Farbe.");
    var cat = puzzle.Cat;
    ref var cell = ref puzzle.GetRefAt(cat.Row, cat.Col);
    if (cell.Shape == FieldShapes.None && color != ShapeColors.None)
      throw new PException($"Das Feld ({cat.Col}|{cat.Row}) enthält kein Muster.");
    cell.Color = color;
  }

  private void SetShape(Puzzle puzzle, string sShape)
  {
    if (!Enum.TryParse<FieldShapes>(sShape, true, out var shape))
      throw new PException($"'{sShape}' ist kein gültiges Muster");
    var cat = puzzle.Cat;
    ref var cell = ref puzzle.GetRefAt(cat.Row, cat.Col);
    cell.Shape = shape;
    if (shape == FieldShapes.None)
      cell.Color = ShapeColors.None;
  }
  private object paint(object[] args)
  {
    var puzzle = CheckPuzzleActive();
    var sdir = CheckArgType<string>(0, args);
    SetColor(puzzle, sdir);
    int add = puzzle.Cat.Carrying != FieldObjects.None ? 2 : 1;
    puzzle.EnergyUsed += add;
    Animate(puzzle);
    return NilValue;
  }
  private object setShape(object[] args)
  {
    var puzzle = CheckPuzzleActive();
    var sColor = CheckArgType<string>(0, args);
    var sShape = FieldShapes.Square.ToString();
    if (args.Length > 1)
      sShape = CheckArgType<string>(1, args);
    SetShape(puzzle, sShape);
    SetColor(puzzle, sColor);
    int add = puzzle.Cat.Carrying != FieldObjects.None ? 4 : 2;
    puzzle.EnergyUsed += add;
    Animate(puzzle);
    return NilValue;
  }
  private object color(object[] _)
  {
    CheckArgLen(_, 0);
    var puzzle = CheckPuzzleActive();
    var cat = puzzle.Cat;
    ref var cell = ref puzzle.GetRefAt(cat.Row, cat.Col);
    puzzle.EnergyUsed++;
    return cell.Color.ToString();
    //return cell.Color == ShapeColors.None ? NilValue : cell.Color.ToString();
  }

  private object seeing(object[] _)
  {
    CheckArgLen(_, 0);
    var puzzle = CheckPuzzleActive();
    var cell = puzzle.FieldInFrontOfCat;
    puzzle.EnergyUsed++;
    return cell.Object.ToString();
  }
  private object read(object[] _)
  {
    CheckArgLen(_, 0);
    var puzzle = CheckPuzzleActive();
    var cell = puzzle.FieldInFrontOfCat;
    puzzle.EnergyUsed++;
    puzzle.ReadCount++;
    if (cell.Object != FieldObjects.Wall)
    {
      Animate(puzzle);
      return cell.SVal;
    }
    var (x, y) = puzzle.Cat.FieldInFront;
    throw new EleuNativeError($"Das Feld mit den Koordinaten ({x}|{y}) enthält keinen Text.");
  }
  private object readNumber(object[] _)
  {
    CheckArgLen(_, 0);
    var puzzle = CheckPuzzleActive(nameof(read));
    var res = (string)read(_);
    var (x, y) = puzzle.Cat.FieldInFront;
    var num = Number.TryParse(res);
    if (!num.HasValue) throw new EleuNativeError($"Der Inhalt der Feldes ({x}|{y}): '{res}' kann nicht in eine Zahl umgewandelt werden.");
    return num.Value;
  }
  private object write(object[] args)
  {
    CheckArgLen(args, 1);
    var puzzle = CheckPuzzleActive();
    var (x, y) = puzzle.Cat.FieldInFront;
    var cell = puzzle[y, x];
    if (cell.Object == FieldObjects.Wall)
      throw new PException("Mauern können nicht beschriftet werden!");
    ref var wcell = ref puzzle.GetRefAt(y, x);
    string sval = "";
    if (args[0] != NilValue)
      sval = Stringify(args[0]);
    puzzle.EnergyUsed += string.IsNullOrEmpty(sval) ? 1 : 2;
    wcell.SVal = sval;
    Animate(puzzle);
    return NilValue;
  }
  private object _energyUsed(object[] _)
  {
    var puzzle = CheckPuzzleActive();
    return new Number(puzzle.EnergyUsed);
  }
}
