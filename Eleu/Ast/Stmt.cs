//> Appendix II stmt
namespace Eleu.Ast;

public abstract class Stmt {
  public interface Visitor<R> {
    R VisitBlockStmt(Block stmt);
    R VisitClassStmt(Class stmt);
    R VisitExpressionStmt(Expression stmt);
    R VisitFunctionStmt(Function stmt);
    R VisitIfStmt(If stmt);
    R VisitPrintStmt(Print stmt);
    R VisitReturnStmt(Return stmt);
    R VisitVarStmt(Var stmt);
    R VisitWhileStmt(While stmt);
  }

  public abstract R Accept<R>(Visitor<R> visitor);

  // Nested Stmt classes here...
//> stmt-block
  public class Block : Stmt {
    public readonly List<Stmt> statements;

    internal Block(List<Stmt> statements) {
      this.statements = statements;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitBlockStmt(this);
    }
  }
//< stmt-block
//> stmt-class
  public class Class : Stmt {
    public readonly string name;
    public readonly Expr.Variable? superclass;
    public readonly List<Stmt.Function> methods;

    internal Class(string name,
          Expr.Variable? superclass,
          List<Stmt.Function> methods) {
      this.name = name;
      this.superclass = superclass;
      this.methods = methods;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitClassStmt(this);
    }
  }
//< stmt-class
//> stmt-expression
  public class Expression : Stmt {
    public readonly Expr expression;

    internal Expression(Expr expression) {
      this.expression = expression;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitExpressionStmt(this);
    }
  }
//< stmt-expression
//> stmt-function
  public class Function : Stmt {
    public readonly FunctionType type;
    public readonly string name;
    public readonly List<Token> paras;
    public readonly List<Stmt> body;

    internal Function(FunctionType type,
          string name,
          List<Token> paras,
          List<Stmt> body) {
      this.type = type;
      this.name = name;
      this.paras = paras;
      this.body = body;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitFunctionStmt(this);
    }
  }
//< stmt-function
//> stmt-if
  public class If : Stmt {
    public readonly Expr condition;
    public readonly Stmt thenBranch;
    public readonly Stmt? elseBranch;

    internal If(Expr condition, Stmt thenBranch, Stmt? elseBranch) {
      this.condition = condition;
      this.thenBranch = thenBranch;
      this.elseBranch = elseBranch;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitIfStmt(this);
    }
  }
//< stmt-if
//> stmt-print
  public class Print : Stmt {
    public readonly Expr expression;

    internal Print(Expr expression) {
      this.expression = expression;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitPrintStmt(this);
    }
  }
//< stmt-print
//> stmt-return
  public class Return : Stmt {
    public readonly Token keyword;
    public readonly Expr? value;

    internal Return(Token keyword, Expr? value) {
      this.keyword = keyword;
      this.value = value;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitReturnStmt(this);
    }
  }
//< stmt-return
//> stmt-var
  public class Var : Stmt {
    public readonly string name;
    public readonly Expr? initializer;

    internal Var(string name, Expr? initializer) {
      this.name = name;
      this.initializer = initializer;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitVarStmt(this);
    }
  }
//< stmt-var
//> stmt-while
  public class While : Stmt {
    public readonly Expr condition;
    public readonly Stmt body;

    internal While(Expr condition, Stmt body) {
      this.condition = condition;
      this.body = body;
    }

    public override R Accept<R>(Visitor<R> visitor) {
      return visitor.VisitWhileStmt(this);
    }
  }
//< stmt-while
}
