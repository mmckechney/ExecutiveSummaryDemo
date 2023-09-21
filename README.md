# Executive Summary Demo

This demo app uses Azure OpenAI to simulate the preparation of summary notes for an executive meeting. To leverage Azure OpenAI, the code uses the [Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/whatissk) to leverage existing skills and create new ones to search for, extract, summarize and gleen insights about company and the company's exectives that you select. 

You can also get started with Semantic Kernel by reviewing the [GitHub repo for the project](https://github.com/microsoft/semantic-kernel) to review both the source code and sample projects.

## Get Started

First, fork this repository, then build and deploy the web app to an Azure Web App.

Once deployed, update the application configuration with the following keys:

- `Skills:BingApiKey` - this will be the API key for a [Bing search](https://learn.microsoft.com/en-us/bing/search-apis/bing-web-search/create-bing-search-service-resource) resource.
- `AzureOpenAI:Chat:ApiKey` - API key for your [Azure OpenAI](https://learn.microsoft.com/en-us/azure/cognitive-services/openai/overview#how-do-i-get-access-to-azure-openai) instance

Since these aren't application secrets, you can also choose to either add these as application configuration or update the appsettings.json file and re-deploy

- `AzureOpenAI:Chat:Label` - the LLM Model name you want to use
- `AzureOpenAI:Chat:Endpoint` - the URL for the Azure OpenAI instance
- `AzureOpenAI:DeploymentName` - the deployment name you have associated with the model


## Running the Demo

1. Open the URL created for your Azure Web App. You should see a Blazor web app that looks like this:

![initial page](/Images/demo-landing.png)

2. Enter a company name and click "Go". This will search for, extract and display a list of executives for the company
3. Enter a meeting topic then click "Generate Meeting Prep Notes". This will then generate the meeting notes.

This diagram provides a detailed workflow of how the demo uses various skills to extract the information and generate the notes:

![Workflow](/Images/Workflow.png)