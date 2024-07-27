using System.Drawing;
using System.Globalization;
using System.Text;
using DomCask;
using static DomCask.Dom;

namespace Eleu.Puzzles;

public class PuzzleHtmlCreator
{
  private readonly StringWriter sw = new();
  private StringWriter swc = new();
  Puzzle? puzz;
  Puzzle puzzle => puzz!;
  HElement canvDiv, canvas, puzzDisplayDiv;
  float border = 28, borderX, borderY;
  int ColCount => puzzle?.ColCount ?? 1;
  int RowCount => puzzle?.RowCount ?? 1;
  int canvasWidth, canvasHeight;
  float cellSize;
  public PuzzleHtmlCreator(Puzzle? puz)
  {
    this.puzz = puz;
    canvDiv = new HElement("puzzCanvasDiv");
    canvas = new HElement("puzzCanvas");
    puzzDisplayDiv = new("puzzDiv");
    if (puzzle == null)
    {
      puzzDisplayDiv.InnerText = "Kein aktives Puzzle.";
      return;
    }
  }
  void SetEmpty()
  {
    if (puzz == null)
    {
      puzzDisplayDiv.InnerText = "";
    }
    canvas.SetProperty("width", "1");
    canvas.SetProperty("height", "1");
  }

  void WriteTag(string tag, string content, params string[] attributes)
  {
    sw.Write($"<{tag}");
    for (int i = 0; i < attributes.Length; i += 2)
    {
      sw.Write($" {attributes[i]}=\"{attributes[i + 1]}\"");
    }
    sw.Write(">");
    sw.Write(content);
    sw.WriteLine($"</{tag}>");
  }
  string canvasPrelude => $@"const canvas = document.getElementById(""{canvas.Id}"");
    const ctx = canvas.getContext(""2d"");";

