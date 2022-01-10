﻿using System;
using System.IO;

namespace Sylvan.BuildTools.Resources
{
	internal class JsonTokenizer
	{
		const int DefaultBufferSize = 0x1000;//4kB
		const int DefaultMaxTokenSize = 0x100000; //1mB

		TextReader reader;

		int line;
		int column;

		char[] buffer;
		int bufferPos;
		int bufferEnd;
		int bufferTokenStart;

		JsonParseErrorHandler errorHandler;

		public JsonTokenizer(TextReader reader) : this(reader, (err, loc) => false)
		{
		}

		public JsonTokenizer(TextReader reader, JsonParseErrorHandler errorHandler) : this(reader, errorHandler, DefaultBufferSize)
		{
		}

		public JsonTokenizer(TextReader reader, JsonParseErrorHandler errorHandler, int bufferSize)
		{
			this.reader = reader;
			buffer = new char[bufferSize];
			this.bufferPos = 0;
			this.bufferEnd = 0;
			this.bufferTokenStart = 0;
			this.line = 1;
			this.column = 1;
			this.errorHandler = errorHandler;
		}

		bool HandleError()
		{
			return HandleError(JsonErrorCode.Unknown, Location);
		}

		bool HandleError(JsonErrorCode code)
		{
			return HandleError(code, Location);
		}

		bool HandleError(JsonErrorCode code, Location location)
		{
			return 
				errorHandler(code, location)
				? false
				: throw new JsonParseException(code, location);
		}

		void NewLine()
		{
			line++;
			column = 1;
		}

		public Location Location
		{
			get
			{
				return new Location(line, column);
			}
		}

		public Location Start { get; private set; }

		public Location End { get; private set; }

		public TokenKind TokenKind { get; private set; }

		void SingleCharacterToken(TokenKind type)
		{
			Start = Location;
			Read();
			End = Location;
			this.TokenKind = type;
		}

		public bool NextToken()
		{
			Start = default;
			End = default;
			TokenKind = TokenKind.None;

			while (true)
			{
				bufferTokenStart = bufferPos;
				var c = Peek();
				switch (c)
				{
				case -1:
					Start = End = Location;
					TokenKind = TokenKind.None;
					return false;
				case '\"':
				case '\'':
					ReadString(c);
					return true;
				case '{':
					SingleCharacterToken(TokenKind.StartObject);
					return true;
				case '}':
					SingleCharacterToken(TokenKind.EndObject);
					return true;
				case '[':
					SingleCharacterToken(TokenKind.StartArray);
					return true;
				case ']':
					SingleCharacterToken(TokenKind.EndArray);
					return true;
				case ':':
					SingleCharacterToken(TokenKind.Colon);
					return true;
				case ',':
					SingleCharacterToken(TokenKind.Comma);
					return true;
				case '/':
					var location = Location;
					Read();
					var n = Peek();
					switch (n)
					{
					case '/':
						Read();
						ReadLineComment();
						return true;
					case '*':
						Read();
						ReadBlockComment();
						return true;
					}
					HandleError(JsonErrorCode.UnexpectedCharacter, location);
					continue;
				case '\r':
					Read();
					if (Peek() == '\n')
					{
						Read();
					}
					NewLine();
					break;
				case '\n':
					Read();
					NewLine();
					break;
				default:

					if (IsDigit((char) c) || c == '-')
					{
						ReadNumber();
						return true;
					}

					if (char.IsWhiteSpace((char) c))
					{
						Read();
						continue;
					}

					if (IsNameStart(c))
					{
						ReadName();
						return true;
					}

					HandleError(JsonErrorCode.UnexpectedCharacter, Location);
					Read();
					continue;
				}
			}
		}

		bool IsDigit(int c)
		{
			return c >= '0' && c <= '9';
		}

		bool IsNameStart(int c)
		{
			return
				c >= 'a' && c <= 'z' ||
				c >= 'A' && c <= 'Z' ||
				c == '_';
		}

		bool IsNamePart(int c)
		{
			return
				IsNameStart(c) ||
				IsDigit(c);
		}

