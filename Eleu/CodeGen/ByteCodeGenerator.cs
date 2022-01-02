using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Eleu.CodeGen
{
	internal class ByteCodeGenerator: Expr.Visitor<bool>
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
			result.Expr!.Accept(this);
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

		public bool VisitAssignExpr(Expr.Assign expr) => throw new NotImplementedException();
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

		public bool VisitVariableExpr(Expr.Variable expr) => throw new NotImplementedException();

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

		void EmitByte(byte by)
		{
			CurrentChunk.Write(by);
		}

		void EmitByte(OpCode op)
		{
			CurrentChunk.Write(op);
		}

		void EmitBytes(OpCode byte1, byte byte2)
		{
			EmitByte(byte1);
			EmitByte(byte2);
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

		void Error(string message) => options.Err.WriteLine(message);
	}
}
