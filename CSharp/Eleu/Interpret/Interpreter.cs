using System.Diagnostics;
using System.Text;
using Eleu.Puzzles;
using Eleu.Types;

namespace Eleu.Interpret;

public class Interpreter : Expr.Visitor<object>, Stmt.Visitor<InterpretResult>
{
  internal EleuOptions options;
  internal InputStatus currentStatus;
  public Puzzle? Puzzle;
  public Action<Puzzle?>? PuzzleStateChanged;
  public Action<string, int, bool>? PuzzleCalled;
  private List<Stmt> statements;
  internal EleuEnvironment globals = new();
  internal Func<Stmt, bool>? canContinueFunc;
  private Func<Stmt, InterpretResult> Execute;
  private Stack<CallStackInfo> callStack = new();
  internal readonly List<Token> orgTokens;
  public int MaxStackDepth = 200;
  internal CallFrame? frame;
  int frameDepth = 0;
  internal EleuEnvironment environment;
  Stack<EleuEnvironment> envStack = new();
  readonly Stack<object> valueStack = new();
  public int InstructionCount = 0;

  private bool doDumpVm = false;
  internal void NotifyPuzzleChange(Puzzle? newPuzzle, float animateState)
  {
    PuzzleStateChanged?.Invoke(newPuzzle);
  }

  public Interpreter(EleuOptions options, List<Stmt> statements, List<Token> tokens)
  {
    this.options = options;
    NativeFunctionBase.DefineAll(new NativeFunctions(), this);
    NativeFunctionBase.DefineAll(new PuzzleFunctions(), this);
    this.orgTokens = tokens;
    this.environment = globals;
    this.statements = statements;
    globals.Define("PI", new Number(Math.PI));
    Execute = ExecuteRelease;
  }

  public int ProgramLength => orgTokens.Where(tok => tok.Type > TokenSemicolon).Count();

  public int ExecutedInstructionCount
  {
    get;
    private set;
  }
  public int PuzzleScore => this.Puzzle == null ? 0 :
     this.Puzzle.Complexity - this.Puzzle.EnergyUsed - ProgramLength - ExecutedInstructionCount / 3;
  public void DefineNative(string name, NativeFn function)
  {
    var ofun = new NativeFunction(name, function);
    globals.Define(name, ofun);
  }

  public EEleuResult InterpretWithDebug(Func<Stmt, bool> canContinueFunc)
  {
    this.canContinueFunc = canContinueFunc;
    Execute = ExecuteDebug;
    return DoInterpret();
  }
  public EEleuResult InterpretWithDebug(CancellationToken token)
  {
    return InterpretWithDebug(stmt => !token.IsCancellationRequested);
  }
  public EEleuResult Interpret(bool useVm)
  {
    if (useVm)
    {
      var res = Start();
      while (res == EEleuResult.NextStep)
      {
        res = Step();
      }
      return res;
    }
    Execute = ExecuteRelease;
    return DoInterpret();
  }

  public EEleuResult DoInterpret()
  {
    EEleuResult result = Ok;
    try
    {
      callStack = new();
      Resolve();
      ExecutedInstructionCount = 0;
      foreach (var stmt in this.statements)
      {
        Execute(stmt);
      }
    }
    catch (EleuRuntimeError ex)
    {
      if (options.ThrowOnAssert && ex is EleuAssertionFail) throw;
      result = EEleuResult.RuntimeError;
      RuntimeExHandler(ex);
    }
    return result;
  }

  private void RuntimeExHandler(EleuRuntimeError ex)
  {
    var stat = ex.Status ?? currentStatus;
    var msg = $"{options.InputStatusFormatter(stat)}: {ex.Message}";
    options.Err.WriteLine(msg);
    Trace.WriteLine(msg);
    if (!options.DumpStackOnError) return;
    var stack = GetCallStack();
    if (stack.Count == 0) return;
    options.Err.WriteLine("Aufgerufene Funktionen:");
    foreach (var se in stack)
    {
      options.Err.WriteLine(se);
    }
  }

