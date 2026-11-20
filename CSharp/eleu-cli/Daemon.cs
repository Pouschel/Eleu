using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DomCask;
using Eleu.LangServer;
using Eleu.Puzzles;
using Eleu.Scanning;

namespace Eleu.Cli;

/// <summary>
/// "eleu-cli daemon": runs Eleu for an editor (the VS Code extension), one JSON message per line on standard input,
/// one per line on standard output. Requests with an "id" get an answer with the same id; a running program sends
/// events ({"event": …}) at any time.
/// <list type="bullet">
/// <item>{"id": 1, "type": "check", "file": "C:\\…\\a.eleu", "text": "…"} → {"id": 1, "errors": [{"line": 3, "column": 7,
/// "endLine": 3, "endColumn": 9, "message": "…"}]} – compile errors (parser and resolver), lines and columns from 1.</item>
/// <item>{"id": 2, "type": "funcnames"} → {"id": 2, "native": ["sqrt", …], "puzzle": ["move", …]}</item>
/// <item>{"type": "run", "file": "…", "text": "…", "runAll": false} – runs the program stepwise like the web app.</item>
/// <item>{"type": "stop"} – stops the running program.</item>
/// <item>{"type": "puzzle", "text": "…" (optional: the last puzzle of _puzzle()), "index": 0} – shows a test of a
/// puzzle (the test selection and "back to start" of the web app).</item>
/// <item>{"type": "speed", "value": 20} – the speed slider of the web app (0 … 35).</item>
/// <item>{"type": "view", "width": 480, "height": 360, "metrics": {"a": [55.6, 52.1, 1.2], …}} – the size of the
/// puzzle canvas and the width, ascent and descent of the characters in 100px Arial.</item>
/// </list>
/// Events: {"event": "out" | "err" | "info" | "pout" | "i_err", "text": "…"}, {"event": "state", "running": true},
/// {"event": "end", "ok": true}, {"event": "puzzle", "name": "…", "index": 0, "count": 4, "ops": [{"eval": "…"} |
/// {"set": "puzzDiv", "prop": "innerHTML", "value": "…"}]} (what PuzzleHtmlCreator does with the page),
/// {"event": "pcall", "code": "…" (compressed), "index": 0, "show": true} (a call of _puzzle()).
/// </summary>
internal sealed class Daemon : IDisposable
{
  readonly TextWriter output;
  readonly object writeLock = new();
  readonly WebviewDom dom = new();
  EleuLanguageServer? server;
  PuzzleHtmlCreator.RenderState renderState = new();
  volatile bool stopRequested, running;
  volatile int frameDelay;
  double speed = 20;
  string? lastPuzzleCode;
  Puzzle? lastPuzzle;

  /// <param name="installDom">false in tests: they route the DOM themselves (see <see cref="PageDom"/>)</param>
  public Daemon(TextWriter output, bool installDom = true)
  {
    this.output = output;
    if (installDom) Dom.SetProvider(dom);
  }

  internal WebviewDom PageDom => dom;

  public static int Run()
  {
    var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
    // nothing else may write to standard output
    Console.SetOut(Console.Error);
    using var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
    using var daemon = new Daemon(stdout);
    string? line;
    while ((line = input.ReadLine()) != null)
    {
      if (line.Trim().Length > 0) daemon.Handle(line);
    }
    return 0;
  }

  public void Dispose() => server?.Dispose();

  int FrameTime
  {
    get
    {
      var delta = 35 - speed;
      return (int)(delta * delta);
    }
  }

  EleuLanguageServer Server => server ??= new EleuLanguageServer(OnResponse);

