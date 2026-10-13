using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NzbDrone.Core.Parser
{
    /// <summary>
    /// One part of a book that was published as several separate releases, such as
    /// GraphicAudio's "Fourth Wing (Part 1 of 2)" / "Fourth Wing (Part 2 of 2)".
    /// Each part is its own download; together they make up the whole book.
    /// </summary>
    public sealed class ReleaseSplitPart : IEquatable<ReleaseSplitPart>
    {
        public ReleaseSplitPart(int part, int count)
        {
            Part = part;
            Count = count;
        }

        public int Part { get; }

        /// <summary>Total number of parts, or 0 when the release only says "Part N".</summary>
        public int Count { get; }

        /// <summary>
        /// True when both describe parts of the same split, but not the same part. A part
        /// without a known total is treated as compatible with any total.
        /// </summary>
        public bool IsSiblingOf(ReleaseSplitPart other)
        {
            return other != null &&
                   Part != other.Part &&
                   (Count == 0 || other.Count == 0 || Count == other.Count);
        }

        public bool Equals(ReleaseSplitPart other) => other != null && Part == other.Part && Count == other.Count;

        public override bool Equals(object obj) => Equals(obj as ReleaseSplitPart);

        public override int GetHashCode() => HashCode.Combine(Part, Count);

        public override string ToString() => Count > 0 ? $"Part {Part} of {Count}" : $"Part {Part}";
    }

    public static class ReleaseSplitPartParser
    {
        private const int MaxParts = 20;

        private const string Number = @"\d{1,2}|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve";

        // "Part 1 of 2", "Pt. 1 of 2", "Part 1/2", "Part One of Two", "part x of X"
        private static readonly Regex PartOfRegex = new Regex(
            $@"\b(?:part|pt)\.?\s*#?(?<part>{Number})\s*(?:of|/)\s*(?<count>{Number})\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // "(1 of 2)", "[1 of 2]", "{1 of 2}" — a bare "N of M" only counts when bracketed, so a
        // series position, disc or chapter ("Book 1 of 3", "Disc 2 of 10") is never a part.
        private static readonly Regex BracketedOfRegex = new Regex(
            $@"[\(\[\{{]\s*(?<part>{Number})\s*of\s*(?<count>{Number})\s*[\)\]\}}]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // "Part 1", "Pt 2" with no total.
        private static readonly Regex PartOnlyRegex = new Regex(
            $@"\b(?:part|pt)\.?\s*#?(?<part>{Number})\b(?!\s*(?:of|/))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Dictionary<string, int> Words = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "one", 1 }, { "two", 2 }, { "three", 3 }, { "four", 4 }, { "five", 5 }, { "six", 6 },
            { "seven", 7 }, { "eight", 8 }, { "nine", 9 }, { "ten", 10 }, { "eleven", 11 }, { "twelve", 12 }
        };

        /// <summary>
        /// Reads the split-release part from a release title, folder or file name.
        /// Returns null when the title is not one part of a split book.
        /// </summary>
        /// <param name="title">The release title, folder name or file name (without extension).</param>
        /// <param name="fileName">
        /// The text is a single file's name. Only an explicit "Part N of M" counts then: inside a
        /// multi-file release, "(3 of 12)" or "Part 3" number tracks, not separate releases.
        /// </param>
        public static ReleaseSplitPart Parse(string title, bool fileName = false)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            var match = PartOfRegex.Match(title);
            if (!match.Success && !fileName)
            {
                match = BracketedOfRegex.Match(title);
            }

            if (match.Success)
            {
                var part = ToNumber(match.Groups["part"].Value);
                var count = ToNumber(match.Groups["count"].Value);

                return part >= 1 && count >= 2 && part <= count && count <= MaxParts
                    ? new ReleaseSplitPart(part, count)
                    : null;
            }

            if (fileName)
            {
                return null;
            }

            match = PartOnlyRegex.Match(title);
            if (match.Success)
            {
                var part = ToNumber(match.Groups["part"].Value);
                return part >= 1 && part <= MaxParts ? new ReleaseSplitPart(part, 0) : null;
            }

            return null;
        }

        private static int ToNumber(string value)
        {
            if (int.TryParse(value, out var number))
            {
                return number;
            }

            return Words.TryGetValue(value, out number) ? number : 0;
        }
    }
}