  public EEleuResult Start()
  {
    doDumpVm = !string.IsNullOrEmpty(options.DumpFileName);
    EEleuResult result = EEleuResult.RuntimeError;
    try
    {
      callStack = new();
      Resolve();
      ExecutedInstructionCount = 0;
      var chunk = new StmtCompiler().Compile(this.statements);
      if (doDumpVm)
      {
        if (File.Exists(options.DumpFileName)) File.Delete(options.DumpFileName);
        File.AppendAllText(options.DumpFileName, $"[global]\n\n{chunk}");
      }
      frame = new CallFrame(chunk);
      return EEleuResult.NextStep;
    }
    catch (EleuRuntimeError ex)
    {
      if (options.ThrowOnAssert && ex is EleuAssertionFail) throw;
      result = EEleuResult.RuntimeError;
      RuntimeExHandler(ex);
    }
    return result;
  }
  public EEleuResult Step()
  {
    var ins = frame!.NextInstruction();
    if (ins == null)
    {
      if (frame.next == null) return EEleuResult.Ok;
      // leave current chunk function
      LeaveFrame();
      return NextStep;
    }
    if (doDumpVm)
    {
      var sb = new StringBuilder();
      foreach (var item in valueStack.Take(10).Reverse())
      {
        var s = item.ToString()!;
        if (s.Length > 10) s = s[..10] + "...";
        s = s.Replace('\n', '\t');
        s = s.Replace('\r', '\t');
        sb.Append(' ');
        sb.Append(s);
      }
      File.AppendAllText(options.DumpFileName, $"{ins,-20} | {sb}\n");
    }
    return ExecuteInstruction(ins!);
  }

  private EEleuResult ExecuteInstruction(Instruction ins)
  {
    try
    {
      if (!ins.status.IsEmpty) currentStatus = ins.status;
      ins.Execute(this);
      ExecutedInstructionCount++;
    }
    catch (EleuRuntimeError ex)
    {
      if (options.ThrowOnAssert && ex is EleuAssertionFail) throw;
      RuntimeExHandler(ex);
      return EEleuResult.RuntimeError;
    }
    catch (Exception ex)
    {
      var msg = $"{currentStatus.Message}: {ex.GetType()}";
      options.Err.WriteLine(msg);
      return EEleuResult.RuntimeError;
    }
    return EEleuResult.NextStep;
  }

  internal void EnterFrame(CallFrame newFrame)
  {
    newFrame.next = frame;
    frame = newFrame;
    frameDepth++;
    if (frameDepth >= MaxStackDepth)
      throw new EleuRuntimeError(currentStatus, "Zu viele verschachtelte Funktionsaufrufe.");
    var csi = new CallStackInfo(this, newFrame.func!, environment);
    callStack.Push(csi);
  }
  internal void LeaveFrame()
  {
    frame = frame!.next!;
    environment = envStack.Pop();
    frameDepth--;
    callStack.Pop();
  }
  internal object Pop() => valueStack.Pop();
  internal object Peek() => valueStack.Peek();
  internal void Push(object o) => valueStack.Push(o);
  internal void EnterEnv(EleuEnvironment env)
  {
    envStack.Push(environment);
    environment = env;
  }
  internal void LeaveEnv() => environment = envStack.Pop();
  internal object AssignAtDistance(String name, int distance, Object value)
  {
    if (distance >= 0)
    {
      environment.AssignAt(distance, name, value);
    }
    else
    {
      globals.Assign(name, value);
    }
    return value;
  }
  void Resolve()
  {
    var resolver = new Resolver(this);
    resolver.Resolve(this.statements);
  }
  public EleuRuntimeError Error(string message)
  {
    return new EleuRuntimeError(currentStatus, message);
  }
  private object Evaluate(Expr expr)
  {
    ExecutedInstructionCount++;
    RegisterStatus(expr.Status);
    var evaluated = expr.Accept(this);
    return evaluated;
  }
  internal void RegisterStatus(in InputStatus? status)
  {
    if (status.HasValue)
    {
      currentStatus = status.Value;
    }
  }
  private InterpretResult ExecuteRelease(Stmt stmt)
  {
    return stmt.Accept(this);
  }

  private InterpretResult ExecuteDebug(Stmt stmt)
  {
    RegisterStatus(stmt.Status);
    while (!canContinueFunc!(stmt))
      Thread.Sleep(10);
    ExecutedInstructionCount++;
    return stmt.Accept(this);
  }