		void ReadName()
		{
			Start = Location;
			Read();
			while (true)
			{
				var c = Peek();
				if (IsNamePart(c))
				{
					Read();
				}
				else
				{
					break;
				}
			}
			End = Location;
			TokenKind = TokenKind.Name;
		}

		bool IsLineEnd(int c)
		{
			return c == '\r' || c == '\n' || c == '\u2028' || c == '\u2029';
		}

		void ReadLineComment()
		{
			while (true)
			{
				var c = Peek();
				if (c == -1)
					break;
				if (c == '\r')
				{
					Read();
					c = Peek();
					if (c == '\n')
						Read();
					break;
				}
				if (IsLineEnd(c))
				{
					Read();
					break;
				}
				Read();
			}
			End = Location;
			NewLine();
			TokenKind = TokenKind.LineComment;
		}

		public long GetInteger()
		{
			long val = 0;
			for (var i = bufferTokenStart; i < bufferPos; i++)
			{
				val = val * 10 + buffer[i] - '0';
			}
			return val;
		}

		public double GetFloat()
		{
			var str = new String(buffer, bufferTokenStart, bufferPos - bufferTokenStart);
			return double.Parse(str);
		}

		public string GetString()
		{
			var writer = new StringWriter();
			WriteString(writer);
			return writer.ToString();
		}

		public int WriteString(TextWriter writer)
		{
			var i = bufferTokenStart;
			char c = buffer[i];
			int count = 0;
			int q = -1;
			if (c == '"' || c == '\'')
			{
				q = c;
				i++;
			}
			while (i < bufferPos)
			{
				c = buffer[i++];
				if (c == q)
					break;
				if (c == '\\')
				{
					if (i >= bufferPos)
					{
						// nothing following the '\', just write it and exit
						writer.Write('\\');
						break;
					}

					var n = buffer[i++];
					switch (n)
					{
					case '\"':
						writer.Write('\"');
						break;
					case '\\':
						writer.Write('\"');
						break;
					case '/':
						writer.Write('/');
						break;
					case 'b':
						writer.Write('\b');
						break;
					case 'f':
						writer.Write('\f');
						break;
					case 'n':
						writer.Write('\n');
						break;
					case 'r':
						writer.Write('\r');
						break;
					case 't':
						writer.Write('\t');
						break;
					case 'u':
						int accum = 0;

						for (int d = 0; d < 4; d++)
						{
							if (i >= bufferPos)
								break;

							var digit = buffer[i++];
							if (digit >= '0' && digit <= '9')
								accum = (accum << 4) | (digit - '0');
							else if (digit >= 'a' && digit <= 'f')
								accum = (accum << 4) | (10 + digit - 'a');
							else if (digit >= 'A' && digit <= 'F')
								accum = (accum << 4) | (10 + digit - 'A');
							else
							{
								i--;
								break;
							}
						}
						writer.Write((char) accum);
						break;
					default:
						// bad escape sequence, just write it as a literal.
						writer.Write('\\');
						writer.Write(n);
						break;
					}
					count++;
				}
				else
				{
					writer.Write((char) c);
					count++;
				}
			}
			return count;
		}

		bool IsKeyword(string str)
		{
			if (bufferPos - bufferTokenStart != str.Length)
				return false;
			for (int i = 0; i < str.Length; i++)
			{
				if (buffer[bufferTokenStart + i] != str[i])
					return false;
			}
			return true;
		}

		bool IsHexDigit(int c)
		{
			return
				c >= '0' && c <= '9' ||
				c >= 'a' && c <= 'f' ||
				c >= 'A' && c <= 'F';
		}

		public SyntaxKind GetValueSyntaxKind()
		{
			if (IsKeyword("true"))
				return SyntaxKind.TrueValue;
			if (IsKeyword("null"))
				return SyntaxKind.NullValue;
			if (IsKeyword("false"))
				return SyntaxKind.FalseValue;
			return SyntaxKind.StringValue;
		}

