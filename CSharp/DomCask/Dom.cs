using System.Globalization;

namespace DomCask;

public interface IDomProvider
{
  string JsEvalWithResult(string jsCode);
  void JsEval(string jsCode) => JsEvalWithResult(jsCode);
  void SetProperty(string elName, string propName, string propValue);
  void SetProperty(string elName, string propName, bool propValue);
  string GetProperty(string elName, string propName);
  void AddEventListener(string elId, string eventName, Action action);
}


public static class Dom
{
  private static IDomProvider globalProvider = new DummyDom();
  // a provider for one piece of code (and the threads it starts), see Use
  private static readonly AsyncLocal<IDomProvider?> scoped = new();
  private static IDomProvider Provider => scoped.Value ?? globalProvider;

  /// <summary>The provider of the page (the web app sets it once).</summary>
  public static void SetProvider(IDomProvider provider) => globalProvider = provider;

  /// <summary>
  /// A provider only for the code until the result is disposed (e.g. a page drawn in another process, a test that
  /// records what is drawn) – other threads keep theirs.
  /// </summary>
  public static IDisposable Use(IDomProvider provider)
  {
    var old = scoped.Value;
    scoped.Value = provider;
    return new Restore(old);
  }

  sealed class Restore(IDomProvider? old) : IDisposable
  {
    public void Dispose() => scoped.Value = old;
  }

  public static string JsEvalWithResult(string jsCode) => Provider.JsEvalWithResult(jsCode);
  public static void JsEval(string jsCode) => Provider.JsEval(jsCode);
  public static void SetStyle(string elId, string propName, string propValue)
  {
    JsEval($"document.getElementById('{elId}').style.{propName} = '{propValue}';");
  }

  public static string GetStyle(string elId, string propName) =>
    JsEvalWithResult($"document.getElementById('{elId}').style.{propName};");
  public static void CallMethod(string elId, string methodName) 
    => JsEval($"document.getElementById('{elId}').{methodName}();");
  public static void AddEventListener(string elId, string eventName, Action action) => Provider.AddEventListener(elId, eventName, action);
  public static void SetProperty(string elName, string propName, string propValue) => Provider.SetProperty(elName, propName, propValue);
  public static void SetProperty(string elName, string propName, bool propValue) => Provider.SetProperty(elName, propName, propValue);
  public static void SetProperty(string elName, string propName, double propValue)
    => SetProperty(elName, propName, propValue.ToString(CultureInfo.InvariantCulture));

  public static string GetProperty(string elName, string propName) => Provider.GetProperty(elName, propName);
  public static int GetIntProperty(string elName, string propName) => int.Parse(Provider.GetProperty(elName, propName));
  public static void ScrollIntoView(string elId) => CallMethod(elId, "scrollIntoView");

  public static void InsertAdjacentHTML(string elId, string position, string htmlText)
  {
    JsEval($"document.getElementById({JsString(elId)}).insertAdjacentHTML({JsString(position)}, {JsString(htmlText)});");
  }
  public static void LocalStoragSet(string key, string value)
  {
    JsEval($"window.localStorage.setItem({JsString(key)}, {JsString(value)});");
  }

  /// <summary>
  /// A text as a JavaScript string literal, for code given to JsEval: quotes, backslashes, line breaks escaped. (A
  /// text put into `…` as it is breaks the code with a backtick, and "${…}" in it would run.)
  /// </summary>
  public static string JsString(string text)
  {
    var sb = new System.Text.StringBuilder(text.Length + 2);
    sb.Append('"');
    foreach (var c in text)
    {
      switch (c)
      {
        case '\\': sb.Append("\\\\"); break;
        case '"': sb.Append("\\\""); break;
        case '\n': sb.Append("\\n"); break;
        case '\r': sb.Append("\\r"); break;
        case '\u2028': sb.Append("\\u2028"); break;
        case '\u2029': sb.Append("\\u2029"); break;
        default:
          if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
          else sb.Append(c);
          break;
      }
    }
    sb.Append('"');
    return sb.ToString();
  }
  public static string LocalStoragGet(string key)
  {
    return JsEvalWithResult($"window.localStorage.getItem('{key}');");
  }
}

class DummyDom : IDomProvider
{
  public string JsEvalWithResult(string jsCode) => throw new NotImplementedException();
  public string GetProperty(string elName, string propName) => throw new NotImplementedException();
  public void SetProperty(string elName, string propName, string propValue) => throw new NotImplementedException();
  public void SetProperty(string elName, string propName, bool propValue) => throw new NotImplementedException();
  public void AddEventListener(string elId, string eventName, Action action) => throw new NotImplementedException();
}
