using System.Diagnostics.CodeAnalysis;

namespace MasteringModernCSharp.Ch03;

public sealed class SmtpSettings
{
    public required string Host { get; init; }
    public required string FromAddress { get; init; }
    public int Port { get; init; } = 587;
    public bool UseTls { get; init; } = true;

    public SmtpSettings() { }

    // Tells the compiler this constructor sets every required member.
    [SetsRequiredMembers]
    public SmtpSettings(string host, string fromAddress)
    {
        Host = host;
        FromAddress = fromAddress;
    }
}
