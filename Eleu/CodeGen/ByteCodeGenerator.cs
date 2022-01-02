using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Eleu.CodeGen
{
	internal class ByteCodeGenerator: Expr.Visitor<bool>, Stmt.Visitor<bool>
	{
		string fileName;
		EleuOptions options;
		CompilerState current;
		EleuResult result;
		Chunk CurrentChunk => current.function.chunk;
		public ByteCodeGenerator(string fileName, EleuOptions options, EleuResult result)
		{
			this.options = options;
			this.fileName = fileName;
			this.result = result;
			current = new CompilerState(FunTypeScript);
		}

		public EleuResult GenCode()
		{
			foreach (var stm in result.Expr!)
			{
				stm.Accept(this);
			}
			var funct = EndCompiler();
			result.Function = funct;
			return result;
		}
		ObjFunction EndCompiler()
		{
			EmitReturn();
			var function = current.function;
			if (options.PrintByteCode)
			{
				CurrentChunk.Disassemble(function.NameOrScript, null, options.Err);
			}
			current = current.enclosing!;
			return function;
		}

		public bool VisitAssignExpr(Expr.Assign expr)
		{
			expr.value.Accept(this);
			var name = expr.name;
			OpCode setOp;
			int arg = ResolveLocal(current, name);
			if (arg != -1)
			{
				setOp = OP_SET_LOCAL;
			}
			else if ((arg = ResolveUpvalue(current, name)) != -1)
			{
				setOp = OP_SET_UPVALUE;
			}
			else
			{
				arg = IdentifierConstant(name);
				setOp = OP_SET_GLOBAL;
			}
			return EmitBytes(setOp, (byte)arg);
		}

		public bool VisitBinaryExpr(Expr.Binary expr)
		{
			expr.left.Accept(this);
			expr.right.Accept(this);
			switch (expr.op.type)
			{
				case TOKEN_BANG_EQUAL: EmitBytes(OP_EQUAL, (byte)OP_NOT); break;
				case TOKEN_EQUAL_EQUAL: EmitByte(OP_EQUAL); break;
				case TOKEN_GREATER: EmitByte(OP_GREATER); break;
				case TOKEN_GREATER_EQUAL: EmitBytes(OP_LESS, (byte)OP_NOT); break;
				case TOKEN_LESS: EmitByte(OP_LESS); break;
				case TOKEN_LESS_EQUAL: EmitBytes(OP_GREATER, (byte)OP_NOT); break;
				case TokenPlus: EmitByte(OP_ADD); break;
				case TokenMinus: EmitByte(OP_SUBTRACT); break;
				case TokenStar: EmitByte(OP_MULTIPLY); break;
				case TokenPercent: EmitByte(OP_REMAINDER); break;
				case TOKEN_SLASH: EmitByte(OP_DIVIDE); break;
				default: return false; // Unreachable.
			}
			return true;
		}

		public bool VisitCallExpr(Expr.Call expr) => throw new NotImplementedException();
		public bool VisitGetExpr(Expr.Get expr) => throw new NotImplementedException();
		public bool VisitGroupingExpr(Expr.Grouping expr) => expr.expression.Accept(this);

		public bool VisitLiteralExpr(Expr.Literal expr)
		{
			switch (expr.value)
			{
				case bool b: EmitByte(b ? OP_TRUE : OP_FALSE); break;
				case null: EmitByte(OP_NIL); break;
				case double d: EmitConstant(CreateNumberVal(d)); break;
				case string s: EmitConstant(CreateStringVal(s)); break;
				default: Error($"Unsupported constant of type: {expr.value}"); return false;
			}
			return true;
		}

		public bool VisitLogicalExpr(Expr.Logical expr) => throw new NotImplementedException();
		public bool VisitSetExpr(Expr.Set expr) => throw new NotImplementedException();
		public bool VisitSuperExpr(Expr.Super expr) => throw new NotImplementedException();
		public bool VisitThisExpr(Expr.This expr) => throw new NotImplementedException();
		public bool VisitUnaryExpr(Expr.Unary expr)
		{
			switch (expr.op.type)
			{
				case TOKEN_BANG: EmitByte(OP_NOT); break;
				case TokenMinus: EmitByte(OP_NEGATE); break;
				default: return false; // Unreachable.
			}
			return true;
		}

		public bool VisitVariableExpr(Expr.Variable expr)
		{
			var name = expr.name;
			OpCode getOp;
			int arg = ResolveLocal(current, name);
			if (arg != -1)
			{
				getOp = OP_GET_LOCAL;
			}
			else if ((arg = ResolveUpvalue(current, name)) != -1)
			{
				getOp = OP_GET_UPVALUE;
			}
			else
			{
				arg = IdentifierConstant(name);
				getOp = OP_GET_GLOBAL;
			}
			return EmitBytes(getOp, (byte)arg);
		}

		CompilerState InitCompiler(FunctionType type, string funName)
		{
			var compiler = new CompilerState(type);
			compiler.enclosing = current;
			if (type != FunTypeScript)
				compiler.function.name = funName;
			return compiler;
		}

		void EmitReturn()
		{
			if (current.type == FunTypeInitializer)
				EmitBytes(OP_GET_LOCAL, 0);
			else
			{
				EmitByte(OP_NIL);
			}
			EmitByte(OP_RETURN);
		}

		bool EmitByte(byte by)
		{
			CurrentChunk.Write(by); return true;
		}

		bool EmitByte(OpCode op)
		{
			CurrentChunk.Write(op); return true;
		}

		bool EmitBytes(OpCode byte1, byte byte2)
		{
			EmitByte(byte1);
			return EmitByte(byte2);
		}

		void EmitConstant(Value value) => EmitBytes(OP_CONSTANT, MakeConstant(value));
		byte MakeConstant(Value value)
		{
			int constant = CurrentChunk.AddConstant(value);
			if (constant > byte.MaxValue)
			{
				Error("Too many constants in one chunk.");
				return 0;
			}
			return (byte)constant;
		}
		byte ParseVariable(Stmt.Var vstm)
		{
			DeclareVariable(vstm.name.StringValue);
			if (current.scopeDepth > 0) return 0;
			return IdentifierConstant(vstm.name);
		}
		byte IdentifierConstant(Token name) => MakeConstant(new Value(name.StringValue));

		void DefineVariable(byte global)
		{
			if (current.scopeDepth > 0)
			{
				MarkInitialized();
				return;
			}
			EmitBytes(OP_DEFINE_GLOBAL, global);
		}
		void MarkInitialized()
		{
			if (current.scopeDepth == 0) return;
			current.locals[current.localCount - 1].depth = current.scopeDepth;
		}
		void DeclareVariable(string name)
		{
			if (current.scopeDepth == 0) return;
			for (int i = current.localCount - 1; i >= 0; i--)
			{
				ref Local local = ref current.locals[i];
				if (local.depth != -1 && local.depth < current.scopeDepth)
					break;
				if (name == local.name)
					Error("Already a variable with this name in this scope.");
			}
			AddLocal(name);
		}
		void AddLocal(string name)
		{
			if (current.localCount >= current.locals.Length)
			{
				Error("Too many local variables in function.");
				return;
			}
			ref Local local = ref current.locals[current.localCount++];
			local.name = name;
			local.depth = -1;
			local.isCaptured = false;
		}
		int ResolveUpvalue(CompilerState compiler, Token name)
		{
			if (compiler.enclosing == null) return -1;
			int local = ResolveLocal(compiler.enclosing, name);
			if (local != -1)
			{
				compiler.enclosing.locals[local].isCaptured = true;
				return AddUpvalue(compiler, (byte)local, true);
			}
			int upvalue = ResolveUpvalue(compiler.enclosing, name);
			if (upvalue != -1)
				return AddUpvalue(compiler, (byte)upvalue, false);
			return -1;
		}
		int AddUpvalue(CompilerState compiler, byte index, bool isLocal)
		{
			int upvalueCount = compiler.function.upvalueCount;
			for (int i = 0; i < upvalueCount; i++)
			{
				var upvalue = compiler.upvalues[i];
				if (upvalue.index == index && upvalue.isLocal == isLocal)
					return i;
			}
			if (upvalueCount == UINT8_COUNT)
			{
				Error("Too many closure variables in function.");
				return 0;
			}
			compiler.upvalues[upvalueCount].isLocal = isLocal;
			compiler.upvalues[upvalueCount].index = index;
			return compiler.function.upvalueCount++;
		}
		int ResolveLocal(CompilerState compiler, Token name)
		{
			for (int i = compiler.localCount - 1; i >= 0; i--)
			{
				ref Local local = ref compiler.locals[i];
				if (identifiersEqual(name, local.name))
				{
					if (local.depth == -1)
						Error("Can't read local variable in its own initializer.");
					return i;
				}
			}
			return -1;
		}
		void Error(string message) => options.Err.WriteLine(message);
		public bool VisitBlockStmt(Stmt.Block stmt) => throw new NotImplementedException();
		public bool VisitClassStmt(Stmt.Class stmt) => throw new NotImplementedException();
		public bool VisitExpressionStmt(Stmt.Expression stmt)
		{
			stmt.expression.Accept(this);
			return EmitByte(OP_POP);
		}

		public bool VisitFunctionStmt(Stmt.Function stmt) => throw new NotImplementedException();
		public bool VisitIfStmt(Stmt.If stmt) => throw new NotImplementedException();
		public bool VisitPrintStmt(Stmt.Print stmt)
		{
			stmt.expression.Accept(this);
			return EmitByte(OP_PRINT);
		}

		public bool VisitReturnStmt(Stmt.Return stmt) => throw new NotImplementedException();
		public bool VisitVarStmt(Stmt.Var stmt)
		{
			byte global = ParseVariable(stmt);
			if (stmt.initializer != null)
				stmt.initializer.Accept(this);
			else
				EmitByte(OP_NIL);
			DefineVariable(global);
			return true;
		}

		public bool VisitWhileStmt(Stmt.While stmt) => throw new NotImplementedException();
	}
}
