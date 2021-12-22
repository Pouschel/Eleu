using System.Globalization;
using static CsLox.Precedence;
using static CsLox.FunctionType;
namespace CsLox;

enum Precedence
{
	PREC_NONE,
	PREC_ASSIGNMENT,  // =
	PREC_OR,          // or
	PREC_AND,         // and
	PREC_EQUALITY,    // == !=
	PREC_COMPARISON,  // < > <= >=
	PREC_TERM,        // + -
	PREC_FACTOR,      // * /
	PREC_UNARY,       // ! -
	PREC_CALL,        // . ()
	PREC_PRIMARY
}

enum FunctionType
{
	TYPE_FUNCTION,
	TYPE_INITIALIZER,
	TYPE_METHOD,
	TYPE_SCRIPT
}

struct Parser
{
	public Token current;
	public Token previous;
	public bool hadError;
	public bool panicMode;
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

class CompilerState
{
	public CompilerState? enclosing;
	public ObjFunction function;
	public FunctionType type;
	public Local[] locals = new Local[UINT8_COUNT];
	public int localCount;
	public Upvalue[] upvalues = new Upvalue[UINT8_COUNT];
	public int scopeDepth;

	public CompilerState(FunctionType type)
	{
		this.type = type;
		this.function = new ObjFunction();
		ref Local local = ref locals[localCount++];
		local.depth = 0;
		local.isCaptured = false;
		if (type != FunctionType.TYPE_FUNCTION)
			local.name = "this";
		else local.name = "";
	}

	public override string ToString() => $"{function.NameOrScript}[{function.arity}]";
}

class ClassCompiler
{
	public ClassCompiler? enclosing;
	public bool hasSuperclass;
}

internal class Compiler
{
	Scanner scanner;
	Parser parser;
	CompilerState current;
	ClassCompiler? currentClass;
	string fileName;
	TextWriter tw;
	ParseRule[] rules;
	public bool DEBUG_PRINT_CODE { get; set; }