  public object VisitAssignExpr(Expr.Assign expr)
  {
    var value = Evaluate(expr.Value);
    var distance = expr.localDistance;
    AssignAtDistance(expr.Name, distance, value);
    return value;
  }

  public object VisitBinaryExpr(Expr.Binary expr)
  {
    var lhs = Evaluate(expr.Left);
    var rhs = Evaluate(expr.Right);
    return expr.Op.Type switch
    {
      TokenBangEqual => !ObjEquals(lhs, rhs),
      TokenEqualEqual => ObjEquals(lhs, rhs),
      TokenGreater => InternalCompare(lhs, rhs) > 0,
      TokenGreaterEqual => InternalCompare(lhs, rhs) >= 0,
      TokenLess => InternalCompare(lhs, rhs) < 0,
      TokenLessEqual => InternalCompare(lhs, rhs) <= 0,
      TokenPlus => NumStrAdd(lhs, rhs),
      TokenMinus => NumSubtract(lhs, rhs),
      TokenStar => NumberOp("*", lhs, rhs, (a, b) => a * b),
      TokenPercent => NumberOp("%", lhs, rhs, (a, b) => a % b),
      TokenSlash => NumberOp("/", lhs, rhs, (a, b) => a / b),
      _ => throw Error("Invalid op: " + expr.Op.Type),
    };
  }

  public void InjectCall(ICallable func, params object[] args)
  {
    for (int i = 0; i < args.Length; i++)
    {
      Push(args[i]);
    }
    Push(func);
    var call = new CallInstruction(args.Length, currentStatus);
    ExecuteInstruction(call);
  }
  public object VisitCallExpr(Expr.Call expr)
  {
    var callee = Evaluate(expr.Callee);
    if (callee is not ICallable function)
    {
      throw Error("Can only call functions and classes.");
    }
    if (function is not NativeFunction && expr.Arguments.Count != function.Arity)
      throw Error($"{function.Name} erwartet {function.Arity} Argumente, übergeben wurden aber {expr.Arguments.Count}.");
    if (callStack.Count >= MaxStackDepth)
      throw Error("Zu viele verschachtelte Funktionsaufrufe.");
    var arguments = new object[expr.Arguments.Count];
    for (int i = 0; i < expr.Arguments.Count; i++)
    {
      var argument = expr.Arguments[i];
      arguments[i] = Evaluate(argument);
    }
    object retVal = CallFunction(function, arguments);
    return retVal;
  }
  public object CallFunction(ICallable function, object[] arguments)
  {
    var csi = new CallStackInfo(this, function, environment);
    callStack.Push(csi);
    var retVal = function.Call(this, arguments);
    callStack.Pop();
    return retVal;
  }

