using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DomCask;

namespace Eleu.Tests;

/// <summary>
/// A DOM that writes down everything sent to the browser (HTML, canvas code, properties), for the code run in
/// <see cref="Capture"/> (with <see cref="Dom.Use"/>: tests may run in parallel).
/// </summary>
internal sealed class RecordingDom : IDomProvider
{
  public const int ClientWidth = 480, ClientHeight = 360;
  readonly StringBuilder sb = new();

  /// <summary>Runs <paramref name="action"/> and returns what it sent to the browser.</summary>
  public static string Capture(Action action)
  {
    var recorder = new RecordingDom();
    using (Dom.Use(recorder)) action();
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

}