	void InitTable()
	{
		SetRule(TOKEN_LEFT_PAREN, grouping, call, PREC_CALL);
		SetRule(TOKEN_RIGHT_PAREN, null, null, PREC_NONE);
		SetRule(TOKEN_LEFT_BRACE, null, null, PREC_NONE);
		SetRule(TOKEN_RIGHT_BRACE, null, null, PREC_NONE);
		SetRule(TOKEN_COMMA, null, null, PREC_NONE);
		SetRule(TOKEN_DOT, null, dot, PREC_CALL);
		SetRule(TOKEN_MINUS, unary, binary, PREC_TERM);
		SetRule(TOKEN_PLUS, null, binary, PREC_TERM);
		SetRule(TOKEN_SEMICOLON, null, null, PREC_NONE);
		SetRule(TOKEN_SLASH, null, binary, PREC_FACTOR);
		SetRule(TOKEN_STAR, null, binary, PREC_FACTOR);
		SetRule(TOKEN_BANG, unary, null, PREC_NONE);
		SetRule(TOKEN_BANG_EQUAL, null, binary, PREC_EQUALITY);
		SetRule(TOKEN_EQUAL, null, null, PREC_NONE);
		SetRule(TOKEN_EQUAL_EQUAL, null, binary, PREC_EQUALITY);
		SetRule(TOKEN_GREATER, null, binary, PREC_COMPARISON);
		SetRule(TOKEN_GREATER_EQUAL, null, binary, PREC_COMPARISON);
		SetRule(TOKEN_LESS, null, binary, PREC_COMPARISON);
		SetRule(TOKEN_LESS_EQUAL, null, binary, PREC_COMPARISON);
		SetRule(TOKEN_IDENTIFIER, variable, null, PREC_NONE);
		SetRule(TOKEN_STRING, _string, null, PREC_NONE);
		SetRule(TOKEN_NUMBER, number, null, PREC_NONE);
		SetRule(TOKEN_AND, null, and_, PREC_AND);
		SetRule(TOKEN_CLASS, null, null, PREC_NONE);
		SetRule(TOKEN_ELSE, null, null, PREC_NONE);
		SetRule(TOKEN_FALSE, literal, null, PREC_NONE);
		SetRule(TOKEN_FOR, null, null, PREC_NONE);
		SetRule(TOKEN_FUN, null, null, PREC_NONE);
		SetRule(TOKEN_IF, null, null, PREC_NONE);
		SetRule(TOKEN_NIL, literal, null, PREC_NONE);
		SetRule(TOKEN_OR, null, or_, PREC_OR);
		SetRule(TOKEN_PRINT, null, null, PREC_NONE);
		SetRule(TOKEN_RETURN, null, null, PREC_NONE);
		SetRule(TOKEN_SUPER, super_, null, PREC_NONE);
		SetRule(TOKEN_THIS, this_, null, PREC_NONE);
		SetRule(TOKEN_TRUE, literal, null, PREC_NONE);
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

	public Compiler(string source, string fileName, TextWriter tw)
	{
		rules = new ParseRule[(int)TOKEN_EOF + 1];
		InitTable();
		this.fileName = fileName;
		this.tw = tw;
		scanner = new Scanner(source);
		parser = new Parser();
		current = initCompiler(TYPE_SCRIPT);
	}
	public ObjFunction? compile()
	{
		scanner.Reset();
		advance();
		while (!match(TOKEN_EOF))
		{
			declaration();
		}
		var function = endCompiler();
		return parser.hadError ? null : function;
	}
	void declaration()
	{
		if (match(TOKEN_CLASS))
		{
			classDeclaration();
		}
		else if (match(TOKEN_FUN))
		{
			funDeclaration();
		}
		else if (match(TOKEN_VAR))
		{
			varDeclaration();
		}
		else
		{
			statement();
		}
		if (parser.panicMode) synchronize();
	}

	void classDeclaration()
	{
		consume(TOKEN_IDENTIFIER, "Expect class name.");
		Token className = parser.previous;
		byte nameConstant = identifierConstant(parser.previous);
		declareVariable();
		emitBytes(OP_CLASS, nameConstant);
		defineVariable(nameConstant);
		ClassCompiler classCompiler = new ClassCompiler();
		classCompiler.enclosing = this.currentClass;
		this.currentClass = classCompiler;
		if (match(TOKEN_LESS))
		{
			consume(TOKEN_IDENTIFIER, "Expect superclass name.");
			variable(false);
			if (identifiersEqual(className, parser.previous))
			{
				error("A class can't inherit from itself.");
			}
			beginScope();
			addLocal(syntheticToken("super"));
			defineVariable(0);
			namedVariable(className, false);
			emitByte(OP_INHERIT);
			classCompiler.hasSuperclass = true;
		}
		namedVariable(className, false);
		consume(TOKEN_LEFT_BRACE, "Expect '{' before class body.");
		while (!check(TOKEN_RIGHT_BRACE) && !check(TOKEN_EOF))
		{
			method();
		}
		consume(TOKEN_RIGHT_BRACE, "Expect '}' after class body.");
		emitByte(OP_POP);
		if (classCompiler.hasSuperclass)
		{
			endScope();
		}
		this.currentClass = this.currentClass.enclosing;
	}

	void synchronize()
	{
		parser.panicMode = false;

		while (parser.current.type != TOKEN_EOF)
		{
			if (parser.previous.type == TOKEN_SEMICOLON) return;
			switch (parser.current.type)
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
			advance();
		}
	}
	void funDeclaration()
	{
		byte global = parseVariable("Expect function name.");
		markInitialized();
		function(TYPE_FUNCTION);
		defineVariable(global);
	}
	CompilerState initCompiler(FunctionType type)
	{
		CompilerState compiler = new CompilerState(type);
		compiler.enclosing = current;
		compiler.function.chunk.FileName = fileName;
		if (type != TYPE_SCRIPT)
			compiler.function.name = parser.previous.StringValue;
		return compiler;
	}
	void function(FunctionType type)
	{
		var compiler = initCompiler(type);
		current = compiler;
		beginScope();

		consume(TOKEN_LEFT_PAREN, "Expect '(' after function name.");
		if (!check(TOKEN_RIGHT_PAREN))
		{
			do
			{
				compiler.function.arity++;
				if (compiler.function.arity > 255)
				{
					errorAtCurrent("Can't have more than 255 parameters.");
				}
				byte constant = parseVariable("Expect parameter name.");
				defineVariable(constant);
			} while (match(TOKEN_COMMA));
		}
		consume(TOKEN_RIGHT_PAREN, "Expect ')' after parameters.");
		consume(TOKEN_LEFT_BRACE, "Expect '{' before function body.");
		block();

		ObjFunction function = endCompiler();
		emitBytes(OP_CLOSURE, makeConstant(OBJ_VAL(function)));
		for (int i = 0; i < function.upvalueCount; i++)
		{
			emitByte((byte)(compiler.upvalues[i].isLocal ? 1 : 0));
			emitByte(compiler.upvalues[i].index);
		}
	}
	void method()
	{
		consume(TOKEN_IDENTIFIER, "Expect method name.");
		byte constant = identifierConstant(parser.previous);
		FunctionType type = TYPE_METHOD;
		if (parser.previous.StringValue == "init")
		{
			type = TYPE_INITIALIZER;
		}
		function(type);
		emitBytes(OP_METHOD, constant);
	}
	void call(bool canAssign)
	{
		byte argCount = argumentList();
		emitBytes(OP_CALL, argCount);
	}
	void dot(bool canAssign)
	{
		consume(TOKEN_IDENTIFIER, "Expect property name after '.'.");
		byte name = identifierConstant(parser.previous);

		if (canAssign && match(TOKEN_EQUAL))
		{
			expression();
			emitBytes(OP_SET_PROPERTY, name);
		}
		else if (match(TOKEN_LEFT_PAREN))
		{
			byte argCount = argumentList();
			emitBytes(OP_INVOKE, name);
			emitByte(argCount);
		}
		else
		{
			emitBytes(OP_GET_PROPERTY, name);
		}
	}
	byte argumentList()
	{
		byte argCount = 0;
		if (!check(TOKEN_RIGHT_PAREN))
		{
			do
			{
				expression();
				if (argCount == byte.MaxValue)
				{
					error("Can't have more than 255 arguments.");
				}
				argCount++;
			} while (match(TOKEN_COMMA));
		}
		consume(TOKEN_RIGHT_PAREN, "Expect ')' after arguments.");
		return argCount;
	}
	void varDeclaration()
	{
		byte global = parseVariable("Expect variable name.");

		if (match(TOKEN_EQUAL))
		{
			expression();
		}
		else
		{
			emitByte(OP_NIL);
		}
		consume(TOKEN_SEMICOLON, "Expect ';' after variable declaration.");

		defineVariable(global);
	}
	byte parseVariable(string errorMessage)
	{
		consume(TOKEN_IDENTIFIER, errorMessage);
		declareVariable();
		if (current.scopeDepth > 0) return 0;
		return identifierConstant(parser.previous);
	}

	byte identifierConstant(Token name)
	{
		return makeConstant(new Value(name.StringValue));
	}

	void statement()
	{
		if (match(TOKEN_PRINT))
		{
			printStatement();

		}
		else if (match(TOKEN_FOR))
		{
			forStatement();
		}
		else if (match(TOKEN_IF))
		{
			ifStatement();
		}
		else if (match(TOKEN_RETURN))
		{
			returnStatement();
		}
		else if (match(TOKEN_WHILE))
		{
			whileStatement();
		}
		else if (match(TOKEN_LEFT_BRACE))
		{
			beginScope();
			block();
			endScope();
		}
		else
		{
			expressionStatement();
		}
	}
	void returnStatement()
	{
		if (current.type == TYPE_SCRIPT)
		{
			error("Can't return from top-level code.");
		}
		if (match(TOKEN_SEMICOLON))
		{
			emitReturn();
		}
		else
		{
			if (current.type == TYPE_INITIALIZER)
			{
				error("Can't return a value from an initializer.");
			}
			expression();
			consume(TOKEN_SEMICOLON, "Expect ';' after return value.");
			emitByte(OP_RETURN);
		}
	}
	void forStatement()
	{
		beginScope();
		consume(TOKEN_LEFT_PAREN, "Expect '(' after 'for'.");
		if (match(TOKEN_SEMICOLON))
		{
			// No initializer.
		}
		else if (match(TOKEN_VAR)) varDeclaration();
		else expressionStatement();

		int loopStart = currentChunk().count;
		int exitJump = -1;
		if (!match(TOKEN_SEMICOLON))
		{
			expression();
			consume(TOKEN_SEMICOLON, "Expect ';' after loop condition.");

			// Jump out of the loop if the condition is false.
			exitJump = emitJump(OP_JUMP_IF_FALSE);
			emitByte(OP_POP); // Condition.
		}
		if (!match(TOKEN_RIGHT_PAREN))
		{
			int bodyJump = emitJump(OP_JUMP);
			int incrementStart = currentChunk().count;
			expression();
			emitByte(OP_POP);
			consume(TOKEN_RIGHT_PAREN, "Expect ')' after for clauses.");

			emitLoop(loopStart);
			loopStart = incrementStart;
			patchJump(bodyJump);
		}

		statement();
		emitLoop(loopStart);
		if (exitJump != -1)
		{
			patchJump(exitJump);
			emitByte(OP_POP); // Condition.
		}
		endScope();
	}

	void whileStatement()
	{
		int loopStart = currentChunk().count;
		consume(TOKEN_LEFT_PAREN, "Expect '(' after 'while'.");
		expression();
		consume(TOKEN_RIGHT_PAREN, "Expect ')' after condition.");

		int exitJump = emitJump(OP_JUMP_IF_FALSE);
		emitByte(OP_POP);
		statement();
		emitLoop(loopStart);
		patchJump(exitJump);
		emitByte(OP_POP);
	}
	void ifStatement()
	{
		consume(TOKEN_LEFT_PAREN, "Expect '(' after 'if'.");
		expression();
		consume(TOKEN_RIGHT_PAREN, "Expect ')' after condition.");
		int thenJump = emitJump(OP_JUMP_IF_FALSE);
		emitByte(OP_POP);
		statement();
		int elseJump = emitJump(OP_JUMP);
		patchJump(thenJump);
		emitByte(OP_POP);
		if (match(TOKEN_ELSE)) statement();
		patchJump(elseJump);
	}
	void and_(bool canAssign)
	{
		int endJump = emitJump(OP_JUMP_IF_FALSE);
		emitByte(OP_POP);
		parsePrecedence(PREC_AND);
		patchJump(endJump);
	}
	void or_(bool canAssign)
	{
		int elseJump = emitJump(OP_JUMP_IF_FALSE);
		int endJump = emitJump(OP_JUMP);

		patchJump(elseJump);
		emitByte(OP_POP);

		parsePrecedence(PREC_OR);
		patchJump(endJump);
	}
	int emitJump(OpCode instruction)
	{
		emitByte(instruction);
		emitByte(0xff);
		emitByte(0xff);
		return currentChunk().count - 2;
	}
	void patchJump(int offset)
	{
		// -2 to adjust for the bytecode for the jump offset itself.
		int jump = currentChunk().count - offset - 2;

		if (jump > ushort.MaxValue)
		{
			error("Too much code to jump over.");
		}
		currentChunk().code[offset] = (byte)((jump >> 8) & 0xff);
		currentChunk().code[offset + 1] = (byte)(jump & 0xff);
	}
	void block()
	{
		while (!check(TOKEN_RIGHT_BRACE) && !check(TOKEN_EOF))
		{
			declaration();
		}

		consume(TOKEN_RIGHT_BRACE, "Expect '}' after block.");
	}
	void beginScope()
	{
		current.scopeDepth++;
	}
	void endScope()
	{
		current.scopeDepth--;
		while (current.localCount > 0
			&& current.locals[current.localCount - 1].depth > current.scopeDepth)
		{
			if (current.locals[current.localCount - 1].isCaptured)
			{
				emitByte(OP_CLOSE_UPVALUE);
			}
			else
			{
				emitByte(OP_POP);
			}
			current.localCount--;
		}
	}

	void expressionStatement()
	{
		expression();
		consume(TOKEN_SEMICOLON, "Expect ';' after expression.");
		emitByte(OP_POP);
	}

	bool match(TokenType type)
	{
		if (!check(type)) return false;
		advance();
		return true;
	}
	bool check(TokenType type)
	{
		return parser.current.type == type;
	}
	void expression()
	{
		parsePrecedence(PREC_ASSIGNMENT);
	}
	void printStatement()
	{
		expression();
		consume(TOKEN_SEMICOLON, "Expect ';' after value.");
		emitByte(OP_PRINT);
	}
	void parsePrecedence(Precedence precedence)
	{
		advance();
		var prefixRule = getRule(parser.previous.type).prefix;
		if (prefixRule == null)
		{
			error("Expect expression.");
			return;
		}
		bool canAssign = precedence <= PREC_ASSIGNMENT;
		prefixRule(canAssign);
		while (precedence <= getRule(parser.current.type).precedence)
		{
			advance();
			var infixRule = getRule(parser.previous.type).infix;
			infixRule!(canAssign);
		}
		if (canAssign && match(TOKEN_EQUAL))
		{
			error("Invalid assignment target.");
		}
	}

	ParseRule getRule(TokenType type) => rules[(int)type];

	void advance()
	{
		parser.previous = parser.current;
		for (; ; )
		{
			parser.current = scanner.scanToken();
			if (parser.current.type != TOKEN_ERROR) break;

			errorAtCurrent(parser.current.StringValue);
		}
	}

	void number(bool canAssign)
	{
		double value = double.Parse(parser.previous.StringValue, CultureInfo.InvariantCulture);
		emitConstant(NUMBER_VAL(value));
	}
	void literal(bool canAssign)
	{
		switch (parser.previous.type)
		{
			case TOKEN_FALSE: emitByte(OP_FALSE); break;
			case TOKEN_NIL: emitByte(OP_NIL); break;
			case TOKEN_TRUE: emitByte(OP_TRUE); break;
			default: return; // Unreachable.
		}
	}
	void _string(bool canAssign)
	{
		emitConstant(OBJ_VAL(parser.previous.StringStringValue));
	}
	void grouping(bool canAssign)
	{
		expression();
		consume(TOKEN_RIGHT_PAREN, "Expect ')' after expression.");
	}

	void unary(bool canAssign)
	{
		TokenType operatorType = parser.previous.type;

		// Compile the operand.
		parsePrecedence(PREC_UNARY);

		// Emit the operator instruction.
		switch (operatorType)
		{
			case TOKEN_BANG: emitByte(OP_NOT); break;
			case TOKEN_MINUS: emitByte(OP_NEGATE); break;
			default: return; // Unreachable.
		}
	}
	void binary(bool canAssign)
	{
		TokenType operatorType = parser.previous.type;
		ParseRule rule = getRule(operatorType);
		parsePrecedence(rule.precedence + 1);

		switch (operatorType)
		{
			case TOKEN_BANG_EQUAL: emitBytes(OP_EQUAL, (byte)OP_NOT); break;
			case TOKEN_EQUAL_EQUAL: emitByte(OP_EQUAL); break;
			case TOKEN_GREATER: emitByte(OP_GREATER); break;
			case TOKEN_GREATER_EQUAL: emitBytes(OP_LESS, (byte)OP_NOT); break;
			case TOKEN_LESS: emitByte(OP_LESS); break;
			case TOKEN_LESS_EQUAL: emitBytes(OP_GREATER, (byte)OP_NOT); break;
			case TOKEN_PLUS: emitByte(OP_ADD); break;
			case TOKEN_MINUS: emitByte(OP_SUBTRACT); break;
			case TOKEN_STAR: emitByte(OP_MULTIPLY); break;
			case TOKEN_SLASH: emitByte(OP_DIVIDE); break;
			default: return; // Unreachable.
		}
	}
	void variable(bool canAssign)
	{
		namedVariable(parser.previous, canAssign);
	}
	void this_(bool canAssign)
	{
		if (currentClass == null)
		{
			error("Can't use 'this' outside of a class.");
			return;
		}
		variable(false);
	}
	void super_(bool canAssign)
	{
		if (currentClass == null)
		{
			error("Can't use 'super' outside of a class.");
		}
		else if (!currentClass.hasSuperclass)
		{
			error("Can't use 'super' in a class with no superclass.");
		}
		consume(TOKEN_DOT, "Expect '.' after 'super'.");
		consume(TOKEN_IDENTIFIER, "Expect superclass method name.");
		byte name = identifierConstant(parser.previous);
		namedVariable(syntheticToken("this"), false);
		if (match(TOKEN_LEFT_PAREN))
		{
			byte argCount = argumentList();
			namedVariable(syntheticToken("super"), false);
			emitBytes(OP_SUPER_INVOKE, name);
			emitByte(argCount);
		}
		else
		{
			namedVariable(syntheticToken("super"), false);
			emitBytes(OP_GET_SUPER, name);
		}
	}
	void namedVariable(Token name, bool canAssign)
	{
		OpCode getOp, setOp;
		int arg = resolveLocal(current, name);
		if (arg != -1)
		{
			getOp = OP_GET_LOCAL;
			setOp = OP_SET_LOCAL;
		}
		else if ((arg = resolveUpvalue(current, name)) != -1)
		{
			getOp = OP_GET_UPVALUE;
			setOp = OP_SET_UPVALUE;
		}
		else
		{
			arg = identifierConstant(name);
			getOp = OP_GET_GLOBAL;
			setOp = OP_SET_GLOBAL;
		}
		if (canAssign && match(TOKEN_EQUAL))
		{
			expression();
			emitBytes(setOp, (byte)arg);
		}
		else
		{
			emitBytes(getOp, (byte)arg);
		}
	}
	int resolveUpvalue(CompilerState compiler, Token name)
	{
		if (compiler.enclosing == null) return -1;
		int local = resolveLocal(compiler.enclosing, name);
		if (local != -1)
		{
			compiler.enclosing.locals[local].isCaptured = true;
			return addUpvalue(compiler, (byte)local, true);
		}
		int upvalue = resolveUpvalue(compiler.enclosing, name);
		if (upvalue != -1)
		{
			return addUpvalue(compiler, (byte)upvalue, false);
		}
		return -1;
	}
	int addUpvalue(CompilerState compiler, byte index, bool isLocal)
	{
		int upvalueCount = compiler.function.upvalueCount;
		for (int i = 0; i < upvalueCount; i++)
		{
			Upvalue upvalue = compiler.upvalues[i];
			if (upvalue.index == index && upvalue.isLocal == isLocal)
			{
				return i;
			}
		}
		if (upvalueCount == UINT8_COUNT)
		{
			error("Too many closure variables in function.");
			return 0;
		}
		compiler.upvalues[upvalueCount].isLocal = isLocal;
		compiler.upvalues[upvalueCount].index = index;
		return compiler.function.upvalueCount++;
	}
	int resolveLocal(CompilerState compiler, Token name)
	{
		for (int i = compiler.localCount - 1; i >= 0; i--)
		{
			ref Local local = ref compiler.locals[i];
			if (identifiersEqual(name, local.name))
			{
				if (local.depth == -1)
				{
					error("Can't read local variable in its own initializer.");
				}
				return i;
			}
		}
		return -1;
	}
	void consume(TokenType type, string message)
	{
		if (parser.current.type == type)
		{
			advance();
			return;
		}
		errorAtCurrent(message);
	}

	void error(string message) => errorAt(parser.previous, message);
	void errorAtCurrent(string message) => errorAt(parser.current, message);

	void errorAt(in Token token, string message)
	{
		if (parser.panicMode) return;
		parser.panicMode = true;
		var msg = string.IsNullOrEmpty(fileName) ? message : $"{fileName}({token.line}): {message}";
		tw.WriteLine(msg);
		System.Diagnostics.Trace.WriteLine(msg);
		parser.hadError = true;
	}

	public void DumpTokens()
	{
		scanner.Reset();
		int line = -1;
		for (; ; )
		{
			Token token = scanner.scanToken();
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

	Chunk currentChunk() => current.function.chunk;

	ObjFunction endCompiler()
	{
		emitReturn();
		ObjFunction function = current.function;
		if (DEBUG_PRINT_CODE)
		{
			if (!parser.hadError)
			{
				currentChunk().disassemble(function.NameOrScript);
			}
		}
		current = current.enclosing!;
		return function;
	}

	void emitReturn()
	{
		if (current.type == TYPE_INITIALIZER)
		{
			emitBytes(OP_GET_LOCAL, 0);
		}
		else
		{
			emitByte(OP_NIL);
		}
		emitByte(OP_RETURN);
	}

	void emitByte(byte by) => currentChunk().write(by, parser.previous.line);
	void emitByte(OpCode op) => currentChunk().write(op, parser.previous.line);

	void emitBytes(OpCode byte1, byte byte2)
	{
		emitByte(byte1);
		emitByte(byte2);
	}

	void emitConstant(Value value)
	{
		emitBytes(OP_CONSTANT, makeConstant(value));
	}

	void emitLoop(int loopStart)
	{
		emitByte(OP_LOOP);
		int offset = currentChunk().count - loopStart + 2;
		if (offset > ushort.MaxValue) error("Loop body too large.");
		emitByte((byte)((offset >> 8) & 0xff));
		emitByte((byte)(offset & 0xff));
	}

	byte makeConstant(Value value)
	{
		int constant = currentChunk().addConstant(value);
		if (constant > byte.MaxValue)
		{
			error("Too many constants in one chunk.");
			return 0;
		}
		return (byte)constant;
	}
	void defineVariable(byte global)
	{
		if (current.scopeDepth > 0)
		{
			markInitialized();
			return;
		}
		emitBytes(OP_DEFINE_GLOBAL, global);
	}
	void markInitialized()
	{
		if (current.scopeDepth == 0) return;
		current.locals[current.localCount - 1].depth = current.scopeDepth;
	}
	void declareVariable()
	{
		if (current.scopeDepth == 0) return;
		Token name = parser.previous;
		for (int i = current.localCount - 1; i >= 0; i--)
		{
			ref Local local = ref current.locals[i];
			if (local.depth != -1 && local.depth < current.scopeDepth)
			{
				break;
			}
			if (identifiersEqual(name, local.name))
			{
				error("Already a variable with this name in this scope.");
			}
		}
		addLocal(name);
	}
	void addLocal(Token name)
	{
		if (current.localCount >= current.locals.Length)
		{
			error("Too many local variables in function.");
			return;
		}
		ref Local local = ref current.locals[current.localCount++];
		local.name = name.StringValue;
		local.depth = -1;
		local.isCaptured = false;
	}
	static Token syntheticToken(string text)
	{
		return new()
		{
			source = text,
			end = text.Length,
		};
	}
}

