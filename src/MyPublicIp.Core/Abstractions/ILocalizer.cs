namespace MyPublicIp.Core.Abstractions;

public interface ILocalizer
{
    string Get(string key);

    string Format(string key, params object?[] args) => string.Format(Get(key), args);
}
