using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DomCask;

namespace Eleu.Tests;

/// <summary>
/// A DOM that writes down everything sent to the browser (HTML, canvas code, properties).
/// <see cref="Router"/> is the provider for <see cref="Dom"/>; it hands each call to the recorder of the
/// current test, so that tests may run in parallel.
/// </summary>
internal sealed class RecordingDom : IDomProvider
{
  public const int ClientWidth = 480, ClientHeight = 360;
  static readonly AsyncLocal<RecordingDom?> current = new();

  /// <summary>Used outside <see cref="Capture"/> (e.g. by the daemon in its own thread).</summary>
  public static IDomProvider? Fallback { get; set; }
  public static readonly IDomProvider Router = new RouterDom();
  readonly StringBuilder sb = new();

  /// <summary>Runs <paramref name="action"/> and returns what it sent to the browser.</summary>
  public static string Capture(Action action)
  {
    var recorder = new RecordingDom();
    var old = current.Value;
    current.Value = recorder;
    try { action(); }
    finally { current.Value = old; }
    return recorder.sb.ToString().ReplaceLineEndings("\n");
  }

  public string JsEvalWithResult(string jsCode)
  {
    if (jsCode.Contains(".clientWidth")) return ClientWidth.ToString();
    if (jsCode.Contains(".clientHeight")) return ClientHeight.ToString();
    if (jsCode.Contains("ctx.measureText(")) return Measure(jsCode);
    sb.Append("eval: ").AppendLine(jsCode);
    return "";
  }
  /// <summary>A fake ctx.measureText: 0.6 × font size per character, 0.8 × font size high.</summary>
  static string Measure(string jsCode)
  {
    var size = Regex.Match(jsCode, @"(\d+(?:\.\d+)?)px");
    var fontSize = size.Success ? double.Parse(size.Groups[1].Value, CultureInfo.InvariantCulture) : 10;
    var text = Regex.Match(jsCode, "measureText\\(\"(.*)\"\\)").Groups[1].Value;
    var width = text.Length * fontSize * 0.6;
    return string.Create(CultureInfo.InvariantCulture, $"{width}|{fontSize * 0.8}");
  }

  public void JsEval(string jsCode) => JsEvalWithResult(jsCode);
  public void SetProperty(string elName, string propName, string propValue) =>
    sb.Append($"set {elName}.{propName} = ").AppendLine(propValue);
  public void SetProperty(string elName, string propName, bool propValue) =>
    sb.Append($"set {elName}.{propName} = ").AppendLine(propValue ? "true" : "false");
  public string GetProperty(string elName, string propName) => "";
  public void AddEventListener(string elId, string eventName, Action action) { }

  sealed class RouterDom : IDomProvider
  {
    static IDomProvider Rec => current.Value ?? Fallback ?? throw new InvalidOperationException("DOM access outside RecordingDom.Capture");
    public string JsEvalWithResult(string jsCode) => Rec.JsEvalWithResult(jsCode);
    public void JsEval(string jsCode) => Rec.JsEval(jsCode);
    public void SetProperty(string elName, string propName, string propValue) => Rec.SetProperty(elName, propName, propValue);
    public void SetProperty(string elName, string propName, bool propValue) => Rec.SetProperty(elName, propName, propValue);
    public string GetProperty(string elName, string propName) => Rec.GetProperty(elName, propName);
    public void AddEventListener(string elId, string eventName, Action action) => Rec.AddEventListener(elId, eventName, action);
  }
}
