using System;
using System.Collections.Generic;
using System.Text.Json;
using Audeu.EplanAi.Contracts;
using Xunit;

namespace Audeu.EplanAi.Contracts.Tests;

public sealed class ProtocolContractTests
{
    [Fact]
    public void ProtocolVersion_IsPinnedTo_1_0()
    {
        Assert.Equal("1.0", ProtocolConstants.Version);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("0.9", false)]
    [InlineData("1.0", true)]
    [InlineData("1.1", false)]
    public void ProtocolVersion_SupportDetection_IsExplicit(string version, bool expected)
    {
        Assert.Equal(expected, ProtocolConstants.IsSupported(version));
    }

    [Fact]
    public void BridgeRequest_DefaultsToCurrentProtocolAndEmptyArguments()
    {
        var request = new BridgeRequest();

        Assert.Equal(ProtocolConstants.Version, request.ProtocolVersion);
        Assert.NotNull(request.Arguments);
        Assert.Empty(request.Arguments);
    }

    [Fact]
    public void BridgeRequest_JsonRoundTripPreservesRequestIdAndArguments()
    {
        var requestId = Guid.NewGuid();
        var request = new BridgeRequest
        {
            RequestId = requestId,
            Operation = "system.status",
            Mode = "read",
            Arguments = new Dictionary<string, string>
            {
                ["key"] = "value"
            }
        };

        var json = JsonSerializer.Serialize(request);
        var roundTripped = JsonSerializer.Deserialize<BridgeRequest>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(requestId, roundTripped.RequestId);
        Assert.Equal("system.status", roundTripped.Operation);
        Assert.Equal("read", roundTripped.Mode);
        Assert.Equal("value", roundTripped.Arguments["key"]);
    }

    [Fact]
    public void BridgeResponse_DefaultsToCurrentProtocolAndEmptyWarnings()
    {
        var response = new BridgeResponse();

        Assert.Equal(ProtocolConstants.Version, response.ProtocolVersion);
        Assert.NotNull(response.Warnings);
        Assert.Empty(response.Warnings);
    }
}