  public void Handle(string line)
  {
    JsonElement id = default;
    try
    {
      var root = JsonDocument.Parse(line).RootElement;
      id = root.TryGetProperty("id", out var idValue) ? idValue.Clone() : default;
      string Str(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
      switch (Str("type"))
      {
        case "check":
          Check(id, Str("file"), Str("text"));
          break;
        case "funcnames":
          Write(json =>
          {
            WriteId(json, id);
            WriteArray(json, "native", new NativeFunctions().GetFunctions().Select(f => f.name).Append("PI"));
            WriteArray(json, "puzzle", new PuzzleFunctions().GetFunctions().Select(f => f.name));
          });
          break;
        case "run":
          stopRequested = false;
          frameDelay = 0;
          var runAll = root.TryGetProperty("runAll", out var ra) && ra.ValueKind == JsonValueKind.True;
          Server.SendCommand("code", new object[] { Str("file"), Str("text"), runAll });
          break;
        case "stop":
          stopRequested = true;
          Server.SendCommand("stop", "");
          break;
        case "puzzle":
          var text = Str("text");
          if (text.Length == 0) text = lastPuzzleCode ?? "";
          if (text.Length == 0) break;
          var index = root.TryGetProperty("index", out var ix) && ix.ValueKind == JsonValueKind.Number ? ix.GetInt32() : 0;
          Server.SendCommand("puzzle", new object[] { text, index });
          break;
        case "speed":
          if (root.TryGetProperty("value", out var sv) && sv.ValueKind == JsonValueKind.Number) speed = Math.Clamp(sv.GetDouble(), 0, 35);
          break;
        case "view":
          dom.SetView(root);
          // a new size: draw the shown puzzle again
          if (!running && lastPuzzle != null) SendPuzzle(lastPuzzle);
          break;
        default:
          throw new ArgumentException($"Unknown request type '{Str("type")}'");
      }
    }
    catch (Exception ex)
    {
      Write(json =>
      {
        if (id.ValueKind != JsonValueKind.Undefined) WriteId(json, id);
        else json.WriteString("event", "i_err");
        json.WriteString(id.ValueKind != JsonValueKind.Undefined ? "fatal" : "text", ex.Message);
      });
    }
  }

  void Check(JsonElement id, string file, string text)
  {
    var errors = new List<string>();
    var opt = new EleuOptions() { Err = new LineWriter(errors.Add), Out = TextWriter.Null };
    var (result, interpreter) = Globals.Compile(text, file, opt);
    // the resolver runs at the start of a program: its errors are compile errors, too
    if (result == EEleuResult.Ok) interpreter!.Start();
    Write(json =>
    {
      WriteId(json, id);
      json.WriteStartArray("errors");
      foreach (var error in errors)
      {
        var (status, message) = InputStatus.ParseWithMessage(error);
        if (status.IsEmpty && status.LineStart == 0) continue;
        json.WriteStartObject();
        json.WriteNumber("line", status.LineStart);
        json.WriteNumber("column", status.ColStart);
        json.WriteNumber("endLine", status.LineEnd);
        json.WriteNumber("endColumn", status.ColEnd);
        json.WriteString("message", message.StartsWith("Cerr: ") ? message[6..] : message);
        json.WriteEndObject();
      }
      json.WriteEndArray();
    });
  }

  void OnResponse(string name, object arg)
  {
    switch (name)
    {
      case "info":
      case "out":
      case "err":
      case "i_err":
      case "pout":
        Event(name, json => json.WriteString("text", (string)arg));
        break;
      case "state":
        var nowRunning = (bool)arg;
        if (nowRunning != running) Event("state", json => json.WriteBoolean("running", nowRunning));
        running = nowRunning;
        if (nowRunning && !stopRequested)
        {
          // the animation of the puzzle: wait after every change, as the web app does
          var delay = frameDelay;
          frameDelay = 0;
          if (delay > 0) Thread.Sleep(delay);
          if (!stopRequested) Server.SendCommand("steps", 20);
        }
        break;
      case "end":
        Event("end", json => json.WriteBoolean("ok", (bool)arg));
        break;
      case "puzzle":
        lastPuzzle = (Puzzle)arg;
        SendPuzzle(lastPuzzle);
        frameDelay = FrameTime;
        break;
      case "_pcall":
        var pc = (PuzzCode)arg;
        lastPuzzleCode = pc.Compressed;
        Event("pcall", json =>
        {
          json.WriteString("code", pc.Compressed.TrimEnd());
          json.WriteNumber("index", pc.TestIndex);
          json.WriteBoolean("show", pc.ShowCompressedCode);
        });
        break;
    }
  }

  void SendPuzzle(Puzzle puzzle)
  {
    List<WebviewDom.Op> ops;
    lock (dom)
    {
      dom.Begin();
      new PuzzleHtmlCreator(puzzle).Render(ref renderState);
      ops = dom.End();
    }
    Event("puzzle", json =>
    {
      json.WriteString("name", puzzle.Name);
      json.WriteNumber("index", puzzle.BundleIndex);
      json.WriteNumber("count", puzzle.Bundle.Count);
      json.WriteStartArray("ops");
      foreach (var op in ops)
      {
        json.WriteStartObject();
        if (op.Element == null) json.WriteString("eval", op.Value);
        else
        {
          json.WriteString("set", op.Element);
          json.WriteString("prop", op.Property);
          json.WriteString("value", op.Value);
        }
        json.WriteEndObject();
      }
      json.WriteEndArray();
    });
  }

  void Event(string name, Action<Utf8JsonWriter> content) => Write(json =>
  {
    json.WriteString("event", name);
    content(json);
  });

  static void WriteId(Utf8JsonWriter json, JsonElement id)
  {
    json.WritePropertyName("id");
    id.WriteTo(json);
  }

  static void WriteArray(Utf8JsonWriter json, string name, IEnumerable<string> values)
  {
    json.WriteStartArray(name);
    foreach (var v in values) json.WriteStringValue(v);
    json.WriteEndArray();
  }

  void Write(Action<Utf8JsonWriter> content)
  {
    using var stream = new MemoryStream();
    using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
    {
      json.WriteStartObject();
      content(json);
      json.WriteEndObject();
    }
    var line = Encoding.UTF8.GetString(stream.ToArray());
    lock (writeLock)
    {
      output.WriteLine(line);
      output.Flush();
    }
  }

  sealed class LineWriter(Action<string> onLine) : TextWriter
  {
    public override Encoding Encoding => Encoding.UTF8;
    public override void WriteLine(string? value)
    {
      if (value != null) onLine(value);
    }
  }
}

/// <summary>
/// The page of the preview as seen by <see cref="PuzzleHtmlCreator"/>: answers the size of the canvas and the text
/// measurements (from the webview), writes down everything else as operations for the webview.
/// </summary>
internal sealed partial class WebviewDom : IDomProvider
{
  public sealed record Op(string? Element, string Property, string Value);

