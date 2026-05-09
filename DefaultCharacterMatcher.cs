using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SkiaSharp;

namespace Topten.RichTextKit;

internal class DefaultCharacterMatcher : ICharacterMatcher
{
	private SKFontManager _fontManager = SKFontManager.Default;

	private static SKTypeface _emojiFallback;
	private static bool _emojiLookupAttempted;
	private static readonly object _emojiLock = new object();

	private static List<SKTypeface> _bundledFonts;
	private static bool _bundledScanned;
	private static readonly object _bundledLock = new object();

	private static readonly ConcurrentDictionary<int, SKTypeface> _charFallbackCache
		= new ConcurrentDictionary<int, SKTypeface>();

	private static IEnumerable<string> CandidateFontDirs()
	{
		string baseDir = AppDomain.CurrentDomain.BaseDirectory;
		if (string.IsNullOrEmpty(baseDir))
		{
			baseDir = Environment.CurrentDirectory;
		}
		string cwd = Environment.CurrentDirectory;
		string[] roots = new string[]
		{
			Path.Combine(baseDir, "..", ".."),
			Path.Combine(baseDir, "..", "..", ".."),
			cwd,
			Path.Combine(cwd, "..", ".."),
			Path.Combine(cwd, "..", "..", ".."),
		};
		string[] subPaths = new string[]
		{
			Path.Combine("download", "assets", "fonts"),
			Path.Combine("addons", "base", "Assets", "fonts"),
		};
		foreach (string root in roots)
		{
			foreach (string sub in subPaths)
			{
				yield return Path.Combine(root, sub);
			}
		}
	}

	private static SKTypeface LoadEmbeddedEmoji()
	{
		try
		{
			var asm = typeof(DefaultCharacterMatcher).Assembly;
			using var stream = asm.GetManifestResourceStream("Topten.RichTextKit.Resources.W10Emoji.ttf");
			if (stream == null)
			{
				return null;
			}
			using var ms = new MemoryStream();
			stream.CopyTo(ms);
			ms.Position = 0;
			return SKTypeface.FromStream(ms);
		}
		catch
		{
			return null;
		}
	}

	private static SKTypeface GetEmojiFallback()
	{
		if (_emojiLookupAttempted)
		{
			return _emojiFallback;
		}
		lock (_emojiLock)
		{
			if (_emojiLookupAttempted)
			{
				return _emojiFallback;
			}
			_emojiLookupAttempted = true;

			try
			{
				foreach (string dir in CandidateFontDirs())
				{
					if (!Directory.Exists(dir))
					{
						continue;
					}
					string[] files = Directory.GetFiles(dir, "w10emoji*.ttf");
					if (files.Length == 0)
					{
						continue;
					}
					_emojiFallback = SKTypeface.FromFile(files[0]);
					if (_emojiFallback != null)
					{
						return _emojiFallback;
					}
				}
			}
			catch
			{
			}

			_emojiFallback = LoadEmbeddedEmoji();
			return _emojiFallback;
		}
	}

	private static List<SKTypeface> GetBundledFonts()
	{
		if (_bundledScanned)
		{
			return _bundledFonts;
		}
		lock (_bundledLock)
		{
			if (_bundledScanned)
			{
				return _bundledFonts;
			}
			_bundledScanned = true;
			var list = new List<SKTypeface>();
			var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				foreach (string dir in CandidateFontDirs())
				{
					if (!Directory.Exists(dir))
					{
						continue;
					}
					string realDir = Path.GetFullPath(dir);
					var files = Directory.GetFiles(realDir, "*.ttf").Concat(Directory.GetFiles(realDir, "*.otf"));
					foreach (var file in files)
					{
						if (!seenPaths.Add(file))
						{
							continue;
						}
						try
						{
							var tf = SKTypeface.FromFile(file);
							if (tf != null)
							{
								list.Add(tf);
							}
						}
						catch
						{
						}
					}
				}
			}
			catch
			{
			}
			_bundledFonts = list;
			return list;
		}
	}

	private static SKTypeface FindBundledFallback(int character)
	{
		if (_charFallbackCache.TryGetValue(character, out var cached))
		{
			return cached;
		}
		var fonts = GetBundledFonts();
		SKTypeface result = null;
		foreach (var tf in fonts)
		{
			try
			{
				using var font = new SKFont(tf);
				if (font.GetGlyph(character) != 0)
				{
					result = tf;
					break;
				}
			}
			catch
			{
			}
		}
		_charFallbackCache[character] = result;
		return result;
	}

	public SKTypeface MatchCharacter(string familyName, int weight, int width, SKFontStyleSlant slant, string[] bcp47, int character)
	{
		if (character >= 0x2300)
		{
			SKTypeface emoji = GetEmojiFallback();
			if (emoji != null)
			{
				using SKFont f = new SKFont(emoji);
				if (f.GetGlyph(character) != 0)
				{
					return emoji;
				}
			}
		}
		SKTypeface tf = _fontManager.MatchCharacter(familyName, weight, width, slant, bcp47, character);
		if (tf != null)
		{
			return tf;
		}
		return FindBundledFallback(character);
	}
}
