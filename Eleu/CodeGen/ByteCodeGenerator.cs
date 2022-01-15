using Eleu.Vm;
namespace Eleu.CodeGen;


internal class ByteCodeGenerator : CodeGenBase, Expr.Visitor<bool>, Stmt.Visitor<bool>
{
	string fileName;

	ClassCompiler? currentClass;
	Chunk CurrentChunk => current.function.chunk;
	EleuResult result;
	public ByteCodeGenerator(string fileName, EleuOptions options, EleuResult result) :
		base(options, result.Expr!)
	{
		this.result = result;
		this.fileName = fileName;
		theCurrent = new CompilerState(FunTypeScript);
		currentClass = null;
	}

	CompilerState theCurrent;
	protected override CompilerState current
	{
		get => theCurrent;
	}

	public EleuResult GenCode()
	{
		foreach (var stm in statements)
		{
			try
			{
				stm.Accept(this);
			}
			catch (CodeGenError)
			{
				result.Result = EEleuResult.CodeGenError;
			}
		}
		if (result.Result == Ok)
		{
			var funct = EndCompiler();
			result.Function = funct;
		}
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
		theCurrent = current.enclosing!;
		return function;
	}

	public bool VisitAssignExpr(Expr.Assign expr)
	{
		expr.Value.Accept(this);
		var name = expr.Name;
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
		expr.Left.Accept(this);
		expr.Right.Accept(this);
		switch (expr.Op.type)
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
		if (expr.Method == null)
		{
			expr.Callee.Accept(this);
			AcceptArguments();
			return EmitBytes(OP_CALL, (byte)expr.Arguments.Count);
		}
		// method
		if (!expr.CallSuper)
		{
			expr.Callee.Accept(this);
			var name = MakeConstant(new Value(expr.Method));
			AcceptArguments();
			EmitBytes(OP_INVOKE, name);
			return EmitByte((byte)expr.Arguments.Count);
		}
		// super call
		{
			new Expr.Variable("this").Accept(this);
			var name = MakeConstant(new Value(expr.Method));
			AcceptArguments();
			expr.Callee.Accept(this);
			EmitBytes(OP_SUPER_INVOKE, name);
			return EmitByte((byte)expr.Arguments.Count);
		}
		void AcceptArguments()
		{
			for (int i = 0; i < expr.Arguments.Count; i++)
			{
				expr.Arguments[i].Accept(this);
			}
		}
	}

	public bool VisitGetExpr(Expr.Get expr)
	{
		expr.Obj.Accept(this);
		byte name = IdentifierConstant(expr.Name);
		bool isSuper = expr.Obj is Expr.Super;
		return EmitBytes(isSuper ? OP_GET_SUPER : OP_GET_PROPERTY, name);
	}

	public bool VisitGroupingExpr(Expr.Grouping expr) => expr.Expression.Accept(this);

	public bool VisitLiteralExpr(Expr.Literal expr)
	{
		switch (expr.Value)
		{
			case bool b: EmitByte(b ? OP_TRUE : OP_FALSE); break;
			case null: EmitByte(OP_NIL); break;
			case double d: EmitConstant(CreateNumberVal(d)); break;
			case string s: EmitConstant(CreateStringVal(s)); break;
			default: Error($"Unsupported constant of type: {expr.Value}"); return false;
		}
		return true;
	}

	public bool VisitLogicalExpr(Expr.Logical expr)
	{
		expr.Left.Accept(this);
		var type = expr.Op.type;
		if (type == TOKEN_OR)
		{
			int elseJump = EmitJump(OP_JUMP_IF_FALSE);
			int endJump = EmitJump(OP_JUMP);
			PatchJump(elseJump);
			EmitByte(OP_POP);
			expr.Right.Accept(this);
			PatchJump(endJump);
		}
		else if (type == TOKEN_AND)
		{
			int endJump = EmitJump(OP_JUMP_IF_FALSE);
			EmitByte(OP_POP);
			expr.Right.Accept(this);
			PatchJump(endJump);
		}
		return true;
	}

	public bool VisitSetExpr(Expr.Set expr)
	{
		expr.Obj.Accept(this);
		byte name = IdentifierConstant(expr.Name);
		expr.Value.Accept(this);
		return EmitBytes(OP_SET_PROPERTY, name);
	}

	public bool VisitSuperExpr(Expr.Super expr)
	{
		if (currentClass == null)
			Error("Can't use 'super' outside of a class.");
		else if (!currentClass.hasSuperclass)
		{
			Error("Can't use 'super' in a class with no superclass.");
		}
		new Expr.Variable(expr.Keyword).Accept(this);
		return true;

		//byte name = IdentifierConstant(expr.keyword);
		//var exThis = new Expr.Variable("this");
		//exThis.Accept(this);
		//return false;
		//if (Match(TOKEN_LEFT_PAREN))
		//{
		//	byte argCount = ArgumentList();
		//	NamedVariable(SyntheticToken("super"), false);
		//	EmitBytes(OP_SUPER_INVOKE, name);
		//	EmitByte(argCount);
		//}
		//else
		//{
		//	NamedVariable(SyntheticToken("super"), false);
		//	EmitBytes(OP_GET_SUPER, name);
		//}
	}

