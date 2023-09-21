using System.Text.Json.Serialization;

namespace ExecutiveSummary.Model
{
    public class Executive
    {
        //regex to find the word Title
        //(?<=Title:)(.*)(?=Priorities:)
        //regex to find the word Priorities
        //(?<=Priorities:)(.*)(?=Bio:)
        //regex to find the word Bio
        //(?<=Bio:)(.*)


        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonIgnore]
        public bool Selected { get; set; }

        [JsonPropertyName("biography")]
        public string Bio { get; set; } = string.Empty;

        [JsonPropertyName("companyname")]
        public string CompanyName { get; set; }

        private List<string> _priorities = new List<string>();
        [JsonPropertyName("priorities")]
        public List<string> Priorities
        {
            get
            {
                return _priorities;
            }
            set
            {
                _priorities = value;
            }
        }


        private List<string> _references = new List<string>();
        [JsonPropertyName("references")]
        public List<string> References
        {
            get
            {
                return _references;
            }
            set
            {
                _references = value;
            }
        }

        [JsonIgnore]
        public string[] PriorityLines
        {
            get
            {
                if (Priorities != null && Priorities.Count > 0)
                {
                    return Priorities.ToArray();
                }
                else
                {
                    return new string[0];
                }
            }
        }

    }

}