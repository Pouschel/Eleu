﻿using System.Xml.Linq;
using Eleu.Types;

using static Eleu.Interpret.InterpretResult;

namespace Eleu.Interpret;

record CallFrame(Chunk chunk, ICallable? func = null)
{
  public int ip = 0;
  public CallFrame? next;

  public Instruction? NextInstruction()
  {
    if (ip >= chunk.Count) return null;
    return chunk.code[ip++];
  }
}

abstract record class Instruction(InputStatus status)
{
  public abstract void Execute(Interpreter vm);

}
record PushInstruction(object value, InputStatus stat) : Instruction(stat)
{
  public override void Execute(Interpreter vm)
  {
    vm.Push(value);
  }
  public override string ToString() => $"push {value}";
}

record PopInstruction(InputStatus stat) : Instruction(stat)
{
  public bool DisallowFunctionPop = false;
  public override void Execute(Interpreter vm)
  {
    var o = vm.Pop();
    if (DisallowFunctionPop && o is ICallable call) 
      throw vm.Error(Func_Call_Missing_Paren(call.Name));
  }
  public override string ToString() => "pop";
}

record CallInstruction(int nArgs, InputStatus status) : Instruction(status)
{
  public override void Execute(Interpreter vm)
  {
    var callee = vm.Pop();
    if (callee is NativeFunction nfunc)
    {
      ExecuteNative(vm, nfunc);
      return;
    }
    if (callee is not ICallable function) throw vm.Error("Can only call functions and classes.");
    if (nArgs != function.Arity)
      throw vm.Error($"{function.Name} erwartet {function.Arity} Argumente, übergeben wurden aber {nArgs}.");
    if (callee is EleuFunction efunc)
    {
      var environment = new EleuEnvironment(efunc.closure);
      DoCall(vm, environment, efunc);
      return;
    }
    if (callee is EleuClass cls)
    {
      var instance = new EleuInstance(cls);
      var initializer = cls.FindMethod("init");
      if (initializer is not EleuFunction ifunc)
      {
        vm.Push(instance);
        return;
      }
      ifunc = ifunc.Bind(instance, true);

      var environment = new EleuEnvironment(ifunc.closure);
      // environment.Define("this", instance);
      DoCall(vm, environment, ifunc);
      return;
    }
    throw new NotSupportedException("message");
  }

  void DoCall(Interpreter vm, EleuEnvironment environment, EleuFunction callee)
  {
    for (int i = nArgs - 1; i >= 0; i--)
    {
      environment.Define(callee.declaration.Paras[i].StringValue, vm.Pop());
    }
    vm.EnterEnv(environment);
    var frame = new CallFrame(callee.compiledChunk, func: callee);
    vm.EnterFrame(frame);
  }

  void ExecuteNative(Interpreter vm, NativeFunction callee)
  {
    var arguments = new object[nArgs];
    for (int i = 0; i < nArgs; i++)
    {
      var argument = vm.Pop();
      arguments[nArgs - i - 1] = argument;
    }
    var res = vm.CallFunction(callee, arguments);
    vm.Push(res);
  }


  public override string ToString() => $"call/{nArgs}";
}

record LookupVarInstruction(string name, int distance, InputStatus status) : Instruction(status)
{

  public override void Execute(Interpreter vm)
  {
    var value = vm.LookUpVariable(name, distance);
    vm.Push(value);
  }

  public override string ToString() => $"get_value@{distance} '{name}'";
}

record AssignInstruction(string name, int distance, InputStatus status) : Instruction(status)
{

  public override void Execute(Interpreter vm)
  {
    var value = vm.Peek();
    vm.AssignAtDistance(name, distance, value);
  }

  public override string ToString() => $"assign@{distance} {name}";
}


record BinaryOpInstruction(TokenType op, InputStatus status) : Instruction(status)
{

