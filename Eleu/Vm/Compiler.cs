global using static Eleu.Vm.Precedence;
using System.Globalization;

namespace Eleu.Vm;

enum Precedence
{
	PREC_NONE,
	PREC_ASSIGNMENT,  // =
	PREC_OR,          // or
	PREC_AND,         // and
	PREC_EQUALITY,    // == !=
	PREC_COMPARISON,  // < > <= >=
	PREC_TERM,        // + -
	PREC_FACTOR,      // * / %
	PREC_UNARY,       // ! -
	PREC_CALL,        // . ()	[]
	PREC_PRIMARY
}



delegate void PaserAction(bool canAssign);

class ParseRule
{
	public PaserAction? prefix;
	public PaserAction? infix;
	public Precedence precedence;
}

struct Local
{
	public string name;
	public int depth;
	public bool isCaptured;
}

struct Upvalue
{
	public byte index;
	public bool isLocal;
}

class ClassCompiler
{
	public ClassCompiler? enclosing;
	public bool hasSuperclass;
	public bool inSuper;
}



internal class Compiler
{
	Token currentToken;
	Token previousToken;
	bool hadError;
	bool panicMode;

	Scanner scanner;
	CompilerState current;
	ClassCompiler? currentClass;
	string fileName;

	ParseRule[] rules;
	EleuOptions options;
	DebugInfo? debugInfo;
	ChunkDebugInfo? chunkDebugInfo;

	bool DEBUG_PRINT_CODE => options.PrintByteCode;

	void InitTable()
	{
		SetRule(TOKEN_LEFT_PAREN, Grouping, Call, PREC_CALL);
		SetRule(TOKEN_RIGHT_PAREN, null, null, PREC_NONE);
		SetRule(TokenLeftBracket, ListConst, null, PREC_NONE);
		SetRule(TokenRightBracket, null, null, PREC_NONE);
		SetRule(TOKEN_LEFT_BRACE, null, null, PREC_NONE);
		SetRule(TOKEN_RIGHT_BRACE, null, null, PREC_NONE);
		SetRule(TOKEN_COMMA, null, null, PREC_NONE);
		SetRule(TOKEN_DOT, null, Dot, PREC_CALL);
		SetRule(TokenMinus, Unary, binary, PREC_TERM);
		SetRule(TokenPlus, null, binary, PREC_TERM);
		SetRule(TokenSemicolon, null, null, PREC_NONE);
		SetRule(TOKEN_SLASH, null, binary, PREC_FACTOR);
		SetRule(TokenStar, null, binary, PREC_FACTOR);
		SetRule(TokenPercent, null, binary, PREC_FACTOR);
		SetRule(TOKEN_BANG, Unary, null, PREC_NONE);
		SetRule(TOKEN_BANG_EQUAL, null, binary, PREC_EQUALITY);
		SetRule(TOKEN_EQUAL, null, null, PREC_NONE);
		SetRule(TOKEN_EQUAL_EQUAL, null, binary, PREC_EQUALITY);
		SetRule(TOKEN_GREATER, null, binary, PREC_COMPARISON);
		SetRule(TOKEN_GREATER_EQUAL, null, binary, PREC_COMPARISON);
		SetRule(TOKEN_LESS, null, binary, PREC_COMPARISON);
		SetRule(TOKEN_LESS_EQUAL, null, binary, PREC_COMPARISON);
		SetRule(TOKEN_IDENTIFIER, Variable, null, PREC_NONE);
		SetRule(TOKEN_STRING, _string, null, PREC_NONE);
		SetRule(TOKEN_NUMBER, Number, null, PREC_NONE);
		SetRule(TOKEN_AND, null, And_, PREC_AND);
		SetRule(TOKEN_CLASS, null, null, PREC_NONE);
		SetRule(TOKEN_ELSE, null, null, PREC_NONE);
		SetRule(TOKEN_FALSE, Literal, null, PREC_NONE);
		SetRule(TOKEN_FOR, null, null, PREC_NONE);
		SetRule(TOKEN_FUN, null, null, PREC_NONE);
		SetRule(TOKEN_IF, null, null, PREC_NONE);
		SetRule(TOKEN_NIL, Literal, null, PREC_NONE);
		SetRule(TOKEN_OR, null, Or_, PREC_OR);
		SetRule(TOKEN_PRINT, null, null, PREC_NONE);
		SetRule(TOKEN_RETURN, null, null, PREC_NONE);
		SetRule(TOKEN_SUPER, Super_, null, PREC_NONE);
		SetRule(TOKEN_THIS, This_, null, PREC_NONE);
		SetRule(TOKEN_TRUE, Literal, null, PREC_NONE);
		SetRule(TOKEN_VAR, null, null, PREC_NONE);
		SetRule(TOKEN_WHILE, null, null, PREC_NONE);
		SetRule(TOKEN_ERROR, null, null, PREC_NONE);
		SetRule(TOKEN_EOF, null, null, PREC_NONE);

		void SetRule(TokenType tt, PaserAction? prefix, PaserAction? infix, Precedence prec = PREC_NONE)
		{
			var rule = new ParseRule
			{
				prefix = prefix,
				infix = infix,
				precedence = prec
			};
			rules[(int)tt] = rule;
		}
	}

