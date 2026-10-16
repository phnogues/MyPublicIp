using Microsoft.Windows.ApplicationModel.Resources;
using MyPublicIp.Core.Abstractions;

namespace MyPublicIp.Platform;

/// <summary>Strings from Strings/&lt;language&gt;/Resources.resw, in the user's language.</summary>
internal sealed class ResourceLocalizer : ILocalizer
{
    private readonly ResourceLoader _loader = new();

    public string Get(string key)
    {
        string value = _loader.GetString(key);
        return string.IsNullOrEmpty(value) ? key : value;
    }
}
