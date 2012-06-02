using System.Text.RegularExpressions;

using VirtualFileSystem.Annotations;


namespace VirtualFileSystem.Utilities
{
    /// <summary>
    /// Simplest possible implementation of file search pattern, using regular expressions.
    /// </summary>
    public sealed class SearchPattern
    {
        private readonly Regex _pattern;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchPattern"/> class.
        /// </summary>
        /// <param name="pattern">The pattern.</param>
        public SearchPattern([NotNull] string pattern)
        {
            Validate.ArgumentNotNull(pattern, "pattern");

            _pattern = new Regex(WildcardToRegularExpression(pattern), RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }


        /// <summary>
        /// Converts specified pattern to regular expression.
        /// </summary>
        [NotNull]
        private static string WildcardToRegularExpression([NotNull]string pattern)
        {
            Validate.ArgumentNotNull(pattern, "pattern");

            return "^" + Regex.Escape(pattern).
            Replace("\\*", ".*").
            Replace("\\?", ".") + "$";
        }


        /// <summary>
        /// Checks if specified file name matches regular expression.
        /// </summary>
        public bool Match([NotNull] string name)
        {
            Validate.ArgumentNotNull(name, "name");

            return _pattern.IsMatch(name);
        }
    }
}