using System.Text;
using Eleu.Interpret;
namespace Eleu.Types;

class NilType
{
  private NilType()
  {
  }
  public static NilType Nil = new();
  public override string ToString() => "nil";
}
class OTable : Dictionary<string, object>
{

  public void Set(string name, object val) => this[name] = val;

  public bool Get(string name, out object val)
  {
    bool b = TryGetValue(name, out val!);
    val ??= NilValue;
    return b;
  }
}

public interface ICallable
{
  object Call(Interpreter interpreter, object[] arguments);
  int Arity { get; }
  string Name { get; }
}

internal class NativeFunction : ICallable
{
  public readonly NativeFn function;
  private string name;

  public NativeFunction(string name, NativeFn function)
  {
    this.function = function;
    this.name = name;
  }

  public int Arity => function.Method.GetParameters().Length - 1;

  public string Name => name;

  public object Call(Interpreter interpreter, object[] arguments)
  {
    return function(arguments);
  }
  public override string ToString() => $"Interne Funktion {name}({new string(',', Arity)})";
}

class EleuFunction : ICallable
{
  internal readonly Stmt.Function declaration;
  internal readonly EleuEnvironment closure;
  internal readonly bool isInitializer;
  Chunk? _chunk;
  public EleuFunction(Stmt.Function declaration, EleuEnvironment closure, bool isInitializer)
  {
    this.declaration = declaration;
    this.closure = closure;
    this.isInitializer = isInitializer;
  }
  public int Arity => declaration.Paras.Count;
  public object Call(Interpreter interpreter, object[] arguments)
  {
    var environment = new EleuEnvironment(closure);
    for (int i = 0; i < declaration.Paras.Count; i++)
    {
      environment.Define(declaration.Paras[i].StringValue, arguments[i]);
    }
    var retVal = interpreter.ExecuteBlock(declaration.Body, environment).Value;
    if (isInitializer) return closure.GetAt("this", 0);
    return retVal;
  }
  public override string ToString()
  {
    var sb = new StringBuilder();
    var fts = "Funktion";
    var thisPrefix = "";
    if (declaration.Type != FunctionType.FunTypeFunction)
    {
      fts = "Methode";
      if (closure.GetAtDistance0("this") is EleuInstance vn)
      { thisPrefix = vn.klass.Name + "."; }
    }
    sb.Append($"{fts} {thisPrefix}{declaration.Name}(");
    for (int i = 0; i < declaration.Paras.Count; i++)
    {
      if (i > 0) sb.Append(',');
      sb.Append(declaration.Paras[i].StringValue);
    }
    sb.Append(')');
    return sb.ToString();
  }

  public string Name => declaration.Name;

  public EleuFunction Bind(EleuInstance instance, bool copyInstructions)
  {
    var environment = new EleuEnvironment(closure);
    environment.Define("this", instance);
    var func = new EleuFunction(declaration, environment, isInitializer);
    if (copyInstructions) func._chunk = this.compiledChunk;
    return func;
  }

  public Chunk compiledChunk
  {
    get
    {
      if (_chunk == null)
      {
        var compiler = new StmtCompiler();
        compiler.isInitializer = this.isInitializer;
        var chunk = compiler.Compile(declaration.Body);
        if (!isInitializer)
          chunk.Add(new PushInstruction(NilValue, InputStatus.Empty));
        else
          chunk.Add(new LookupVarInstruction("this", 1, InputStatus.Empty));
        _chunk = chunk;
      }
      return _chunk!;
    }
  }

}

