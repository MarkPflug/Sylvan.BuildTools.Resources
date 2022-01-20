using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Sylvan
{
	/// <summary>
	/// Provides conversions between different styles of identifiers.
	/// </summary>
	abstract class IdentifierStyle
	{
		/// <summary>
		/// A "PascalCase" identifier style.
		/// </summary>
		public static readonly IdentifierStyle PascalCase = new PascalCaseStyle();

		/// <summary>
		/// Converts a string to the given identifier style.
		/// </summary>
		public abstract string Convert(string str);

		internal static string Separated(string str, CasingStyle segmentStyle, char separator = '\0', char quote = '\0')
		{
			if (str == null) throw new ArgumentNullException(nameof(str));

			using var sw = new StringWriter();
			if (quote != '\0')
			{
				sw.Write(quote);
			}
			bool isUpper = IsAllUpper(str);

			bool first = true;

			foreach (var segment in GetSegments(str))
			{
				if (!first)
				{
					if (separator != '\0')
						sw.Write(separator);
				}
				for (int i = segment.Start; i < segment.End; i++)
				{
					var c = str[i];

					if (i == segment.Start)
					{
						switch (segmentStyle)
						{
							case CasingStyle.LowerCase:
								c = char.ToLowerInvariant(c);
								break;
							case CasingStyle.TitleCase:
							case CasingStyle.UpperCase:
								c = char.ToUpperInvariant(c);
								break;
						}
					}
					else
					{
						switch (segmentStyle)
						{
							case CasingStyle.LowerCase:
								c = char.ToLowerInvariant(c);
								break;
							case CasingStyle.TitleCase:
								if (isUpper)
								{
									c = char.ToLowerInvariant(c);
								}
								break;
							case CasingStyle.UpperCase:
								c = char.ToUpperInvariant(c);
								break;
						}
					}
					sw.Write(c);
				}
				first = false;
			}
			if (quote != '\0')
			{
				sw.Write(quote);
			}
			return sw.ToString();
		}

		static UnicodeCategory GetUnicodeCategory(char c)
		{
			if (char.IsUpper(c))
				return UnicodeCategory.UppercaseLetter;
			if (char.IsLower(c))
				return UnicodeCategory.LowercaseLetter;
			if (char.IsDigit(c))
				return UnicodeCategory.DecimalDigitNumber;

			return UnicodeCategory.OtherLetter;
		}

		internal static IEnumerable<Range> GetSegments(string identifier)
		{
			int start = 0;
			int length = 0;

			for (var i = 0; i < identifier.Length; i++)
			{
startLabel:
				var c = identifier[i];

				var cat = GetUnicodeCategory(c);
				switch (cat)
				{
					case UnicodeCategory.UppercaseLetter:
						if (length > 0)
						{
							yield return new Range(start, length);
						}
						start = i;
						length = 1;
						for (int j = i + 1; j < identifier.Length; j++)
						{
							c = identifier[j];
							cat = GetUnicodeCategory(c);
							switch (cat)
							{
								case UnicodeCategory.UppercaseLetter:
									length++;
									break;
								case UnicodeCategory.LowercaseLetter:
									if (length > 1)
									{
										yield return new Range(start, length - 1);
										start = j - 1;
										i = j;
										length = 2;
									}
									goto done;
								case UnicodeCategory.DecimalDigitNumber:
									yield return new Range(start, length);
									start = j;
									i = j;
									length = 0;
									goto startLabel;
								default:
									yield return new Range(start, length);
									i = j;
									start = j;
									length = 0;
									goto done;
							}
						}
						i = identifier.Length;

done:
						break;
					case UnicodeCategory.LowercaseLetter:
						if (length == 0)
						{
							start = i;
						}
						length++;
						break;
					case UnicodeCategory.DecimalDigitNumber:
						if (length > 0)
						{
							yield return new Range(start, length);
						}
						start = i;
						length = 1;
						for (int j = i + 1; j < identifier.Length; j++)
						{
							c = identifier[j];
							cat = GetUnicodeCategory(c);
							switch (cat)
							{
								case UnicodeCategory.DecimalDigitNumber:
									length++;
									break;
								default:
									yield return new Range(start, length);
									i = j - 1;
									start = j;
									length = 0;
									goto done2;
							}
						}
						i = identifier.Length;

done2:
						break;
					default:
						if (length > 0)
						{
							yield return new Range(start, length);
							length = 0;
						}
						break;
				}
			}
			if (length > 0)
			{
				yield return new Range(start, length);
			}
		}

		internal static bool IsAllUpper(string str)
		{
			for (int i = str.Length - 1; i >= 0; i--)
			{
				if (char.IsLower(str[i]))
					return false;
			}
			return true;
		}

		internal struct Range
		{
			public int Start { get; }
			public int Length { get; }

			public int End => Start + Length;

			public Range(int start, int length)
			{
				this.Start = start;
				this.Length = length;
			}
		}
	}

	/// <summary>
	/// The pascale identifier style.
	/// </summary>
	sealed class PascalCaseStyle : IdentifierStyle
	{
		/// <inheritdoc/>
		public override string Convert(string str)
		{
			using var sw = new StringWriter();
			bool isUpper = IsAllUpper(str);
			char last = '\0';
			foreach (var segment in GetSegments(str))
			{
				for (int i = segment.Start; i < segment.End; i++)
				{
					var c = str[i];
					if (i == segment.Start)
					{
						if (char.IsDigit(c) && char.IsDigit(last))
							sw.Write('_');
						c = char.ToUpperInvariant(c);
					}
					else
					{
						if (isUpper)
						{
							c = char.ToLowerInvariant(c);
						}
					}
					sw.Write(c);
					last = c;
				}
			}
			return sw.ToString();
		}
	}

	/// <summary>
	/// The camel case identifier style.
	/// </summary>
	sealed class CamelCaseStyle : IdentifierStyle
	{
		/// <inheritdoc/>
		public override string Convert(string str)
		{
			using var sw = new StringWriter();
			bool isUpper = IsAllUpper(str);

			bool first = true;
			char last = '\0';
			foreach (var segment in GetSegments(str))
			{
				for (int i = segment.Start; i < segment.End; i++)
				{
					var c = str[i];
					if (first)
					{
						c = char.ToLowerInvariant(c);
					}
					else
					{
						if (i == segment.Start)
						{
							if (char.IsDigit(c) && char.IsDigit(last))
								sw.Write('_');

							c = char.ToUpperInvariant(c);
						}
						else
						{
							if (isUpper)
							{
								c = char.ToLowerInvariant(c);
							}
						}
					}
					sw.Write(c);
					last = c;
				}
				first = false;
			}
			return sw.ToString();
		}
	}

	/// <summary>
	/// The casing style used within segments.
	/// </summary>
	enum CasingStyle
	{
		/// <summary>
		/// Use the casing of the original identifier.
		/// </summary>
		Unchanged = 0,
		/// <summary>
		/// UpperCase every character.
		/// </summary>
		UpperCase,
		/// <summary>
		/// LowerCase every character.
		/// </summary>
		LowerCase,
		/// <summary>
		/// UpperCase first character, and lowercase the rest.
		/// </summary>
		TitleCase,
	}

}