  (float, float) MeasureString(string font, string text)
  {
    var s = @$"{canvasPrelude}
ctx.font = ""{font}"";
let metrics = ctx.measureText(""{text}"");
metrics.width+""|""+(metrics.actualBoundingBoxAscent + metrics.actualBoundingBoxDescent)";
    s = JsEval(s);
    var parts = s.Split('|');
    return (float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture));
  }

  public void Render()
  {
    SetEmpty();
    if (puzz == null) return;
    WriteTag("div", puzzle.Name, "class", "puzzTitle");
    WriteTag("div", puzzle.Description, "class", "puzzText");
    WriteTag("div", "<b>Erlaubte Funktionen:</b> " + puzzle.GetAllowedFuncString(", "), "class", "puzzText");

    puzzDisplayDiv.InnerHTML = sw.ToString();

    canvasWidth = canvDiv.ClientWidth;
    canvasHeight = canvDiv.ClientHeight;
    //Dom.SetStyle("puzzCanvasDiv", "display", "none");

    canvas.SetProperty("width", canvasWidth.ToString());
    canvas.SetProperty("height", canvasHeight.ToString());
    cellSize = Math.Min(canvasWidth / (ColCount + 1), canvasHeight / (RowCount + 1));
    borderX = (canvasWidth - (ColCount + 1) * cellSize) / 2 + cellSize;
    borderY = (canvasHeight - (RowCount + 1) * cellSize) / 2 + cellSize;
    border = Math.Min(borderX, borderY);

    swc.WriteLine(canvasPrelude);
    swc.WriteLine($"");
    swc.WriteLine(@"const catImg = document.getElementById(""catImg"");
const wallImg = document.getElementById(""wallImg"");
const mouseImg = document.getElementById(""mouseImg"");
const bowlImg = document.getElementById(""bowlImg"");
");
    DrawGrid();
    DrawCoordinateLines();
    DrawCoordinates();
    JsEval(swc.ToString());
    //Dom.SetStyle("puzzCanvasDiv", "display", "block");
  }

  string CreateBrush(FieldState fs)
  {
    if (fs.Color == ShapeColors.None) return "transparent";
    var col = ColorTranslator.FromHtml(fs.Color.ToString());
    var colStr = $"#{col.R:x2}{col.G:x2}{col.B:x2}";

    if (fs.Color != ShapeColors.Black)
      colStr += $"{130:x2}";
    return colStr;
  }
  (float x, float y) GetCellPos(int cx, int cy)
  {
    var x = borderX + cellSize * cx;
    var y = borderY + cellSize * cy;
    return (x, y);
  }
  void DrawGrid()
  {
    if (puzzle == null) return;

    FieldState lastState = new FieldState();
    var lastBrush = CreateBrush(lastState);
    for (int iy = 0; iy < puzzle.RowCount; iy++)
    {
      for (int ix = 0; ix < puzzle.ColCount; ix++)
      {
        var cell = puzzle[iy, ix];
        if (!cell.Equals(lastState))
        {
          lastState = cell;
          lastBrush = CreateBrush(cell);
        }
        DrawCell(lastBrush, iy, ix, cell);
      }
    }
  }
  private static int CountObjects(FieldState cell)
  {
    int n = 0;
    if (cell.Object != FieldObjects.None) n++;
    bool shapeIsOverWritable = cell.Shape == FieldShapes.None
      || (n == 0 && cell.Shape == FieldShapes.Square);
    if (cell.Num.HasValue || !shapeIsOverWritable) n++;
    return n;
  }
  private void DrawCell(string lastBrush, int iy, int ix, FieldState cell)
  {
    var (x, y) = GetCellPos(ix, iy);
    float orgx = x, orgy = y;
    int n = CountObjects(cell);
    bool hasCat = false;

    if (puzzle!.Cat.Row == iy && puzzle.Cat.Col == ix)
    {
      n++; hasCat = true;
    }
    var csize = cellSize;
    if (n > 1) csize /= 2;
    if (hasCat && n > 1)
    {
      x += csize;
    }
    // draw the object
    if (cell.Object != FieldObjects.None)
    {
      switch (cell.Object)
      {
        case FieldObjects.Wall:
          DrawWall(x, y, csize);
          break;
        case FieldObjects.Bowl: DrawBowl(x, y, csize); break;
        case FieldObjects.Mouse:
          DrawMouseMapShifted(x, y, 0.06f, csize);
          break;
        case FieldObjects.BowlWithMouse:
          DrawBowl(x, y, csize); DrawMouseMapShifted(x, y, 0.2f, csize);
          break;
      }
      x += csize;
      if (x > orgx + 0.9f * cellSize)
      {
        x = orgx; y += csize;
      }
    }

    DrawCellShape(cell.Shape, x, y, lastBrush, csize);
    if (hasCat)
    {
      DrawCat(orgx, orgy, csize);
    }
    if (cell.SVal != null)
      DrawCellText(x, y, cell.SVal, csize, hasCat);
  }
  private void DrawWall(float x, float y, float cellSize)
  {
    swc.WriteLine($"ctx.drawImage(wallImg,{x.F()}, {y.F()}, {cellSize.F()}, {cellSize.F()});");
  }
  private void DrawCat(float x, float y, float cellSize)
  {
    var cat = puzzle!.Cat;
    var angle = 0;
    string scaleStr = "";
    switch (cat.LookAt)
    {
      case Directions.S: angle = 90; break;
      case Directions.W: angle = 180; scaleStr = "ctx.scale(1,-1);"; break;
      case Directions.N: angle = 270; break;
    }
    string sx = (x + cellSize / 2).F(), sy = (y + cellSize / 2).F(), scell2 = (-cellSize / 2).F();
    // Console.WriteLine(scaleStr);
    swc.WriteLine($"""
ctx.save();
ctx.translate({sx},{sy});    
ctx.rotate(Math.PI / 180 * {angle});{scaleStr}
ctx.drawImage(catImg, {scell2}, {scell2}, {cellSize.F()}, {cellSize.F()});
//ctx.drawImage(catImg,{x.F()},{y.F()},{cellSize.F()},{cellSize.F()});
ctx.restore();
""");
    if (cat.Carrying == FieldObjects.Mouse)
      DrawMouseMapShifted(x, y, 0.12f, cellSize);
  }

  void DrawMouseMapShifted(float x, float y, float shift, float cellSize)
  {
    shift = cellSize * shift;
    var scsh = (cellSize - 2 * shift).F();
    swc.WriteLine($"ctx.drawImage(mouseImg, {(x + shift).F()}, {(y + shift).F()}, {scsh}, {scsh});");
  }
  void DrawBowl(float x, float y, float cellSize)
  {
    swc.WriteLine($"ctx.drawImage(bowlImg,{x.F()}, {y.F()},{cellSize.F()}, {cellSize.F()});");
  }
  void CanvasFunc(string name, params object[] args)
  {
    swc.Write($"ctx.{name}(");
    for (int i = 0; i < args.Length; i++)
    {
      if (i > 0) swc.Write(',');
      if (args[i] is float f) swc.Write(f.F());
      if (args[i] is string s) swc.Write($"\"{s}\"");
    }
    swc.WriteLine(");");
  }
  private void DrawCoordinateLines()
  {
    swc.WriteLine("ctx.strokeStyle = \"grey\";ctx.lineWidth = 0.8;");
    for (int iy = 0; iy <= RowCount; iy++)
    {
      var (x0, y0) = GetCellPos(0, iy);
      var (x1, y1) = GetCellPos(ColCount, iy);
      CanvasFunc("moveTo", x0, y0);
      CanvasFunc("lineTo", x1, y1);
    }
    for (int ix = 0; ix <= ColCount; ix++)
    {
      var (x0, y0) = GetCellPos(ix, 0);
      var (x1, y1) = GetCellPos(ix, RowCount);
      CanvasFunc("moveTo", x0, y0);
      CanvasFunc("lineTo", x1, y1);
    }
    CanvasFunc("stroke");
  }
  void DrawCoordinates()
  {
    var fnts = $"{(int)(cellSize * 0.6f)}px Arial";
    swc.WriteLine("ctx.fillStyle = \"darkgrey\";" +
      $"ctx.font = \"{fnts}\";");

    swc.WriteLine("ctx.textAlign = \"right\";ctx.textBaseline = 'middle';");
    for (int iy = 0; iy < RowCount; iy++)
    {
      var (x0, y0) = GetCellPos(0, iy);
      var s = iy.ToString();
      //var (sx, sy) = MeasureString(fnts, s);
      CanvasFunc("fillText", s, borderX - cellSize / 10, y0 + cellSize / 2);

    }
    swc.WriteLine("ctx.textAlign = \"center\";ctx.textBaseline = 'alphabetic';");
    for (int ix = 0; ix < ColCount; ix++)
    {
      var (x0, y0) = GetCellPos(ix, 0);
      var s = ix.ToString();
      //var (sx, sy) = MeasureString(fnts, s);
      CanvasFunc("fillText", s, x0 + cellSize / 2, borderY - cellSize / 10);
    }
  }

  private void DrawCellText(float x, float y, string text, float cellSize, bool hasCat)
  {
    var fh = cellSize * 0.8f;
    var font = $"{(int)fh}px Arial";

    var bo = 0.05f;
    var measurewidth = (int)((1 - bo) * cellSize);
    var mSizeW = measurewidth;
    var mSizeH = measurewidth;

    int wordCount = text.Split(" \t".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Length;
    (float, float) FindSize()
    {
      while (true)
      {
        var (sx, sy) = MeasureString(font, text);
        if (sy < cellSize * 0.9f && sx < cellSize * 0.9f)
          return (sx, sy);
        fh *= 0.9f;
        font = $"{(int)fh}px Arial";
      }

    }
    var sz = FindSize();
    swc.WriteLine("ctx.textAlign = \"center\";ctx.textBaseline = 'middle';");
    swc.WriteLine("ctx.fillStyle = \"black\";" + $"ctx.font = \"{font}\";");
    CanvasFunc("fillText", text, x + cellSize / 2, y + cellSize / 2);
  }

  private void DrawCellShape(FieldShapes shape, float x, float y, string br, float cellSize)
  {
    if (shape == FieldShapes.None) return;
    var d = cellSize / 20;
    var e = cellSize / 10;
    var wh = cellSize - 2 * d;
    var wh2 = wh / 2;
    var cx = x + cellSize / 2;
    var cy = y + cellSize / 2;
    var p2 = ((float)(Math.PI * 2)).F();

    swc.WriteLine($"ctx.fillStyle = \"{br}\";" + $"ctx.strokeStyle = \"grey\";");

    
    switch (shape)
    {
      case FieldShapes.Diamond:
        var poly = new PointF[] { new( cellSize/2,  d), new(cellSize-e,cellSize/2),
          new(cellSize/2,cellSize-d),new(e,cellSize/2) };
        var shift = new SizeF(x, y);
        for (int i = 0; i < poly.Length; i++)
        {
          poly[i] = PointF.Add(poly[i], shift);
        }
        string PolyPath()
        {
          var sb = new StringBuilder();
          for (int i = 0; i < poly!.Length; i++)
          {
            var p = poly[i];
            var mlto = i == 0 ? "moveTo" : "lineTo";
            sb.Append($"ctx.{mlto}({p.X.F()},{p.Y.F()});");
          }
          sb.Append("ctx.closePath();");
          return sb.ToString();
        }
        {
          var els = PolyPath();  
          swc.WriteLine($"ctx.beginPath();{els};ctx.fill();");
          swc.WriteLine($"ctx.beginPath();{els};ctx.stroke();");
        }

        break;
      case FieldShapes.Circle:
        {
          var els = $"ctx.ellipse({cx.F()},{cy.F()},{wh2.F()},{wh2.F()},0,0,{p2})";
          swc.WriteLine($"ctx.beginPath();{els};ctx.fill();");
          swc.WriteLine($"ctx.beginPath();{els};ctx.stroke();");
        }
        break;
      case FieldShapes.Square:
        {
          var els = $"ctx.rect({(x+d).F()},{(y+d).F()},{wh.F()},{wh.F()})";
          swc.WriteLine($"ctx.beginPath();{els};ctx.fill();");
          swc.WriteLine($"ctx.beginPath();{els};ctx.stroke();");
        }
        break;
        //default:
        //  gr.DrawString(shape.ToString(), SystemFonts.DefaultFont, Brushes.Red, x, y);
        //  break;
    }
  }


}



