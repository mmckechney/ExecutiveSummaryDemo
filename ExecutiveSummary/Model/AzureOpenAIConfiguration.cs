// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

[SuppressMessage("Performance", "CA1812:class never instantiated", Justification = "Instantiated through IConfiguration")]
internal sealed class AzureOpenAIConfiguration
{
    public string Label { get; set; }

    public string DeploymentName { get; set; }

    public string Endpoint { get; set; }

    public string ApiKey { get; set; }

    public AzureOpenAIConfiguration(string label, string deploymentName, string endpoint, string apiKey)
    {
        Label = label;
        DeploymentName = deploymentName;
        Endpoint = endpoint;
        ApiKey = apiKey;
    }
}
