namespace ExecutiveSummary.Model
{
    /*Write C# code for this JSON
     {
      "id": "cmpl-6oZQFQQH4kKWRemaAJTL0vJi8yh8L",
      "object": "text_completion",
      "created": 1677510583,
      "model": "text-davinci-003",
      "choices": [
        {
          "text": "\n\n[\n    {\n        \"Name\" : \"Doug McMillon\",\n        \"Position\" : \"President and Chief Executive Officer\"\n    },\n    {\n        \"Name\" : \"Molly Blakeman\",\n        \"Position\" : \"Senior Manager of Corporate Communications\"\n    },\n    {\n        \"Name\" : \"John Furner\",\n        \"Position\" : \"Chief Executive Officer of Sam's Club\"\n    },\n    {\n        \"Name\" : \"Kathryn McLay\",\n        \"Position\" : \"Chief Executive Officer of Walmart Japan\"\n    },\n    {\n        \"Name\" : \"Gisel Ruiz\",\n        \"Position\" : \"Executive Vice President and Chief Operating Officer\"\n    },\n]",
          "index": 0,
          "finish_reason": "stop",
          "logprobs": null
        }
      ],
      "usage": {
        "completion_tokens": 164,
        "prompt_tokens": 9,
        "total_tokens": 173
      }
    }
    */
    public class RawOpenAiResponse
    {
        public string id { get; set; }
        public string @object { get; set; }
        public int created { get; set; }
        public string model { get; set; }
        public Choice[] choices { get; set; }
        public Usage usage { get; set; }
    }

    public class Usage
    {
        public int completion_tokens { get; set; }
        public int prompt_tokens { get; set; }
        public int total_tokens { get; set; }
    }

    public class Choice
    {
        public string text { get; set; }
        public int index { get; set; }
        public string finish_reason { get; set; }
        public object logprobs { get; set; }
    }
}