	public Compiler(string source, string fileName, EleuOptions options)
	{
		rules = new ParseRule[(int)TOKEN_EOF + 1];
		InitTable();
		this.fileName = fileName;
		this.options = options;
		scanner = new Scanner(source);
		if (options.CreateDebugInfo) debugInfo = new DebugInfo();
		current = InitCompiler(FunTypeScript);
	}

	public EleuResult Compile()
	{
		scanner.Reset();
		Advance();
		while (!Match(TOKEN_EOF))
		{
			Declaration();
		}
		var function = EndCompiler();
		return new EleuResult
		{
			Result = hadError ? CompileError : Ok,
			Function = hadError ? null : function,
			DebugInfo = debugInfo
		};
	}
	void Declaration()
	{
		if (Match(TOKEN_CLASS))
			ClassDeclaration();
		else if (Match(TOKEN_FUN))
		{
			FunDeclaration();
		}
		else if (Match(TOKEN_VAR))
		{
			VarDeclaration();
		}
		else
		{
			Statement();
		}
		if (panicMode) Synchronize();
	}

	void ClassDeclaration()
	{
		Consume(TOKEN_IDENTIFIER, "Expect class name.");
		var className = previousToken;
		byte nameConstant = IdentifierConstant(previousToken);
		DeclareVariable();
		EmitBytes(OP_CLASS, nameConstant);
		DefineVariable(nameConstant);
		var classCompiler = new ClassCompiler();
		classCompiler.enclosing = this.currentClass;
		this.currentClass = classCompiler;
		if (Match(TOKEN_LESS))
		{
			Consume(TOKEN_IDENTIFIER, "Expect superclass name.");
			Variable(false);
			if (identifiersEqual(className, previousToken))
				Error("A class can't inherit from itself.");
			BeginScope();
			AddLocal(SyntheticToken("super"));
			DefineVariable(0);
			NamedVariable(className, false);
			EmitByte(OP_INHERIT);
			classCompiler.hasSuperclass = true;
		}
		NamedVariable(className, false);
		Consume(TOKEN_LEFT_BRACE, "Expect '{' before class body.");
		while (!Check(TOKEN_RIGHT_BRACE) && !Check(TOKEN_EOF))
		{
			Method();
		}
		Consume(TOKEN_RIGHT_BRACE, "Expect '}' after class body.");
		EmitByte(OP_POP);
		if (classCompiler.hasSuperclass)
			EndScope();
		this.currentClass = this.currentClass.enclosing;
	}