  public object VisitGetExpr(Expr.Get expr)
  {
    var obj = Evaluate(expr.Obj);
    if (obj is EleuInstance inst)
    {
      return inst.Get(expr.Name, false);
    }
    throw Error("Only instances have properties.");
  }
  public object VisitGroupingExpr(Expr.Grouping expr) => Evaluate(expr.Expression);
  public object VisitLiteralExpr(Expr.Literal expr)
  {
    if (expr.Value == null) return NilValue;
    if (expr.Value is EleuList l)
    {
      if (l.Len == 0) return l;
      var resultList = new EleuList();
      foreach (var item in l)
      {
        var o = Evaluate((Expr)item);
        resultList.Add(o);
      }
      return resultList;
    }
    return expr.Value;
  }
  public object VisitLogicalExpr(Expr.Logical expr)
  {
    var left = Evaluate(expr.Left);
    if (left is not bool) throw Error(Wrong_Op_Arg(expr.Op, left));
    var right = Evaluate(expr.Right);
    if (right is not bool) throw Error(Wrong_Op_Arg(expr.Op, right));
    if (expr.Op.Type == TokenOr)
    {
      if (IsTruthy(left)) return left;
    }
    else
    {
      if (IsFalsey(left)) return left;
    }
    return right;
  }
  public object VisitSetExpr(Expr.Set expr)
  {
    var obj = Evaluate(expr.Obj);
    if (obj is not EleuInstance li)
    {
      throw Error("Only instances have fields.");
    }
    var value = Evaluate(expr.Value);
    li.Set(expr.Name, value);
    return value;
  }
  public object VisitSuperExpr(Expr.Super expr)
  {
    int distance = expr.localDistance;
    EleuClass superclass = (EleuClass)environment.GetAt("super", distance);
    EleuInstance obj = (EleuInstance)environment.GetAt("this", distance - 1);
    EleuFunction? method = superclass.FindMethod(expr.Method) as EleuFunction;
    if (method == null)
    {
      throw Error("Undefined property '" + expr.Method + "'.");
    }
    return method.Bind(obj, false);
  }
  public object VisitThisExpr(Expr.This expr)
  {
    return LookUpVariable(expr.Keyword, expr.localDistance);
  }
  public object VisitUnaryExpr(Expr.Unary expr)
  {
    var right = Evaluate(expr.Right);
    switch (expr.Op.Type)
    {
      case TokenBang:
        if (right is not bool) throw Error("Operand muss vom Typ boolean sein.");
        return !IsTruthy(right);
      case TokenMinus:
        {
          if (right is not Number d) throw Error("Operand muss eine Zahl sein.");
          return -d;
        }
      default: throw Error("Unknown op type: " + expr.Op.Type);// Unreachable.
    };
  }
  internal void resolve(Expr expr, int depth)
  {
    //locals[expr] = depth;
    expr.localDistance = depth;
  }
  public object VisitVariableExpr(Expr.Variable expr) => LookUpVariable(expr.Name, expr.localDistance);
  internal object LookUpVariable(string name, int distance)
  {
    if (distance >= 0)
    {
      return environment.GetAt(name, distance);
    }
    else
    {
      return globals.Lookup(name);
    }
  }

  public InterpretResult VisitBlockStmt(Stmt.Block stmt) => ExecuteBlock(stmt.Statements, new EleuEnvironment(environment));

  internal InterpretResult ExecuteBlock(List<Stmt> statements, EleuEnvironment environment)
  {
    var previous = this.environment;
    InterpretResult result = InterpretResult.NilResult;
    try
    {
      this.environment = environment;
      foreach (Stmt statement in statements)
      {
        result = Execute(statement);
        if (result.Stat != InterpretResult.Status.Normal)
          break;
      }
      return result;
    }
    finally
    {
      this.environment = previous;
    }
  }

  public InterpretResult VisitClassStmt(Stmt.Class stmt)
  {
    EleuClass? superclass = null;
    if (stmt.Superclass != null)
    {
      var superclassV = Evaluate(stmt.Superclass);
      if (superclassV is not EleuClass)
      {
        throw new EleuRuntimeError("Superclass must be a class.");
      }
      superclass = superclassV as EleuClass;
    }

    if (environment.GetAtDistance0(stmt.Name) is not EleuClass klass)
    {
      environment.Define(stmt.Name, NilValue);
      klass = new EleuClass(stmt.Name, superclass);
    }
    else
    {
      if (klass.Superclass != null && klass.Superclass != superclass)
        throw new EleuRuntimeError(stmt.Status,
          $"Super class must be the same ({klass.Superclass.Name} vs. {superclass?.Name})");
    }
    if (superclass != null)
    {
      environment = new EleuEnvironment(environment);
      environment.Define("super", superclass);
    }

    //klass = new EleuClass(stmt.Name, superclass);
    foreach (Stmt.Function method in stmt.Methods)
    {
      EleuFunction function = new EleuFunction(method, environment, method.Name == "init");
      klass.Methods.Set(method.Name, function);
    }
    var kval = klass;
    if (superclass != null)
    {
      environment = environment.enclosing!;
    }
    environment.Assign(stmt.Name, kval);
    return new InterpretResult(kval);
  }
  public InterpretResult VisitExpressionStmt(Stmt.Expression stmt)
  {
    var evRes = Evaluate(stmt.expression);
    if (evRes is ICallable call && stmt.expression is Expr.Variable)
      throw Error(Func_Call_Missing_Paren(call.Name));
    return new(evRes);
  }
  public InterpretResult VisitFunctionStmt(Stmt.Function stmt)
  {
    EleuFunction function = new EleuFunction(stmt, environment, false);
    environment.Define(stmt.Name, function);
    return new(function);
  }
  public InterpretResult VisitIfStmt(Stmt.If stmt)
  {
    var cond = Evaluate(stmt.Condition);
    if (cond is not bool b) throw Error($"Die if-Bedingung '{cond}' ist nicht vom Typ boolean");
    if (IsTruthy(b))
      return Execute(stmt.ThenBranch);
    else if (stmt.ElseBranch != null)
      return Execute(stmt.ElseBranch);
    return InterpretResult.NilResult;
  }
  public InterpretResult VisitReturnStmt(Stmt.Return stmt)
  {
    var val = NilValue;
    if (stmt.Value != null) val = Evaluate(stmt.Value);
    return new InterpretResult(val, InterpretResult.Status.Return);
  }

