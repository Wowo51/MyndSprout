//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using System.Runtime.ExceptionServices;

namespace SqlContain;

public enum Scope
{
    Instance,
    Database,
    Both
}

public sealed class HardenerOptions
{
    public string Server { get; set; } = "";
    public string Auth { get; set; } = "";
    public string Database { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public bool InternalFirewallOnly { get; set; }

    public string? SqlServrPath { get; set; } = null;
    public Scope Scope { get; set; } = Scope.Database;
    public bool Firewall { get; set; } = false;

    /// <summary>
    /// Default false: any skipped DENY statements will cause a failure (AggregateException).
    /// Set true only for explicit niche overrides.
    /// </summary>
    public bool AllowSkippedDeny { get; set; } = false;

    /// <summary>
    /// Default false: if true, absence of supported trigger events will not cause failure and trigger-creation will be skipped.
    /// Set true only to opt into environments that legitimately report no supported database trigger events.
    /// </summary>
    public bool AllowMissingTrigger { get; set; } = false;

    public bool DisallowUse { get; set; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(this.Server) && !this.InternalFirewallOnly)
        {
            ArgumentException ex = new ArgumentException("Server is required unless InternalFirewallOnly is true.", nameof(Server));
            ExceptionDispatchInfo.Capture(ex).Throw();
        }

        // in Validate(): ensure Database required when Scope contains Database
        if ((this.Scope == Scope.Database || this.Scope == Scope.Both) && string.IsNullOrWhiteSpace(this.Database))
        {
            ArgumentException ex = new ArgumentException("Database must be specified when Scope includes Database.", nameof(Database));
            ExceptionDispatchInfo.Capture(ex).Throw();
        }

        return;
    }
}