  int width = 480, height = 360;
  // width, ascent, descent in 100px Arial
  Dictionary<string, double[]> metrics = [];
  List<Op> ops = [];

  public void SetView(JsonElement root)
  {
    if (root.TryGetProperty("width", out var w) && w.ValueKind == JsonValueKind.Number) width = Math.Max(1, (int)w.GetDouble());
    if (root.TryGetProperty("height", out var h) && h.ValueKind == JsonValueKind.Number) height = Math.Max(1, (int)h.GetDouble());
    if (root.TryGetProperty("metrics", out var m) && m.ValueKind == JsonValueKind.Object)
    {
      var table = new Dictionary<string, double[]>();
      foreach (var p in m.EnumerateObject())
        table[p.Name] = p.Value.EnumerateArray().Select(v => v.GetDouble()).ToArray();
      metrics = table;
    }
  }

  public void Begin() => ops = [];
  public List<Op> End() => ops;

  public string JsEvalWithResult(string jsCode)
  {
    if (jsCode.Contains(".clientWidth")) return width.ToString(CultureInfo.InvariantCulture);
    if (jsCode.Contains(".clientHeight")) return height.ToString(CultureInfo.InvariantCulture);
    if (jsCode.Contains("ctx.measureText(")) return Measure(jsCode);
    ops.Add(new(null, "", jsCode));
    return "";
  }

  public void JsEval(string jsCode) => JsEvalWithResult(jsCode);
  public void SetProperty(string elName, string propName, string propValue) => ops.Add(new(elName, propName, propValue));
  public void SetProperty(string elName, string propName, bool propValue) => ops.Add(new(elName, propName, propValue ? "true" : "false"));
  public string GetProperty(string elName, string propName) => "";
  public void AddEventListener(string elId, string eventName, Action action) { }

  [GeneratedRegex(@"(\d+(?:\.\d+)?)px")]
  private static partial Regex FontSize();
  [GeneratedRegex("measureText\\(\"(.*)\"\\)")]
  private static partial Regex MeasuredText();

  string Measure(string jsCode)
  {
    var size = FontSize().Match(jsCode);
    var px = size.Success ? double.Parse(size.Groups[1].Value, CultureInfo.InvariantCulture) : 10;
    var text = MeasuredText().Match(jsCode).Groups[1].Value;
    double w = 0, ascent = 0, descent = 0;
    foreach (var rune in text.EnumerateRunes())
    {
      // unknown characters: like a digit (Arial: 0.556 em wide, 0.716 em high)
      var m = metrics.TryGetValue(rune.ToString(), out var v) && v.Length == 3 ? v : [55.6, 71.6, 0];
      w += m[0];
      ascent = Math.Max(ascent, m[1]);
      descent = Math.Max(descent, m[2]);
    }
    var f = px / 100;
    return string.Create(CultureInfo.InvariantCulture, $"{w * f}|{(ascent + descent) * f}");
  }
}