	public bool VisitThisExpr(Expr.This expr)
	{
		if (currentClass == null)
		{
			Error("Can't use 'this' outside of a class.");
			return false;
		}
		return new Expr.Variable(expr.Keyword).Accept(this);
	}

	public bool VisitUnaryExpr(Expr.Unary expr)
	{
		expr.Right.Accept(this);
		switch (expr.Op.type)
		{
			case TOKEN_BANG: EmitByte(OP_NOT); break;
			case TokenMinus: EmitByte(OP_NEGATE); break;
			default: return false; // Unreachable.
		}
		return true;
	}

	public bool VisitVariableExpr(Expr.Variable expr)
	{
		var name = expr.Name;
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
	byte IdentifierConstant(string name) => MakeConstant(new Value(name));

	void DefineVariable(byte global)
	{
		if (current.scopeDepth > 0)
		{
			MarkInitialized();
			return;
		}
		EmitBytes(OP_DEFINE_GLOBAL, global);
	}
	int ResolveUpvalue(CompilerState compiler, string name)
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
	int AddUpvalue(CompilerState cstate, byte index, bool isLocal)
	{
		int upvalueCount = cstate.function.upvalueCount;
		for (int i = 0; i < upvalueCount; i++)
		{
			var upvalue = cstate.upvalues[i];
			if (upvalue.index == index && upvalue.isLocal == isLocal)
				return i;
		}
		if (upvalueCount == UINT8_COUNT)
		{
			Error("Too many closure variables in function.");
			return 0;
		}
		cstate.upvalues[upvalueCount].isLocal = isLocal;
		cstate.upvalues[upvalueCount].index = index;
		return cstate.function.upvalueCount++;
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


	public bool VisitBlockStmt(Stmt.Block stmt)
	{
		BeginScope();
		VisitStmtList(stmt.Statements, this);
		EndScope();
		return true;
	}


	public bool VisitClassStmt(Stmt.Class stmt)
	{
		var className = stmt.Name;
		byte nameConstant = IdentifierConstant(className);
		DeclareVariable(className);
		EmitBytes(OP_CLASS, nameConstant);
		DefineVariable(nameConstant);
		var classCompiler = new ClassCompiler();
		classCompiler.enclosing = this.currentClass;
		this.currentClass = classCompiler;
		var clsVar = new Expr.Variable(className);
		if (stmt.Superclass != null)
		{
			if (className == stmt.Superclass.Name)
				Error("A class can't inherit from itself.");
			BeginScope();
			AddLocal("super");
			DefineVariable(0);
			stmt.Superclass.Accept(this);
			clsVar.Accept(this);
			EmitByte(OP_INHERIT);
			classCompiler.hasSuperclass = true;
		}
		clsVar.Accept(this);
		foreach (var mth in stmt.Methods)
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
		if (stmt.Type == FunctionType.FunTypeFunction)
		{
			byte global = ParseVariable(stmt.Name);
			MarkInitialized();
			Function(stmt.Type, stmt);
			DefineVariable(global);
		}
		else if (stmt.Type == FunTypeMethod)
		{
			byte constant = IdentifierConstant(stmt.Name);
			var type = stmt.Type;
			if (stmt.Name == "init")
				type = FunTypeInitializer;
			Function(type, stmt);
			EmitBytes(OP_METHOD, constant);
		}
		return true;
	}
	void Function(FunctionType type, Stmt.Function stmt)
	{
		var compiler = InitCompiler(type, stmt.Name);
		theCurrent = compiler;
		BeginScope();
		foreach (var para in stmt.Paras)
		{
			byte constant = ParseVariable(para.StringValue);
			DefineVariable(constant);
		}
		compiler.function.arity = stmt.Paras.Count;
		VisitStmtList(stmt.Body, this);
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
		stmt.Condition.Accept(this);
		int thenJump = EmitJump(OP_JUMP_IF_FALSE);
		EmitByte(OP_POP);
		stmt.ThenBranch.Accept(this);
		int elseJump = EmitJump(OP_JUMP);
		PatchJump(thenJump);
		EmitByte(OP_POP);
		if (stmt.ElseBranch != null) stmt.ElseBranch.Accept(this);
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
		if (stmt.Value == null)
			EmitReturn();
		else
		{
			if (current.type == FunTypeInitializer)
				Error("Can't return a value from an initializer.");
			stmt.Value.Accept(this);
			EmitByte(OP_RETURN);
		}
		return true;
	}

	public bool VisitVarStmt(Stmt.Var stmt)
	{
		byte global = ParseVariable(stmt.Name);
		if (stmt.Initializer != null)
			stmt.Initializer.Accept(this);
		else
			EmitByte(OP_NIL);
		DefineVariable(global);
		return true;
	}
	public bool VisitWhileStmt(Stmt.While stmt)
	{
		int loopStart = CurrentChunk.count;
		stmt.Condition.Accept(this);
		int exitJump = EmitJump(OP_JUMP_IF_FALSE);
		EmitByte(OP_POP);
		stmt.Body.Accept(this);
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
