using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Eleu.Cli;

namespace Eleu.Tests;

/// <summary>The protocol of "eleu-cli daemon" for the VS Code extension.</summary>
public sealed class DaemonTests : IDisposable
{
  const string MiniPuzzle = """
    var p = "
    :def
    c=Cat
    g=Green
    :grid
    cg
    :meta
    win=CatAt 1 0
    name=Mini
    funcs=move
    :info
    Gehe einen Schritt.
    ";
    assert _puzzle(p);
    move();
    """;

  readonly MessageWriter messages = new();
  readonly Daemon daemon;

  public DaemonTests()
  {
    // the daemon draws in its own thread: the tests' DOM router hands that to the daemon's page
    daemon = new Daemon(messages, installDom: false);
    RecordingDom.Fallback = daemon.PageDom;
  }

  public void Dispose()
  {
    daemon.Dispose();
    RecordingDom.Fallback = null;
  }

  void Send(object request) => daemon.Handle(JsonSerializer.Serialize(request));

  [Fact]
  public void Check_reports_compile_errors_with_position()
  {
    Send(new { id = 1, type = "check", file = "a.eleu", text = "var x = 1;\nvar y = ;" });
    var answer = messages.Next(m => m.Has("id", 1));
    var error = Assert.Single(answer.GetProperty("errors").EnumerateArray());
    Assert.Equal(2, error.GetProperty("line").GetInt32());
    // columns from 1; the parser marks the token before the missing expression: "="
    Assert.Equal(7, error.GetProperty("column").GetInt32());
    Assert.Equal(8, error.GetProperty("endColumn").GetInt32());
    Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("message").GetString()));
  }

  [Fact]
  public void Check_reports_resolver_errors()
  {
    Send(new { id = 2, type = "check", file = "a.eleu", text = "fun f() { return; }\nreturn 1;" });
    var answer = messages.Next(m => m.Has("id", 2));
    Assert.NotEmpty(answer.GetProperty("errors").EnumerateArray());
  }

  [Fact]
  public void Check_of_correct_code_has_no_errors()
  {
    Send(new { id = 3, type = "check", file = "a.eleu", text = MiniPuzzle });
    var answer = messages.Next(m => m.Has("id", 3));
    Assert.Empty(answer.GetProperty("errors").EnumerateArray());
  }

  [Fact]
  public void Funcnames_lists_native_and_puzzle_functions()
  {
    Send(new { id = 4, type = "funcnames" });
    var answer = messages.Next(m => m.Has("id", 4));
    Assert.Contains("sqrt", answer.GetProperty("native").EnumerateArray().Select(e => e.GetString()));
    Assert.Contains("move", answer.GetProperty("puzzle").EnumerateArray().Select(e => e.GetString()));
  }

  [Fact]
  public void Images_are_embedded()
  {
    Send(new { id = 5, type = "images" });
    var images = messages.Next(m => m.Has("id", 5)).GetProperty("images").EnumerateObject().ToList();
    Assert.Equal(["catImg", "wallImg", "bowlImg", "mouseImg"], images.Select(i => i.Name));
    Assert.All(images, i => Assert.StartsWith("data:image/png;base64,iVBOR", i.Value.GetString()));
  }

  [Fact]
  public void Format_formats_with_the_given_line_breaks()
  {
    Send(new { id = 6, type = "format", text = "if(a){print(1);}", newLine = "\r\n" });
    Assert.Equal("if (a)\r\n{\r\n  print(1);\r\n}\r\n", messages.Next(m => m.Has("id", 6)).GetProperty("text").GetString());
  }

  [Fact]
  public void Run_sends_output_and_end()
  {
    Send(new { type = "run", file = "a.eleu", text = "print(1 + 2);", runAll = false });
    Assert.Equal("3", messages.Next(m => m.IsEvent("out")).GetProperty("text").GetString());
    Assert.True(messages.Next(m => m.IsEvent("end")).GetProperty("ok").GetBoolean());
  }

  [Fact]
  public void Runtime_error_has_position()
  {
    Send(new { type = "run", file = "a.eleu", text = "print(1);\nprint(x);", runAll = false });
    var error = messages.Next(m => m.IsEvent("err")).GetProperty("text").GetString()!;
    Assert.Equal(2, Eleu.Scanning.InputStatus.Parse(error).LineStart);
    Assert.False(messages.Next(m => m.IsEvent("end")).GetProperty("ok").GetBoolean());
  }

  [Fact]
  public void Puzzle_is_drawn_and_its_code_sent()
  {
    Send(new { type = "speed", value = 35 });
    Send(new { type = "view", width = 300, height = 200 });
    Send(new { type = "run", file = "a.eleu", text = MiniPuzzle, runAll = false });
    var call = messages.Next(m => m.IsEvent("pcall"));
    Assert.StartsWith(">:)", call.GetProperty("code").GetString());
    var puzzle = messages.Next(m => m.IsEvent("puzzle"));
    Assert.Equal("Mini", puzzle.GetProperty("name").GetString());
    var ops = puzzle.GetProperty("ops").EnumerateArray().ToList();
    Assert.Contains(ops, o => o.Has("set", "puzzDiv") && o.GetProperty("value").GetString()!.Contains("Gehe einen Schritt."));
    Assert.Contains(ops, o => o.TryGetProperty("eval", out var js) && js.GetString()!.Contains("ctx.drawImage(catImg"));
    var end = messages.Next(m => m.IsEvent("end"));
    Assert.True(end.GetProperty("ok").GetBoolean());
    Assert.Contains(messages.All, m => m.IsEvent("pout") && m.GetProperty("text").GetString()!.Contains("gelöst"));
  }

  [Fact]
  public void Puzzle_request_shows_another_test()
  {
    Send(new { type = "speed", value = 35 });
    Send(new { type = "run", file = "a.eleu", text = MiniPuzzle, runAll = false });
    messages.Next(m => m.IsEvent("end"));
    // the test selection: the last puzzle of _puzzle(), test 1 at the start
    Send(new { type = "puzzle", index = 0 });
    var puzzle = messages.Next(m => m.IsEvent("puzzle") && m.GetProperty("ops").EnumerateArray().Any(o => o.TryGetProperty("eval", out var js) && js.GetString()!.Contains("catImg")));
    Assert.Equal(0, puzzle.GetProperty("index").GetInt32());
  }

  [Fact]
  public void Stop_ends_an_endless_program()
  {
    Send(new { type = "run", file = "a.eleu", text = "var i = 0;\nwhile (true) { i = i + 1; }", runAll = false });
    messages.Next(m => m.IsEvent("state") && m.GetProperty("running").GetBoolean());
    Send(new { type = "stop" });
    messages.Next(m => m.IsEvent("state") && !m.GetProperty("running").GetBoolean());
    Assert.Contains(messages.All, m => m.IsEvent("err") && m.GetProperty("text").GetString() == "Programm abgebrochen!");
  }

  [Fact]
  public void Bad_request_is_answered_with_fatal()
  {
    Send(new { id = 9, type = "nonsense" });
    Assert.Contains("nonsense", messages.Next(m => m.Has("id", 9)).GetProperty("fatal").GetString());
  }

  /// <summary>The daemon's output, one JSON message per line.</summary>
  sealed class MessageWriter : TextWriter
  {
    readonly BlockingCollection<JsonElement> queue = new();
    public ConcurrentQueue<JsonElement> All { get; } = new();
    public override Encoding Encoding => Encoding.UTF8;

    public override void WriteLine(string? value)
    {
      if (value == null) return;
      var element = JsonDocument.Parse(value).RootElement.Clone();
      All.Enqueue(element);
      queue.Add(element);
    }

    /// <summary>The next message that matches (skipping the others), within 10 seconds.</summary>
    public JsonElement Next(Func<JsonElement, bool> match)
    {
      var until = DateTime.UtcNow.AddSeconds(10);
      while (true)
      {
        var left = until - DateTime.UtcNow;
        if (left <= TimeSpan.Zero || !queue.TryTake(out var message, left))
          throw new TimeoutException("No matching message from the daemon. Got:\n" + string.Join('\n', All.Select(m => m.GetRawText())));
        if (match(message)) return message;
      }
    }
  }
}

internal static class JsonTestExtensions
{
  public static bool IsEvent(this JsonElement e, string name) => e.Has("event", name);

  public static bool Has(this JsonElement e, string property, object value) =>
    e.ValueKind == JsonValueKind.Object && e.TryGetProperty(property, out var v) && value switch
    {
      int i => v.ValueKind == JsonValueKind.Number && v.GetInt32() == i,
      string s => v.ValueKind == JsonValueKind.String && v.GetString() == s,
      _ => false,
    };
}