  public InterpretResult VisitVarStmt(Stmt.Var stmt)
  {
    var value = NilValue;
    if (stmt.Initializer != null)
    {
      value = Evaluate(stmt.Initializer);
    }
    if (environment.ContainsAtDistance0(stmt.Name))
      throw new EleuRuntimeError($"Mehrfache var-Anweisung: '{stmt.Name}' wurde bereits deklariert!");
    environment.Define(stmt.Name, value);
    return new(value);
  }
  public InterpretResult VisitWhileStmt(Stmt.While stmt)
  {
    var result = InterpretResult.NilResult;
    while (IsTruthy(Evaluate(stmt.Condition)))
    {
      result = Execute(stmt.Body);
      if (result.Stat == InterpretResult.Status.Break)
      {
        result = InterpretResult.NilResult;
        break;
      }
      if (result.Stat == InterpretResult.Status.Continue ||
          result.Stat == InterpretResult.Status.Normal)
      {
        if (stmt.Increment != null) Evaluate(stmt.Increment!);
        continue;
      }

      if (result.Stat != InterpretResult.Status.Normal) break;
    }
    return result;
  }
  public InterpretResult VisitAssertStmt(Stmt.Assert stmt)
  {
    bool fail = false;
    try
    {
      var val = Evaluate(stmt.expression);
      if (IsFalsey(val)) fail = true;
    }
    catch (EleuRuntimeError ex)
    {
      if (stmt.isErrorAssert)
      {
        if (stmt.message == null || stmt.message == ex.Message)
          return new(NilValue);
      }
      throw;
    }
    var msg = stmt.message ?? "Eine Annahme ist fehlgeschlagen.";
    if (stmt.isErrorAssert)
    {
      fail = true;
      msg += " Es wurde eine RuntimeException erwartet!";
    }
    if (fail)
      throw new EleuAssertionFail(stmt.expression.Status, msg);
    return new(NilValue);
  }
  public InterpretResult VisitBreakContinueStmt(Stmt.BreakContinue stmt)
  {
    return stmt.IsBreak ? InterpretResult.BreakResult : InterpretResult.ContinueResult;
  }
  public IEnumerable<VariableInfo> GetGlobalVariablesAndValues()
  {
    return globals.GetVariableInfos(null);
  }
  public List<CallStackInfo> GetCallStack()
  {
    var l = callStack.ToList();
    //l.Reverse();
    return l;
  }
  public InterpretResult VisitRepeatStmt(Stmt.Repeat stmt)
  {
    var result = InterpretResult.NilResult;
    int? GetCount()
    {
      var count = Evaluate(stmt.Count);
      if (count is not Number num) return null;
      if (!num.IsInt) return null;
      return num.IntValue;
    }
    var count = GetCount();
    if (!count.HasValue) throw new EleuRuntimeError(stmt.Count.Status, "Es wird eine natürliche Zahl erwartet.");

    for (int i = 0; i < count.Value; i++)
    {
      result = Execute(stmt.Body);
      if (result.Stat == InterpretResult.Status.Continue)
      {
        continue;
      }
      if (result.Stat == InterpretResult.Status.Break)
      {
        result = InterpretResult.NilResult;
        break;
      }
      if (result.Stat != InterpretResult.Status.Normal)
        break;
    }
    return result;
  }


}
