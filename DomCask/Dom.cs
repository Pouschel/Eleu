using System.Globalization;

namespace DomCask;

public interface IDomProvider
{
  string JsEval(string jsCode);
  void SetProperty(string elName, string propName, string propValue);
  void SetProperty(string elName, string propName, bool propValue);
  string GetProperty(string elName, string propName);
  void AddEventListener(string elId, string eventName, Action action);
}


public static class Dom
{
  private static IDomProvider Provider = new DummyDom();
  public static void SetProvider(IDomProvider provider) => Provider = provider;
  public static string JsEval(string jsCode) => Provider.JsEval(jsCode);
  public static void SetStyle(string elId, string propName, string propValue)
  {
    JsEval($"document.getElementById('{elId}').style.{propName} = '{propValue}';");
  }

  public static string GetStyle(string elId, string propName) =>
    JsEval($"document.getElementById('{elId}').style.{propName};");

  public static void CallMethod(string elId, string methodName)
  {
    JsEval($"document.getElementById('{elId}').{methodName}();");
  }
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
    JsEval($"document.getElementById('{elId}').insertAdjacentHTML('{position}', `{htmlText}`);");
  }
  public static void LocalStoragSet(string key, string value)
  {
    JsEval($"window.localStorage.setItem('{key}',`{value}`);");
  }
  public static string LocalStoragGet(string key)
  {
    return JsEval($"window.localStorage.getItem('{key}');");
  }
}

class DummyDom : IDomProvider
{
  public string JsEval(string jsCode) => throw new NotImplementedException();
  public string GetProperty(string elName, string propName) => throw new NotImplementedException();
  public void SetProperty(string elName, string propName, string propValue) => throw new NotImplementedException();
  public void SetProperty(string elName, string propName, bool propValue) => throw new NotImplementedException();
  public void AddEventListener(string elId, string eventName, Action action) => throw new NotImplementedException();
}
