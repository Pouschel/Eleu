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
		ClassCompiler? currentClass;
		Chunk CurrentChunk => current.function.chunk;
		public ByteCodeGenerator(string fileName, EleuOptions options, EleuResult result)
		{
			this.options = options;
			this.fileName = fileName;
			this.result = result;
			current = new CompilerState(FunTypeScript);
			currentClass = null;
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

		public bool VisitCallExpr(Expr.Call expr)
		{
			expr.callee.Accept(this);
			byte name = 0;
			if (expr.method != null)
				name = MakeConstant(new Value(expr.method));
			for (int i = 0; i < expr.arguments.Count; i++)
			{
				expr.arguments[i].Accept(this);
			}
			if (expr.method==null)
			  return EmitBytes(OP_CALL, (byte) expr.arguments.Count);
			else
			{
				EmitBytes(OP_INVOKE, name);
				EmitByte((byte)expr.arguments.Count);
			}
			return true;
		}

		public bool VisitGetExpr(Expr.Get expr)
		{
			expr.obj.Accept(this);
			byte name = IdentifierConstant(expr.name);
			EmitBytes(OP_GET_PROPERTY, name);
			return true;
		}

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

		public bool VisitLogicalExpr(Expr.Logical expr)
		{
			expr.left.Accept(this);
			var type = expr.op.type;
			if (type == TOKEN_OR)
			{
				int elseJump = EmitJump(OP_JUMP_IF_FALSE);
				int endJump = EmitJump(OP_JUMP);
				PatchJump(elseJump);
				EmitByte(OP_POP);
				expr.right.Accept(this);
				PatchJump(endJump);
			}
			else if (type==TOKEN_AND)
			{
				int endJump = EmitJump(OP_JUMP_IF_FALSE);
				EmitByte(OP_POP);
				expr.right.Accept(this);
				PatchJump(endJump);
			}
			return true;
		}

		public bool VisitSetExpr(Expr.Set expr)
		{
			expr.obj.Accept(this);
			byte name = IdentifierConstant(expr.name);
			expr.value.Accept(this);
			return EmitBytes(OP_SET_PROPERTY, name);
		}

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
		byte ParseVariable(string name)
		{
			DeclareVariable(name);
			if (current.scopeDepth > 0) return 0;
			return MakeConstant(new Value(name));
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
		void BeginScope() => current.scopeDepth++;
		void EndScope()
		{
			current.scopeDepth--;
			while (current.localCount > 0
				&& current.locals[current.localCount - 1].depth > current.scopeDepth)
			{
				if (current.locals[current.localCount - 1].isCaptured)
					EmitByte(OP_CLOSE_UPVALUE);
				else
				{
					EmitByte(OP_POP);
				}
				current.localCount--;
			}
		}
		void Error(string message) => options.Err.WriteLine(message);
		public bool VisitBlockStmt(Stmt.Block stmt)
		{
			BeginScope();
			VisitStmtList(stmt.statements);
			EndScope();
			return true;
		}

		void VisitStmtList(List<Stmt> list)
		{
			foreach (var istmt in list)
			{
				istmt.Accept(this);
			}
		}
		public bool VisitClassStmt(Stmt.Class stmt)
		{
			var className = stmt.name;
			byte nameConstant = IdentifierConstant(className);
			DeclareVariable(className.StringValue);
			EmitBytes(OP_CLASS, nameConstant);
			DefineVariable(nameConstant);
			var classCompiler = new ClassCompiler();
			classCompiler.enclosing = this.currentClass;
			this.currentClass = classCompiler;
			if (stmt.superclass!=null)
			{
				if (className.StringValue == stmt.superclass.name.StringValue)
					Error("A class can't inherit from itself.");
				BeginScope();
				AddLocal("super");
				DefineVariable(0);
				stmt.superclass.Accept(this);
				EmitByte(OP_INHERIT);
				classCompiler.hasSuperclass = true;
			}
			new Expr.Variable(className).Accept(this);
			foreach (var mth in stmt.methods)
			{
				mth.Accept(this);
			}
			EmitByte(OP_POP);
			if (classCompiler.hasSuperclass)
				EndScope();
			this.currentClass = this.currentClass.enclosing;
			return true;
		}

		public bool VisitExpressionStmt(Stmt.Expression stmt)
		{
			stmt.expression.Accept(this);
			return EmitByte(OP_POP);
		}
		public bool VisitFunctionStmt(Stmt.Function stmt)
		{
			if (stmt.type == FunctionType.FunTypeFunction)
			{
				byte global = ParseVariable(stmt.name.StringValue);
				MarkInitialized();
				Function(stmt.type, stmt);
				DefineVariable(global);
			} else if (stmt.type==FunTypeMethod)
			{
				byte constant = IdentifierConstant(stmt.name);
				var type = stmt.type;
				if (stmt.name.StringValue == "init")
					 type = FunTypeInitializer;
				Function(type, stmt);
				EmitBytes(OP_METHOD, constant);
			}
			return true;
		}
		void Function(FunctionType type, Stmt.Function stmt)
		{
			var compiler = InitCompiler(type, stmt.name.StringValue);
			current = compiler;
			BeginScope();
			foreach (var para in stmt.paras)
			{
				byte constant = ParseVariable(para.StringValue);
				DefineVariable(constant);
			}
			compiler.function.arity = stmt.paras.Count;
			VisitStmtList(stmt.body);
			var function = EndCompiler();
			EmitBytes(OP_CLOSURE, MakeConstant(CreateObjVal(function)));
			for (int i = 0; i < function.upvalueCount; i++)
			{
				EmitByte((byte)(compiler.upvalues[i].isLocal ? 1 : 0));
				EmitByte(compiler.upvalues[i].index);
			}
		}
		public bool VisitIfStmt(Stmt.If stmt)
		{
			stmt.condition.Accept(this);
			int thenJump = EmitJump(OP_JUMP_IF_FALSE);
			EmitByte(OP_POP);
			stmt.thenBranch.Accept(this);
			int elseJump = EmitJump(OP_JUMP);
			PatchJump(thenJump);
			EmitByte(OP_POP);
			if (stmt.elseBranch != null) stmt.elseBranch.Accept(this);
			PatchJump(elseJump);
			return true;
		}
 		int EmitJump(OpCode instruction)
		{
			EmitByte(instruction);
			EmitByte(0xff);
			EmitByte(0xff);
			return CurrentChunk.count - 2;
		}
		void PatchJump(int offset)
		{
			// -2 to adjust for the bytecode for the jump offset itself.
			int jump = CurrentChunk.count - offset - 2;

			if (jump > ushort.MaxValue)
				Error("Too much code to jump over.");
			CurrentChunk.code[offset] = (byte)(jump >> 8 & 0xff);
			CurrentChunk.code[offset + 1] = (byte)(jump & 0xff);
		}

		public bool VisitPrintStmt(Stmt.Print stmt)
		{
			stmt.expression.Accept(this);
			return EmitByte(OP_PRINT);
		}

		public bool VisitReturnStmt(Stmt.Return stmt)
		{
			if (current.type == FunTypeScript)
				Error("Can't return from top-level code.");
			if (stmt.value==null)
				EmitReturn();
			else
			{
				if (current.type == FunTypeInitializer)
					Error("Can't return a value from an initializer.");
				stmt.value.Accept(this);
				EmitByte(OP_RETURN);
			}
			return true;
		}

		public bool VisitVarStmt(Stmt.Var stmt)
		{
			byte global = ParseVariable(stmt.name.StringValue);
			if (stmt.initializer != null)
				stmt.initializer.Accept(this);
			else
				EmitByte(OP_NIL);
			DefineVariable(global);
			return true;
		}

		public bool VisitWhileStmt(Stmt.While stmt)
		{
			int loopStart = CurrentChunk.count;
			stmt.condition.Accept(this);
			int exitJump = EmitJump(OP_JUMP_IF_FALSE);
			EmitByte(OP_POP);
			stmt.body.Accept(this);
			EmitLoop(loopStart);
			PatchJump(exitJump);
			return EmitByte(OP_POP);
		}
		void EmitLoop(int loopStart)
		{
			EmitByte(OP_LOOP);
			int offset = CurrentChunk.count - loopStart + 2;
			if (offset > ushort.MaxValue) Error("Loop body too large.");
			EmitByte((byte)(offset >> 8 & 0xff));
			EmitByte((byte)(offset & 0xff));
		}
	}
}
