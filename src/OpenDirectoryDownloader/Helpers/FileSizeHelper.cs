﻿using NLog;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenDirectoryDownloader.Helpers;

public static class FileSizeHelper
{
	public static readonly Logger Logger = LogManager.GetCurrentClassLogger();

	// Parse a file size.
	private static readonly string[][] SizeSuffixes =
	{
			new string[] { "BYTES", "BYTE", "B", "OCTETS", "OCTET" },
			new string[] { "KB", "K", "KIB", "KO" },
			new string[] { "MB", "M", "MIB", "MO" },
			new string[] { "GB", "G", "GIB", "GO" },
			new string[] { "TB", "T", "TIB", "TO" },
			new string[] { "PB", "P", "PIB", "PO" },
			new string[] { "EB", "E", "EIB", "EO" },
			new string[] { "ZB", "Z", "ZIB", "ZO" },
			new string[] { "YB", "Y", "YIB", "YO" }
		};
	private static readonly Regex AlphaNumericRegex = new Regex("[^a-zA-Z0-9 .,]");
	private static readonly Regex BytesRegex = new Regex("\\((?<FileSize>(\\d*,?)+)bytes\\)");

	public static long ParseFileSize(string value, int kbValue = 1024, bool throwException = false, bool onlyChecking = false)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return Constants.NoFileSize;
		}

		// Strip HTML
		value = Regex.Replace(value, "<.*?>", string.Empty);

		Match bytesRegexMatch = BytesRegex.Match(value);

		if (bytesRegexMatch.Success)
		{
			return long.Parse(bytesRegexMatch.Groups["FileSize"].Value.Replace(",", string.Empty));
		}

		value = AlphaNumericRegex.Replace(value, string.Empty);

		// Remove leading and trailing spaces.
		value = value.Trim();

		if (string.IsNullOrWhiteSpace(value))
		{
			return Constants.NoFileSize;
		}

		try
		{
			// Find the last non-alphabetic character.
			int ext_start = 0;

			for (int i = value.Length - 1; i >= 0; i--)
			{
				// Stop if we find something other than a letter.
				if (!char.IsLetter(value, i))
				{
					ext_start = i + 1;
					break;
				}
			}

			// Get the numeric part.
			if (double.TryParse(value.AsSpan(0, ext_start), NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
			{
				// Get the extension.
				string suffix;

				if (ext_start < value.Length)
				{
					suffix = value[ext_start..].Trim().ToUpper();
				}
				else
				{
					suffix = "BYTES";
				}

				// Find the extension in the list.
				int suffix_index = -1;

				for (int i = 0; i < SizeSuffixes.Length; i++)
				{
					if (SizeSuffixes[i].ToList().Contains(suffix))
					{
						suffix_index = i;
						break;
					}
				}

				if (suffix_index < 0)
				{
					throw new FormatException($"Unknown file size extension {suffix}, value: {value}.");
				}

				// Return the result.
				return (long)Math.Round(number * Math.Pow(kbValue, suffix_index));
			}

			return Constants.NoFileSize;
		}
		catch (Exception ex)
		{
			if (throwException)
			{
				throw new FormatException($"Invalid file size format, value: {value}", ex);
			}
			else
			{
				if (!onlyChecking)
				{
					Logger.Warn($"Cannot parse \"{value}\" as a filesize.");
				}

				return Constants.NoFileSize;
			}
		}
	}

	private static readonly string[] sizeSuffixes = { "B", "kiB", "MiB", "GiB", "TiB", "PiB", "EiB", "ZiB", "YiB" };
	private static readonly string[] sizeSuffixesBit = { "b", "kib", "Mib", "Gib", "Tib", "Pib", "Eib", "Zib", "Yib" };

	public static string ToHumanReadable(long? size, bool useBits = false)
	{
		Debug.Assert(sizeSuffixes.Length > 0);

		const string formatTemplate = "{0}{1:#.##} {2}";

		if (size == null)
		{
			return "-";
		}

		if (size == 0)
		{
			return string.Format(formatTemplate, null, 0, sizeSuffixes[0]);
		}

		double absSize = Math.Abs((double)size);
		double fpPower = Math.Log(absSize, 1000);
		int intPower = (int)fpPower;
		int iUnit = intPower >= sizeSuffixes.Length
			? sizeSuffixes.Length - 1
			: intPower;
		double normSize = absSize / Math.Pow(1024, iUnit);

		return string.Format(formatTemplate, size < 0 ? "-" : null, normSize, (useBits ? sizeSuffixesBit : sizeSuffixes)[iUnit]);
	}

}
