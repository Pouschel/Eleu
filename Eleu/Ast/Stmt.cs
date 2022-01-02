//> Appendix II stmt
namespace Eleu.Ast;

public abstract class Stmt {
  public interface Visitor<R> {
    R visitBlockStmt(Block stmt);
    R visitClassStmt(Class stmt);
    R visitExpressionStmt(Expression stmt);
    R visitFunctionStmt(Function stmt);
    R visitIfStmt(If stmt);
    R visitPrintStmt(Print stmt);
    R visitReturnStmt(Return stmt);
    R visitVarStmt(Var stmt);
    R visitWhileStmt(While stmt);
  }

  public abstract R accept<R>(Visitor<R> visitor);

  // Nested Stmt classes here...
//> stmt-block
  public class Block : Stmt {
    Block(List<Stmt> statements) {
      this.statements = statements;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitBlockStmt(this);
    }

    readonly List<Stmt> statements;
  }
//< stmt-block
//> stmt-class
  public class Class : Stmt {
    Class(Token name,
          Expr.Variable superclass,
          List<Stmt.Function> methods) {
      this.name = name;
      this.superclass = superclass;
      this.methods = methods;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitClassStmt(this);
    }

    readonly Token name;
    readonly Expr.Variable superclass;
    readonly List<Stmt.Function> methods;
  }
//< stmt-class
//> stmt-expression
  public class Expression : Stmt {
    Expression(Expr expression) {
      this.expression = expression;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitExpressionStmt(this);
    }

    readonly Expr expression;
  }
//< stmt-expression
//> stmt-function
  public class Function : Stmt {
    Function(Token name, List<Token> paras, List<Stmt> body) {
      this.name = name;
      this.paras = paras;
      this.body = body;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitFunctionStmt(this);
    }

    readonly Token name;
    readonly List<Token> paras;
    readonly List<Stmt> body;
  }
//< stmt-function
//> stmt-if
  public class If : Stmt {
    If(Expr condition, Stmt thenBranch, Stmt elseBranch) {
      this.condition = condition;
      this.thenBranch = thenBranch;
      this.elseBranch = elseBranch;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitIfStmt(this);
    }

    readonly Expr condition;
    readonly Stmt thenBranch;
    readonly Stmt elseBranch;
  }
//< stmt-if
//> stmt-print
  public class Print : Stmt {
    Print(Expr expression) {
      this.expression = expression;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitPrintStmt(this);
    }

    readonly Expr expression;
  }
//< stmt-print
//> stmt-return
  public class Return : Stmt {
    Return(Token keyword, Expr value) {
      this.keyword = keyword;
      this.value = value;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitReturnStmt(this);
    }

    readonly Token keyword;
    readonly Expr value;
  }
//< stmt-return
//> stmt-var
  public class Var : Stmt {
    Var(Token name, Expr initializer) {
      this.name = name;
      this.initializer = initializer;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitVarStmt(this);
    }

    readonly Token name;
    readonly Expr initializer;
  }
//< stmt-var
//> stmt-while
  public class While : Stmt {
    While(Expr condition, Stmt body) {
      this.condition = condition;
      this.body = body;
    }

    public override R accept<R>(Visitor<R> visitor) {
      return visitor.visitWhileStmt(this);
    }

    readonly Expr condition;
    readonly Stmt body;
  }
//< stmt-while
}
