using Microsoft.AspNetCore.Components;
using System.Text.RegularExpressions;

namespace ExecutiveSummary
{
    public static class Extensions
    {
        private static Regex urlRegex = new Regex("[A-Za-z]+:\\/\\/[A-Za-z0-9\\-_]+\\.[A-Za-z0-9\\-_:%&;\\?\\#\\/.=]+");
        //extension method for a string type that will find any embedded urls
        public static MarkupString WithLinks(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return (MarkupString)string.Empty;
            }
            value.Trim();


            var matches = urlRegex.Matches(value);
            if (matches == null || matches.Count == 0)
            {
                return (MarkupString)value;
            }
            foreach (var match in matches)
            {
                value = value.Replace(match.ToString(), $"<small><a href=\"{match}\" target=\"_blank\">{match}</a></small>");
            }
            value = value.Replace("\n\n", "<br/>");
            value = value.Replace("\n", "<br/>");
            return (MarkupString)value;
        }
        public static bool IsUrl(this string value)
        {
            return urlRegex.IsMatch(value);
        }
        public static MarkupString AddLinksAndTitles(this MarkupString value)
        {
            return AddLinksAndTitles(value.ToString());
        }
        public static MarkupString AddLinksAndTitles(this string value)
        {
            value = value.WithLinks().ToString();
            var titles = new List<string> { "Business Priorities:", "References:", "Bio:", "Key Insights:", "Summary for quarterly report:", "Summary for 10K:",
                        "Title:", "Article Summary:", "Article Insights:","Page URL:"};
            foreach (var title in titles)
            {
                value = value.Replace(title, $"</br><b>{title}</b></br>");
            }
            return (MarkupString)value;

        }
    }
}