	void Synchronize()
	{
		panicMode = false;

		while (currentToken.type != TOKEN_EOF)
		{
			if (previousToken.type == TokenSemicolon) return;
			switch (currentToken.type)
			{
				case TOKEN_CLASS:
				case TOKEN_FUN:
				case TOKEN_VAR:
				case TOKEN_FOR:
				case TOKEN_IF:
				case TOKEN_WHILE:
				case TOKEN_PRINT:
				case TOKEN_RETURN:
					return;
				default: break;
			}
			Advance();
		}
	}
	void FunDeclaration()
	{
		byte global = ParseVariable("Expect function name.");
		MarkInitialized();
		Function(FunTypeFunction);
		DefineVariable(global);
	}
	CompilerState InitCompiler(FunctionType type)
	{
		var compiler = new CompilerState(type);
		compiler.enclosing = current;
		if (debugInfo != null)
		{
			chunkDebugInfo = new ChunkDebugInfo(fileName, compiler.function);
			debugInfo.Add(chunkDebugInfo);
		}
		if (type != FunTypeScript)
			compiler.function.name = previousToken.StringValue;
		return compiler;
	}
	ObjFunction EndCompiler()
	{
		EmitReturn();
		var function = current.function;
		if (DEBUG_PRINT_CODE)
		{
			CurrentChunk.Disassemble(function.NameOrScript, debugInfo, options.Err);
		}
		current = current.enclosing!;
		chunkDebugInfo = current== null ? null: debugInfo?.GetChunkInfo(current.function.chunk);
		return function;
	}

	void Function(FunctionType type)
	{
		var compiler = InitCompiler(type);
		current = compiler;
		BeginScope();

		Consume(TOKEN_LEFT_PAREN, "Expect '(' after function name.");
		if (!Check(TOKEN_RIGHT_PAREN))
		{
			do
			{
				compiler.function.arity++;
				if (compiler.function.arity > 255)
					ErrorAtCurrent("Can't have more than 255 parameters.");
				byte constant = ParseVariable("Expect parameter name.");
				DefineVariable(constant);
			} while (Match(TOKEN_COMMA));
		}
		Consume(TOKEN_RIGHT_PAREN, "Expect ')' after parameters.");
		Consume(TOKEN_LEFT_BRACE, "Expect '{' before function body.");
		Block();

		var function = EndCompiler();
		EmitBytes(OP_CLOSURE, MakeConstant(CreateObjVal(function)));
		for (int i = 0; i < function.upvalueCount; i++)
		{
			EmitByte((byte)(compiler.upvalues[i].isLocal ? 1 : 0));
			EmitByte(compiler.upvalues[i].index);
		}
	}
	void Method()
	{
		Consume(TOKEN_IDENTIFIER, "Expect method name.");
		byte constant = IdentifierConstant(previousToken);
		var type = FunTypeMethod;
		if (previousToken.StringValue == "init")
			type = FunTypeInitializer;
		Function(type);
		EmitBytes(OP_METHOD, constant);
	}
	void Call(bool canAssign)
	{
		byte argCount = ArgumentList();
		EmitBytes(OP_CALL, argCount);
	}
	void Dot(bool canAssign)
	{
		Consume(TOKEN_IDENTIFIER, "Expect property name after '.'.");
		byte name = IdentifierConstant(previousToken);

		if (canAssign && Match(TOKEN_EQUAL))
		{
			Expression();
			EmitBytes(OP_SET_PROPERTY, name);
		}
		else if (Match(TOKEN_LEFT_PAREN))
		{
			byte argCount = ArgumentList();
			EmitBytes(OP_INVOKE, name);
			EmitByte(argCount);
		}
		else
		{
			EmitBytes(OP_GET_PROPERTY, name);
		}
	}
	byte ArgumentList()
	{
		byte argCount = 0;
		if (!Check(TOKEN_RIGHT_PAREN))
		{
			do
			{
				Expression();
				if (argCount == byte.MaxValue)
					Error("Can't have more than 255 arguments.");
				argCount++;
			} while (Match(TOKEN_COMMA));
		}
		Consume(TOKEN_RIGHT_PAREN, "Expect ')' after arguments.");
		return argCount;
	}
	void VarDeclaration()
	{
		byte global = ParseVariable("Expect variable name.");

		if (Match(TOKEN_EQUAL))
			Expression();
		else
		{
			EmitByte(OP_NIL);
		}
		Consume(TokenSemicolon, "Expect ';' after variable declaration.");

		DefineVariable(global);
	}
	byte ParseVariable(string errorMessage)
	{
		Consume(TOKEN_IDENTIFIER, errorMessage);
		DeclareVariable();
		if (current.scopeDepth > 0) return 0;
		return IdentifierConstant(previousToken);
	}

	byte IdentifierConstant(Token name) => MakeConstant(new Value(name.StringValue));

