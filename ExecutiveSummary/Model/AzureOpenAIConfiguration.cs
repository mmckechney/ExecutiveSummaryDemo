// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

[SuppressMessage("Performance", "CA1812:class never instantiated", Justification = "Instantiated through IConfiguration")]
internal sealed class AzureOpenAIConfiguration
{
    public string ModelName { get; set; }

    public string DeploymentName { get; set; }

    public string Endpoint { get; set; }

    public string ApiKey { get; set; }

    public AzureOpenAIConfiguration(string modelName, string deploymentName, string endpoint, string apiKey)
    {
        ModelName = modelName;
        DeploymentName = deploymentName;
        Endpoint = endpoint;
        ApiKey = apiKey;
    }
}