  public override void Execute(Interpreter vm)
  {
    var rhs = vm.Pop();
    var lhs = vm.Pop();
    var result = NilValue;
    result = op switch
    {
      TokenType.TokenBangEqual => !ObjEquals(lhs, rhs),
      TokenType.TokenEqualEqual => ObjEquals(lhs, rhs),
      TokenType.TokenGreater => InternalCompare(lhs, rhs) > 0,
      TokenType.TokenGreaterEqual => InternalCompare(lhs, rhs) >= 0,
      TokenType.TokenLess => InternalCompare(lhs, rhs) < 0,
      TokenType.TokenLessEqual => InternalCompare(lhs, rhs) <= 0,
      TokenType.TokenPlus => NumStrAdd(lhs, rhs),
      TokenType.TokenMinus => NumSubtract(lhs, rhs),
      TokenType.TokenStar => NumberOp("*", lhs, rhs, (a, b) => a * b),
      TokenType.TokenPercent => NumberOp("%", lhs, rhs, (a, b) => a % b),
      TokenType.TokenSlash => NumberOp("/", lhs, rhs, (a, b) => a / b),
      _ => throw new EleuRuntimeError(status, $"Invalid op: {op}")
    };
    vm.Push(result);
  }
  public override string ToString() => $"op {op}";
}

record GetInstruction(string name, InputStatus status) : Instruction(status)
{
  public override void Execute(Interpreter vm)
  {
    var obj = vm.Pop();
    if (obj is EleuInstance inst)
    {
      var val = inst.Get(name, true);
      vm.Push(val);
      return;
    }
    throw vm.Error("Only instances have properties.");
  }
  public override string ToString() => $"get {name}";
}

record LogicalOpInstruction(Token op, InputStatus status) : Instruction(status)
{
  public override void Execute(Interpreter vm)
  {
    var right = vm.Pop();
    if (right is not bool)
      throw new EleuRuntimeError(status, Wrong_Op_Arg(op, right));
    var left = vm.Pop();
    if (left is not bool)
      throw new EleuRuntimeError(status, Wrong_Op_Arg(op, left));
    if (op.Type == TokenType.TokenOr)
    {
      if (IsTruthy(left))
      {
        vm.Push(left);
        return;
      }
    }
    else
    {
      if (IsFalsey(left))
      {
        vm.Push(left);
        return;
      }
    }
    vm.Push(right);
  }

  public override string ToString() => op.StringValue;
}

record SetInstruction(string name, InputStatus status) : Instruction(status)
{
  public override void Execute(Interpreter vm)
  {
    var value = vm.Pop();
    var obj = vm.Pop();
    if (obj is not EleuInstance inst)
      throw vm.Error("Only instances have fields.");
    inst.Set(name, value);
    vm.Push(value);
  }
  public override string ToString() => $"set_field {name}";
}

record SuperInstruction(string name, int distance, InputStatus status) : Instruction(status)
{
  public override void Execute(Interpreter vm)
  {
    if (vm.environment.GetAt("super", distance) is not EleuClass superclass)
      throw vm.Error("No superclass found");
    if (vm.environment.GetAt("this", distance - 1) is not EleuInstance obj)
      throw vm.Error("No this found");
    var method = superclass.FindMethod(name);
    if (method is not EleuFunction func)
      throw vm.Error($"Undefined property '{name}'.");
    vm.Push(func.Bind(obj, true));
  }
}

record UnaryOpInstruction(TokenType type, InputStatus status) : Instruction(status)
{
  public override void Execute(Interpreter vm)
  {
    var right = vm.Pop();
    switch (type)
    {
      case TokenType.TokenBang:
        if (right is not bool)
          throw new EleuRuntimeError(status, "Operand muss vom Typ boolean sein.");
        vm.Push(!IsTruthy(right));
        break;
      case TokenType.TokenMinus:
        {
          if (right is not Number num)
            throw new EleuRuntimeError(status, "Operand muss eine Zahl sein.");
          vm.Push(-num);
        }
        break;
      default:
        throw new EleuRuntimeError(status, $"Unknown op type: {type}"); // Unreachable.
    }
  }
}

record AssertInstruction(InputStatus status) : Instruction(status)
{
  public override void Execute(Interpreter vm)
  {
    var val = vm.Pop();
    if (IsFalsey(val))
      throw new EleuAssertionFail(status, "Eine Annahme ist fehlgeschlagen.");
  }
}