	void Statement()
	{
		if (Match(TOKEN_PRINT))
			PrintStatement();
		else if (Match(TOKEN_FOR))
		{
			ForStatement();
		}
		else if (Match(TOKEN_IF))
		{
			IfStatement();
		}
		else if (Match(TOKEN_RETURN))
		{
			ReturnStatement();
		}
		else if (Match(TOKEN_WHILE))
		{
			WhileStatement();
		}
		else if (Match(TOKEN_LEFT_BRACE))
		{
			BeginScope();
			Block();
			EndScope();
		}
		else
		{
			ExpressionStatement();
		}
	}
	void ReturnStatement()
	{
		if (current.type == FunTypeScript)
			Error("Can't return from top-level code.");
		if (Match(TokenSemicolon))
			EmitReturn();
		else
		{
			if (current.type == FunTypeInitializer)
				Error("Can't return a value from an initializer.");
			Expression();
			Consume(TokenSemicolon, "Expect ';' after return value.");
			EmitByte(OP_RETURN);
		}
	}
	void ForStatement()
	{
		BeginScope();
		Consume(TOKEN_LEFT_PAREN, "Expect '(' after 'for'.");
		if (Match(TokenSemicolon))
		{
			// No initializer.
		}
		else if (Match(TOKEN_VAR)) VarDeclaration();
		else ExpressionStatement();

		int loopStart = CurrentChunk.count;
		int exitJump = -1;
		if (!Match(TokenSemicolon))
		{
			Expression();
			Consume(TokenSemicolon, "Expect ';' after loop condition.");

			// Jump out of the loop if the condition is false.
			exitJump = EmitJump(OP_JUMP_IF_FALSE);
			EmitByte(OP_POP); // Condition.
		}
		if (!Match(TOKEN_RIGHT_PAREN))
		{
			int bodyJump = EmitJump(OP_JUMP);
			int incrementStart = CurrentChunk.count;
			Expression();
			EmitByte(OP_POP);
			Consume(TOKEN_RIGHT_PAREN, "Expect ')' after for clauses.");

			EmitLoop(loopStart);
			loopStart = incrementStart;
			PatchJump(bodyJump);
		}

		Statement();
		EmitLoop(loopStart);
		if (exitJump != -1)
		{
			PatchJump(exitJump);
			EmitByte(OP_POP); // Condition.
		}
		EndScope();
	}

