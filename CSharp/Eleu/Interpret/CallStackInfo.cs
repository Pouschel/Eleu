using Eleu.Types;

namespace Eleu.Interpret;

public class CallStackInfo
{
  public ICallable Function { get; }
  private EleuEnvironment Fence { get; }
  private Interpreter vm;
  internal CallStackInfo(Interpreter vm, ICallable function, EleuEnvironment fence)
  {
    this.vm = vm;
    this.Function = function;
    this.Fence = fence;
  }

  public override string ToString()
  {
    string s = Function.ToString()!;
    return s;
  }

  public List<VariableInfo> GetLocals() => vm.environment.GetVariableInfos(Fence);
}

public class VariableInfo
{
  private string name;
  private object value;

  public string Name => name;

  public string Value => Stringify(value);

  public string Type => NativeFunctions.@typeof(new object[] { value }).ToString()!;

  public VariableInfo(string name, object value)
  {
    this.name = name;
    this.value = value;
  }
}