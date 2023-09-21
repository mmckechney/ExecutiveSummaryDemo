namespace ExecutiveSummary.Model
{
    public class Article
    {
        public string Url { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Insights { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        public string[] InsightLines
        {
            get
            {
                if (Insights.Length > 0)
                {
                    var lines = Insights.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                    return lines;

                }
                else
                {
                    return new string[0];
                }
            }
        }

    }
}