	void WhileStatement()
	{
		int loopStart = CurrentChunk.count;
		Consume(TOKEN_LEFT_PAREN, "Expect '(' after 'while'.");
		Expression();
		Consume(TOKEN_RIGHT_PAREN, "Expect ')' after condition.");

		int exitJump = EmitJump(OP_JUMP_IF_FALSE);
		EmitByte(OP_POP);
		Statement();
		EmitLoop(loopStart);
		PatchJump(exitJump);
		EmitByte(OP_POP);
	}
	void IfStatement()
	{
		Consume(TOKEN_LEFT_PAREN, "Expect '(' after 'if'.");
		Expression();
		Consume(TOKEN_RIGHT_PAREN, "Expect ')' after condition.");
		int thenJump = EmitJump(OP_JUMP_IF_FALSE);
		EmitByte(OP_POP);
		Statement();
		int elseJump = EmitJump(OP_JUMP);
		PatchJump(thenJump);
		EmitByte(OP_POP);
		if (Match(TOKEN_ELSE)) Statement();
		PatchJump(elseJump);
	}
	void And_(bool canAssign)
	{
		int endJump = EmitJump(OP_JUMP_IF_FALSE);
		EmitByte(OP_POP);
		ParsePrecedence(PREC_AND);
		PatchJump(endJump);
	}
	void Or_(bool canAssign)
	{
		int elseJump = EmitJump(OP_JUMP_IF_FALSE);
		int endJump = EmitJump(OP_JUMP);

		PatchJump(elseJump);
		EmitByte(OP_POP);

		ParsePrecedence(PREC_OR);
		PatchJump(endJump);
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
	void Block()
	{
		while (!Check(TOKEN_RIGHT_BRACE) && !Check(TOKEN_EOF))
		{
			Declaration();
		}
		Consume(TOKEN_RIGHT_BRACE, "Expect '}' after block.");
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

	void ExpressionStatement()
	{
		// allow empty expressions
		if (Match(TokenSemicolon))
			return;
		Expression();
		Consume(TokenSemicolon, "Expect ';' after expression.");
		EmitByte(OP_POP);
	}

	bool Match(TokenType type)
	{
		if (!Check(type)) return false;
		Advance();
		return true;
	}
	bool Check(TokenType type)
	{
		return currentToken.type == type;
	}
	void Expression() => ParsePrecedence(PREC_ASSIGNMENT);
	void PrintStatement()
	{
		Expression();
		Consume(TokenSemicolon, "Expect ';' after value.");
		EmitByte(OP_PRINT);
	}
	void ParsePrecedence(Precedence precedence)
	{
		Advance();
		var prefixRule = GetRule(previousToken.type).prefix;
		if (prefixRule == null)
		{
			Error("Expect expression.");
			return;
		}
		bool canAssign = precedence <= PREC_ASSIGNMENT;
		prefixRule(canAssign);
		while (precedence <= GetRule(currentToken.type).precedence)
		{
			Advance();
			var infixRule = GetRule(previousToken.type).infix;
			infixRule!(canAssign);
		}
		if (canAssign && Match(TOKEN_EQUAL))
			Error("Invalid assignment target.");
	}

	ParseRule GetRule(TokenType type) => rules[(int)type];

	void Advance()
	{
		previousToken = currentToken;
		for (; ; )
		{
			currentToken = scanner.ScanToken();
			if (currentToken.type != TOKEN_ERROR) break;
			ErrorAtCurrent(currentToken.StringValue);
		}
	}

	void Number(bool canAssign)
	{
		double value = double.Parse(previousToken.StringValue, CultureInfo.InvariantCulture);
		EmitConstant(CreateNumberVal(value));
	}
	void Literal(bool canAssign)
	{
		switch (previousToken.type)
		{
			case TOKEN_FALSE: EmitByte(OP_FALSE); break;
			case TOKEN_NIL: EmitByte(OP_NIL); break;
			case TOKEN_TRUE: EmitByte(OP_TRUE); break;
			default: return; // Unreachable.
		}
	}
	void _string(bool canAssign)
	{
		EmitConstant(CreateStringVal(previousToken.StringStringValue));
	}
	void Grouping(bool canAssign)
	{
		Expression();
		Consume(TOKEN_RIGHT_PAREN, "Expect ')' after expression.");
	}

	void ListConst(bool canAssign)
	{
		int count = 0;
		if (!Check(TokenRightBracket))
			do
			{
				Expression(); count++;
			} while (Match(TOKEN_COMMA));
		Consume(TokenRightBracket, "Expect ']' at list end");
		EmitConstant(CreateNumberVal(count));
		EmitByte(OpNewList);
	}

	void Unary(bool canAssign)
	{
		var operatorType = previousToken.type;

		// Compile the operand.
		ParsePrecedence(PREC_UNARY);

		// Emit the operator instruction.
		switch (operatorType)
		{
			case TOKEN_BANG: EmitByte(OP_NOT); break;
			case TokenMinus: EmitByte(OP_NEGATE); break;
			default: return; // Unreachable.
		}
	}
	void binary(bool canAssign)
	{
		var operatorType = previousToken.type;
		var rule = GetRule(operatorType);
		ParsePrecedence(rule.precedence + 1);

		switch (operatorType)
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
			default: return; // Unreachable.
		}
	}
	void Variable(bool canAssign) => NamedVariable(previousToken, canAssign);
	void This_(bool canAssign)
	{
		if (currentClass == null)
		{
			Error("Can't use 'this' outside of a class.");
			return;
		}
		Variable(false);
	}
	void Super_(bool canAssign)
	{
		if (currentClass == null)
			Error("Can't use 'super' outside of a class.");
		else if (!currentClass.hasSuperclass)
		{
			Error("Can't use 'super' in a class with no superclass.");
		}
		Consume(TOKEN_DOT, "Expect '.' after 'super'.");
		Consume(TOKEN_IDENTIFIER, "Expect superclass method name.");
		byte name = IdentifierConstant(previousToken);
		NamedVariable(SyntheticToken("this"), false);
		if (Match(TOKEN_LEFT_PAREN))
		{
			byte argCount = ArgumentList();
			NamedVariable(SyntheticToken("super"), false);
			EmitBytes(OP_SUPER_INVOKE, name);
			EmitByte(argCount);
		}
		else
		{
			NamedVariable(SyntheticToken("super"), false);
			EmitBytes(OP_GET_SUPER, name);
		}
	}
	void NamedVariable(Token name, bool canAssign)
	{
		OpCode getOp, setOp;
		int arg = ResolveLocal(current, name);
		if (arg != -1)
		{
			getOp = OP_GET_LOCAL;
			setOp = OP_SET_LOCAL;
		}
		else if ((arg = ResolveUpvalue(current, name)) != -1)
		{
			getOp = OP_GET_UPVALUE;
			setOp = OP_SET_UPVALUE;
		}
		else
		{
			arg = IdentifierConstant(name);
			getOp = OP_GET_GLOBAL;
			setOp = OP_SET_GLOBAL;
		}
		if (canAssign && Match(TOKEN_EQUAL))
		{
			Expression();
			EmitBytes(setOp, (byte)arg);
		}
		else
		{
			EmitBytes(getOp, (byte)arg);
		}
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
	void Consume(TokenType type, string message)
	{
		if (currentToken.type == type)
		{
			Advance();
			return;
		}
		ErrorAtCurrent(message);
	}

	void Error(string message) => ErrorAt(previousToken, message);
	void ErrorAtCurrent(string message) => ErrorAt(currentToken, message);

	void ErrorAt(in Token token, string message)
	{
		if (panicMode) return;
		panicMode = true;
		var msg = string.IsNullOrEmpty(fileName) ? message : $"{fileName}({token.line}): Cerr: {message}";
		//msg = $"File \"{fileName}\", line {token.line}: Compiler error: {message}";
		options.Err.WriteLine(msg);
		System.Diagnostics.Trace.WriteLine(msg);
		hadError = true;
	}

	public void DumpTokens()
	{
		scanner.Reset();
		int line = -1;
		var tw = options.Err;
		for (; ; )
		{
			var token = scanner.ScanToken();
			if (token.line != line)
			{
				tw.Write("{0,4} ", token.line);
				line = token.line;
			}
			else
			{
				tw.Write("   | ");
			}
			tw.Write("{0,2} '{1}'\n", token.type, token.StringValue);

			if (token.type == TOKEN_EOF || token.type == TOKEN_ERROR) break;
		}
	}

	Chunk CurrentChunk => current.function.chunk;

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

	void EmitByte(byte by)
	{
		chunkDebugInfo?.AddLine(previousToken.line);
		CurrentChunk.Write(by);
	}

	void EmitByte(OpCode op)
	{
		chunkDebugInfo?.AddLine(previousToken.line);
		CurrentChunk.Write(op);
	}

	void EmitBytes(OpCode byte1, byte byte2)
	{
		EmitByte(byte1);
		EmitByte(byte2);
	}

	void EmitConstant(Value value) => EmitBytes(OP_CONSTANT, MakeConstant(value));

	void EmitLoop(int loopStart)
	{
		EmitByte(OP_LOOP);
		int offset = CurrentChunk.count - loopStart + 2;
		if (offset > ushort.MaxValue) Error("Loop body too large.");
		EmitByte((byte)(offset >> 8 & 0xff));
		EmitByte((byte)(offset & 0xff));
	}

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
	void DeclareVariable()
	{
		if (current.scopeDepth == 0) return;
		var name = previousToken;
		for (int i = current.localCount - 1; i >= 0; i--)
		{
			ref Local local = ref current.locals[i];
			if (local.depth != -1 && local.depth < current.scopeDepth)
				break;
			if (identifiersEqual(name, local.name))
				Error("Already a variable with this name in this scope.");
		}
		AddLocal(name);
	}
	void AddLocal(Token name)
	{
		if (current.localCount >= current.locals.Length)
		{
			Error("Too many local variables in function.");
			return;
		}
		ref Local local = ref current.locals[current.localCount++];
		local.name = name.StringValue;
		local.depth = -1;
		local.isCaptured = false;
	}
	static Token SyntheticToken(string text)
	{
		return new()
		{
			source = text,
			end = text.Length,
		};
	}
}

