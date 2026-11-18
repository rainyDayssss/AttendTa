namespace ASI.Basecode.Resources.Constants
{
    /// <summary>Shared program choices and profile field options.</summary>
    public static class ProfileOptions
    {
        /// <summary>The organization's current program list; add a program here when it changes.</summary>
        public static readonly IReadOnlyList<string> Courses = new[] { "BSCS", "BSIT" };

        /// <summary>A course is one of the offered programs; the annotations read this pattern.</summary>
        public const string CoursePattern = "^(BSCS|BSIT)$";

        // Recognize legacy long labels without rewriting historical registration snapshots.
        public static string? NormalizeCourse(string? course) => course?.Trim().ToUpperInvariant() switch
        {
            "BSIT" or "BS INFORMATION TECHNOLOGY" => "BSIT",
            "BSCS" or "BS COMPUTER SCIENCE" => "BSCS",
            _ => course?.Trim()
        };

        /// <summary>Year levels offered by the form.</summary>
        public static readonly IReadOnlyList<int> YearLevels = new[] { 1, 2, 3, 4 };

        /// <summary>Lowest year level the profile rules accept.</summary>
        public const int MinYearLevel = 1;

        /// <summary>Highest year level the profile rules accept.</summary>
        public const int MaxYearLevel = 4;

        /// <summary>Sections offered by the form.</summary>
        public static readonly IReadOnlyList<string> Sections = new[] { "A", "B", "C" };

        /// <summary>Student numbers are exactly eight ASCII digits; the value stays a string so leading zeroes survive.</summary>
        public const string StudentNumberPattern = "^[0-9]{8}$";

        /// <summary>Mobile numbers are optional; a supplied Philippine number is eleven digits starting with 09.</summary>
        public const string MobileNumberPattern = "^09[0-9]{9}$";

        /// <summary>A section is one letter from the offered set; the service upper-cases what it stores.</summary>
        public const string SectionPattern = "^[A-Ca-c]$";

        /// <summary>A password is at least eight characters and carries a letter and a digit.</summary>
        public const string PasswordPattern = "^(?=.*[A-Za-z])(?=.*[0-9]).{8,}$";

        // Trim and upper-case a section without rewriting anything else; blank collapses to null.
        public static string? NormalizeSection(string? section) =>
            string.IsNullOrWhiteSpace(section) ? null : section.Trim().ToUpperInvariant();

        // Suffixes that read as numerals rather than words: a name ending in III keeps its capitals.
        private static readonly string[] NameSuffixes = { "II", "III", "IV", "V", "VI" };

        // Title-case a name without changing its structure: initials, hyphens and apostrophes survive.
        public static string? NormalizeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < words.Length; index++)
            {
                words[index] = TitleCaseWord(words[index]);
            }

            return string.Join(' ', words);
        }

        private static string TitleCaseWord(string word)
        {
            var bare = word.TrimEnd('.');
            if (bare.Length > 0 && Array.Exists(NameSuffixes, suffix => string.Equals(suffix, bare, StringComparison.OrdinalIgnoreCase)))
            {
                return bare.ToUpperInvariant() + word[bare.Length..];
            }

            var characters = word.ToCharArray();
            var startOfWord = true;

            for (var index = 0; index < characters.Length; index++)
            {
                var character = characters[index];
                if (char.IsWhiteSpace(character) || character == '-' || character == '\'' || character == '.')
                {
                    startOfWord = true;
                    continue;
                }

                characters[index] = startOfWord ? char.ToUpperInvariant(character) : char.ToLowerInvariant(character);
                startOfWord = false;
            }

            return new string(characters);
        }

        /// <summary>Membership types the organization uses.</summary>
        public static readonly IReadOnlyList<string> MembershipTypes = new[] { "Regular", "Developer" };
    }
}