record ScopeInstruction(bool begin) : Instruction(InputStatus.Empty)
{
  public override void Execute(Interpreter vm)
  {
    if (begin)
    {
      var env = new EleuEnvironment(vm.environment);
      vm.EnterEnv(env);
    }
    else
      vm.LeaveEnv();
  }

  public override string ToString() => begin ? "enter_scope" : "leave_scope";
}

record VarDefInstruction(string name, InputStatus status1) : Instruction(status1)
{

  public override void Execute(Interpreter vm)
  {
    if (vm.environment.ContainsAtDistance0(name))
      throw new EleuRuntimeError(status, $"Mehrfache var-Anweisung: '{name}' wurde bereits deklariert!");
    var value = vm.Pop();
    vm.environment.Define(name, value);
  }
  public override string ToString() => $"def var {name}";
}


enum JumpMode
{
  jmp,
  jmp_true,
  jmp_false,
  jmp_le_zero,
}

record JumpInstruction : Instruction
{
  public int offset;
  public JumpMode mode = JumpMode.jmp;
  public int leaveScopes = 0;

  public JumpInstruction(JumpMode mode, InputStatus status) : base(status)
  {
    this.mode = mode;
  }

  public override void Execute(Interpreter vm)
  {
    for (var i = 0; i < leaveScopes; i++)
    {
      vm.LeaveEnv();
    }
    if (mode == JumpMode.jmp)
    {
      vm.frame!.ip = offset;
      return;
    }
    var val = vm.Peek();

    int? GetCount()
    {
      if (val is not Number num) return null;
      if (!num.IsInt) return null;
      return num.IntValue;
    }

    switch (mode)
    {
      case JumpMode.jmp_true:
        if (!IsTruthy(val)) return;
        break;
      case JumpMode.jmp_false:
        if (!IsFalsey(val)) return;
        break;
      case JumpMode.jmp_le_zero:
        var count = GetCount();
        if (count is not int)
          throw new EleuRuntimeError(status, "Es wird eine natürliche Zahl erwartet.");
        if (count > 0) return;
        break;
      default:
        throw new NotSupportedException("invalid jump code");
    }
    vm.frame!.ip = offset;
  }


  public override string ToString() => $"{mode} {offset}";
}

record DefFunInstruction(Stmt.Function func) : Instruction(InputStatus.Empty)
{
  public override void Execute(Interpreter vm)
  {
    EleuFunction function = new EleuFunction(func, vm.environment, false);
    vm.environment.Define(func.Name, function);
  }
}

record ReturnInstruction(int scopeDepth, InputStatus status) : Instruction(status)
{
  public override void Execute(Interpreter vm)
  {
    for (var i = 0; i < scopeDepth; i++)
    {
      vm.LeaveEnv();
    }
    vm.LeaveFrame();
  }
}

record ClassInstruction(string clsName, List<Stmt.Function> methods, InputStatus status) : Instruction(status)
{
  public override void Execute(Interpreter vm)
  {
    if (vm.Pop() is not bool hasSuper) throw new NotSupportedException();
    EleuClass? superclass = null;
    if (hasSuper)
    {
      var superclassV = vm.Pop();
      if (superclassV is not EleuClass sucv)
      {
        throw vm.Error("Superclass must be a class.");
      }
      superclass = sucv;
    }
    var klss = vm.environment.GetAtDistance0(clsName);
    if (klss is not EleuClass klass)
    {
      vm.environment.Define(clsName, NilValue);
      klass = new EleuClass(clsName, superclass);
    }
    else
    {
      if (klass.Superclass != null && klass.Superclass != superclass)
        throw new EleuRuntimeError(status,
          $"Super class must be the same ({klass.Superclass?.Name} vs. {superclass?.Name})");
    }
    if (superclass != null)
    {
      vm.environment = new EleuEnvironment(vm.environment);
      vm.environment.Define("super", superclass);
    }
    foreach (var method in methods)
    {
      EleuFunction function = new EleuFunction(method, vm.environment, method.Name == "init");
      klass.Methods.Set(method.Name, function);
    }
    var kval = klass;
    if (superclass != null)
    {
      vm.environment = vm.environment.enclosing!;
    }
    vm.environment.Assign(clsName, kval);
  }

  public override string ToString() => $"def_class {clsName}";
}
