using System;
using System.Collections.Generic;
using System.IO;

namespace Elemental.Json
{
	public class JsonReader
	{
		const int DefaultMaxErrors = 32;

		struct NodeInfo
		{
			public NodeInfo(Location start, Context context)
			{
				this.Start = start;
				this.Context = context;
			}

			public Location Start { get; }
			public Context Context { get; }
		}

		JsonParseErrorHandler CapErrors(JsonParseErrorHandler handler, int maxErrors)
		{
			int c = 0;
			return
				(error, location) =>
				{
					if (c++ > maxErrors)
						return false;
					return handler(error, location);
				};
		}

		State state;
		JsonTokenizer tokenizer;
		Stack<NodeInfo> nodeStack;
		JsonParseErrorHandler errorHandler;

		public JsonReader(TextReader reader) : this(reader, (err, loc) => false)
		{

		}

		public JsonReader(TextReader reader, JsonParseErrorHandler errorHandler)
		{
			this.tokenizer = new JsonTokenizer(reader, errorHandler);
			this.nodeStack = new Stack<NodeInfo>();
			this.state = State.Start;
			this.errorHandler = CapErrors(errorHandler, DefaultMaxErrors);
		}

		enum State : byte
		{
			Start = 1,
			End,
			ObjectStart,
			PropertyName,
			ObjectColon,
			ObjectValue,
			ArrayStart,
			Comma,
			Value,
		}

		public SyntaxKind SyntaxKind
		{
			get; private set;
		}

		public int Depth
		{
			get { return nodeStack.Count; }
		}

		public Location Start
		{
			get { return tokenizer.Start; }
		}

		public Location End
		{
			get { return tokenizer.End; }
		}

		void HandleError()
		{
			HandleError(default, default);
		}

		void HandleError(JsonErrorCode error, Location location)
		{
			if (!this.errorHandler(error, location)) {
				throw new JsonParseException(error, location);
			}
		}

		void HandleError(JsonErrorCode code)
		{
			if (!this.errorHandler(code, tokenizer.Location)) {
				throw new JsonParseException(code, tokenizer.Location);
			}
		}

		void Push(Context context)
		{
			nodeStack.Push(new NodeInfo(tokenizer.Start, context));
		}

		enum Context : byte
		{
			Root,
			Object,
			Array,
		}

		Context Peek()
		{
			if (nodeStack.Count == 0)
				return Context.Root;

			return nodeStack.Peek().Context;
		}

		Context Pop()
		{
			if (nodeStack.Count == 0)
				throw new Exception();
			var info = nodeStack.Pop();
			return info.Context;
		}

		Location lastEnd;

		public double FloatValue
		{
			get
			{
				switch(this.tokenizer.TokenKind) {
				case TokenKind.NumberFloat:
					return tokenizer.GetFloat();
				case TokenKind.NumberInteger:
					return tokenizer.GetInteger();
				default:
					throw new InvalidOperationException();
				}
			}
		}

		public long IntegerValue
		{
			get
			{
				switch (this.tokenizer.TokenKind) {
				case TokenKind.NumberInteger:
					return tokenizer.GetInteger();
				default:
					throw new InvalidOperationException();
				}
			}
		}

		public string StringValue
		{
			get
			{
				switch(this.tokenizer.TokenKind) {
				case TokenKind.Name:
					throw new NotImplementedException();
				case TokenKind.String:
					return tokenizer.GetString();
				default:
					throw new InvalidOperationException();
				}
			}
		}

		public int WriteStringValue(TextWriter writer)
		{
			switch (this.tokenizer.TokenKind) {
			case TokenKind.Name:
				throw new NotImplementedException();
			case TokenKind.String:
				return tokenizer.WriteString(writer);
			default:
				throw new InvalidOperationException();
			}
			throw new NotImplementedException();
		}

		public string PropertyName
		{
			get
			{
				if(SyntaxKind != SyntaxKind.PropertyName)
					throw new InvalidOperationException();
				switch (this.tokenizer.TokenKind) {
				case TokenKind.Name:
				case TokenKind.String:
					return tokenizer.GetString();
				default:
					throw new InvalidOperationException();
				}
			}
		}

		public int WritePropertyName(TextWriter writer)
		{
			if (SyntaxKind != SyntaxKind.PropertyName)
				throw new InvalidOperationException();
			switch (this.tokenizer.TokenKind) {
			case TokenKind.Name:
			case TokenKind.String:
				return tokenizer.WriteString(writer);
			default:
				throw new InvalidOperationException();
			}
		}

		public bool BooleanValue
		{
			get
			{
				switch (this.tokenizer.TokenKind) {
				case TokenKind.Name:
					switch (this.SyntaxKind) {
					case SyntaxKind.TrueValue:
						return true;
					case SyntaxKind.FalseValue:
						return false;
					}
					break;
				}
				throw new InvalidOperationException();
			}
		}

