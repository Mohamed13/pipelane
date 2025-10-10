using System.Collections.Generic;

namespace Pipelane.Application.Abstractions;

public interface IProviderWebhookVerifier
{
    bool Verify(string provider, string payload, IReadOnlyDictionary<string, string> headers);
}