		void ReadBlockComment()
		{
			while (true)
			{
				var c = Peek();
				if (c == -1)
				{
					HandleError(JsonErrorCode.UnexpectedEndOfText);
					break;
				}

				if (c == '\r')
				{
					Read();
					if (Peek() == '\n')
						Read();
					NewLine();
				}

				if (c == '\n')
				{
					Read();
					NewLine();
				}

				if (c == '*')
				{
					Read();
					c = Peek();
					if (c == '/')
					{
						Read();
						break;
					}
				}

				Read();
			}
			End = Location;
			TokenKind = TokenKind.BlockComment;
		}

		void ReadDigits()
		{
			while (true)
			{
				var c = Peek();
				if (c >= '0' && c <= '9')
				{
					Read();
				}
				else
				{
					break;
				}
			}
		}

		void ReadNumber()
		{
			Start = Location;
			int c;
			var kind = TokenKind.NumberInteger;
			c = Peek();
			if (c == '-')
				Read();

			ReadDigits();

			c = Peek();
			if (c == '.')
			{
				Read();
				kind = TokenKind.NumberFloat;
				ReadDigits();
			}
			if (c == 'e' || c == 'E')
			{
				kind = TokenKind.NumberFloat;
				Read();
				c = Peek();
				if (c == '-' || c == '+')
				{
					Read();
				}
				ReadDigits();
			}

			End = Location;
			TokenKind = kind;
		}

		int GetNewBufferSize()
		{
			var curLen = this.buffer.Length;
			if (curLen >= DefaultMaxTokenSize)
			{
				throw new JsonParseException(JsonErrorCode.TokenTooLong, this.Start);
			}
			var newLen = this.buffer.Length * 2;
			newLen = Math.Min(newLen, DefaultMaxTokenSize);
			return newLen;
		}

		bool FillBuffer()
		{
			var offset = bufferPos - bufferTokenStart;
			if (bufferEnd != 0)
			{
				if (bufferTokenStart == 0)
				{
					// need a bigger buffer;
					var newLen = GetNewBufferSize();
					var buffer = new char[newLen];
					Array.Copy(this.buffer, bufferTokenStart, buffer, 0, bufferEnd - bufferTokenStart);
					this.buffer = buffer;
				}
				else
				{
					Array.Copy(this.buffer, bufferTokenStart, this.buffer, 0, bufferEnd - bufferTokenStart);
				}
			}
			this.bufferTokenStart = 0;

			bufferPos = offset;
			var read = reader.Read(buffer, offset, buffer.Length - offset);
			bufferEnd = bufferPos + read;
			return read != 0;
		}

		void ReadString(int quote)
		{
			Start = Location;
			Read(); // consume the opening quote
			while (true)
			{
				var c = Read();
				if (c == -1)
				{
					HandleError(JsonErrorCode.UnterminatedString, Start);
					return;
				}
				if (c == quote)
				{
					End = Location;
					TokenKind = TokenKind.String;
					return;
				}

				if (c == '\r')
				{
					if (Peek() == '\n')
						Read();
					NewLine();
				}

				if (c == '\n')
				{
					NewLine();
				}

				if (c == '\\')
				{
					var n = Read();
					switch (n)
					{
					case '\"':
					case '\\':
					case '/':
					case 'b':
					case 'f':
					case 'n':
					case 'r':
					case 't':
						break;
					case 'u':
						for (int i = 0; i < 4; i++)
						{
							var d = Peek();

							if (IsHexDigit(d))
							{
								Read();
							}
							else
							{
								HandleError(JsonErrorCode.UnexpectedCharacter);
								break;
							}
						}
						break;
					default:
							HandleError(JsonErrorCode.UnexpectedCharacter);
							break;
					}
				}
			}
		}

		int Peek()
		{
			if (bufferPos >= bufferEnd)
			{
				if (!FillBuffer())
				{
					return -1;
				}
			}

			return buffer[bufferPos];
		}

		bool Read(char c)
		{
			if (c == Peek())
			{
				Read();
				return true;
			}
			return false;
		}

		int Read()
		{
			var c = Peek();
			if (c >= 0)
				bufferPos += 1;
			column++;
			return c;
		}
	}
}