		public bool Read()
		{
			this.lastEnd = tokenizer.End;
			start:
			if (tokenizer.NextToken()) {
				next:
				var token = tokenizer.TokenKind;
				//this.SyntaxKind = token;

				if (token == TokenKind.EndOfText) {
					if (nodeStack.Count > 0) {
						HandleError(JsonErrorCode.UnexpectedEndOfFile);
						return false;
					}
					return false;
				}

				if (token == TokenKind.BlockComment || token == TokenKind.LineComment)
					goto start;

				switch (state) {
				case State.End:
					switch (token) {
					case TokenKind.None:
						return false;
					default:
						HandleError();
						return false;
					}
				case State.Start:
					switch (token) {
					case TokenKind.StartArray:
						Push(Context.Array);
						state = State.ArrayStart;
						this.SyntaxKind = SyntaxKind.ArrayStart;
						return true;
					case TokenKind.StartObject:
						Push(Context.Object);
						state = State.ObjectStart;
						this.SyntaxKind = SyntaxKind.ObjectStart;
						return true;
					default:
						HandleError(JsonErrorCode.ExpectedStartObjectOrArray, tokenizer.Location);
						goto start;
					}
				case State.ObjectStart:
					if (token == TokenKind.EndObject) {
						var context = Peek();
						if (context == Context.Object) {
							Pop();
							this.SyntaxKind = SyntaxKind.ObjectEnd;
							return true;
						}
						HandleError();
						goto start;
					}
					goto case State.PropertyName;
				case State.PropertyName:
					switch (token) {
					case TokenKind.String:
					case TokenKind.Name:
						this.SyntaxKind = SyntaxKind.PropertyName;
						this.state = State.ObjectColon;
						return true;
					case TokenKind.EndObject:
						var context = Peek();
						if (context == Context.Object) {
							Pop();
							this.SyntaxKind = SyntaxKind.ObjectEnd;
							return true;
						}
						HandleError();
						goto start;
					default:
						HandleError();
						goto start;
					}
				case State.ObjectColon:
					if (token == TokenKind.Colon) {
						state = State.Value;
						goto start;
					}
					else {
						HandleError();
						goto start;
					}
				case State.Value:
					state = State.Comma;
					switch (token) {
					case TokenKind.NumberInteger:
						SyntaxKind = SyntaxKind.IntegerValue;
						return true;
					case TokenKind.NumberFloat:
						SyntaxKind = SyntaxKind.DoubleValue;
						return true;
					case TokenKind.Name:
						SyntaxKind = tokenizer.GetValueSyntaxKind();
						return true;
					case TokenKind.String:
						SyntaxKind = SyntaxKind.StringValue;
						return true;
					case TokenKind.StartArray:
						Push(Context.Array);
						state = State.ArrayStart;
						SyntaxKind = SyntaxKind.ArrayStart;
						return true;
					case TokenKind.StartObject:
						Push(Context.Object);
						state = State.ObjectStart;
						SyntaxKind = SyntaxKind.ObjectStart;
						return true;
					case TokenKind.EndArray:
						var context = Peek();
						if (context == Context.Array) {
							Pop();
							context = Peek();
							state = State.Comma;
							this.SyntaxKind = SyntaxKind.ArrayEnd;
							return true;
						}
						goto default;
					default:
						HandleError();
						goto start;
					}
				case State.ArrayStart:
					if (token == TokenKind.EndArray) {
						var context = Peek();
						if (context == Context.Array) {
							Pop();
							context = Peek();
							state = State.Comma;
							this.SyntaxKind = SyntaxKind.ArrayEnd;
							return true;
						}
					}
					goto case State.Value;
				case State.Comma:
					switch (token) {
					case TokenKind.Comma:
						switch (Peek()) {
						case Context.Object:
							state = State.PropertyName;
							goto start;
						case Context.Array:
							state = State.Value;
							goto start;
						default:
							HandleError(JsonErrorCode.UnexpectedEndOfFile);
							goto start;
						}
					case TokenKind.EndArray:
						if (Peek() == Context.Array) {
							Pop();
						}
						else {
							HandleError();
							goto start;
						}
						return true;
					case TokenKind.EndObject:
						if (Peek() == Context.Object) {
							Pop();
						}
						else {
							HandleError();
							goto start;
						}
						return true;
					default:
						HandleError(JsonErrorCode.MissingComma, lastEnd);
						switch (Peek()) {
						case Context.Array:
							state = State.Value;
							break;
						case Context.Object:
							state = State.PropertyName;
							break;
						}
						goto next;
					}
				}
			}

			if (nodeStack.Count > 0) {
				HandleError(JsonErrorCode.UnexpectedEndOfFile);
			}
			return false;
		}
	}
}
