// This file was generated by a tool. Do not edit!
// AST classes for stmt
namespace Eleu.Ast;

public abstract class Stmt : ExprStmtBase {

  public interface Visitor<R> {
    R VisitBlockStmt(Block stmt);
    R VisitClassStmt(Class stmt);
    R VisitExpressionStmt(Expression stmt);
    R VisitFunctionStmt(Function stmt);
    R VisitIfStmt(If stmt);
    R VisitAssertStmt(Assert stmt);
    R VisitReturnStmt(Return stmt);
    R VisitBreakContinueStmt(BreakContinue stmt);
    R VisitVarStmt(Var stmt);
    R VisitWhileStmt(While stmt);
    R VisitRepeatStmt(Repeat stmt);
  }

  public abstract R Accept<R>(Visitor<R> visitor);

  // Nested Stmt classes here...
  // stmt-block
  public class Block : Stmt {
    public readonly List<Stmt> Statements;

    internal Block(List<Stmt> Statements) {
      this.Statements = Statements;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitBlockStmt(this);
    }
  }
  // stmt-class
  public class Class : Stmt {
    public readonly string Name;
    public readonly Expr.Variable? Superclass;
    public readonly List<Stmt.Function> Methods;

    internal Class(string Name,
          Expr.Variable? Superclass,
          List<Stmt.Function> Methods) {
      this.Name = Name;
      this.Superclass = Superclass;
      this.Methods = Methods;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitClassStmt(this);
    }
  }
  // stmt-expression
  public class Expression : Stmt {
    public readonly Expr expression;

    internal Expression(Expr expression) {
      this.expression = expression;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitExpressionStmt(this);
    }
  }
  // stmt-function
  public class Function : Stmt {
    public readonly FunctionType Type;
    public readonly string Name;
    public readonly List<Token> Paras;
    public readonly List<Stmt> Body;

    internal Function(FunctionType Type,
          string Name,
          List<Token> Paras,
          List<Stmt> Body) {
      this.Type = Type;
      this.Name = Name;
      this.Paras = Paras;
      this.Body = Body;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitFunctionStmt(this);
    }
  }
  // stmt-if
  public class If : Stmt {
    public readonly Expr Condition;
    public readonly Stmt ThenBranch;
    public readonly Stmt? ElseBranch;

    internal If(Expr Condition, Stmt ThenBranch, Stmt? ElseBranch) {
      this.Condition = Condition;
      this.ThenBranch = ThenBranch;
      this.ElseBranch = ElseBranch;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitIfStmt(this);
    }
  }
  // stmt-assert
  public class Assert : Stmt {
    public readonly Expr expression;
    public readonly string? message;
    public readonly bool isErrorAssert;

    internal Assert(Expr expression, string? message, bool isErrorAssert) {
      this.expression = expression;
      this.message = message;
      this.isErrorAssert = isErrorAssert;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitAssertStmt(this);
    }
  }
  // stmt-return
  public class Return : Stmt {
    public readonly Token Keyword;
    public readonly Expr? Value;

    internal Return(Token Keyword, Expr? Value) {
      this.Keyword = Keyword;
      this.Value = Value;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitReturnStmt(this);
    }
  }
  // stmt-breakcontinue
  public class BreakContinue : Stmt {
    public readonly bool IsBreak;

    internal BreakContinue(bool IsBreak) {
      this.IsBreak = IsBreak;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitBreakContinueStmt(this);
    }
  }
  // stmt-var
  public class Var : Stmt {
    public readonly string Name;
    public readonly Expr? Initializer;

    internal Var(string Name, Expr? Initializer) {
      this.Name = Name;
      this.Initializer = Initializer;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitVarStmt(this);
    }
  }
  // stmt-while
  public class While : Stmt {
    public readonly Expr Condition;
    public readonly Stmt Body;
    public readonly Expr? Increment;

    internal While(Expr Condition, Stmt Body, Expr? Increment) {
      this.Condition = Condition;
      this.Body = Body;
      this.Increment = Increment;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitWhileStmt(this);
    }
  }
  // stmt-repeat
  public class Repeat : Stmt {
    public readonly Expr Count;
    public readonly Stmt Body;

    internal Repeat(Expr Count, Stmt Body) {
      this.Count = Count;
      this.Body = Body;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitRepeatStmt(this);
    }
  }
}
