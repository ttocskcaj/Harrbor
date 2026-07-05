using Harrbor.Abstractions.Clients;
using Harrbor.Abstractions.Connections;
using Harrbor.Abstractions.Validation;

namespace Harrbor.Abstractions.Providers;

/// <summary>
/// Strongly-typed base for a connection provider. Reflects the schema from <typeparamref name="TSettings"/>
/// and bridges the non-generic <see cref="IConnectionProvider"/> surface to typed overrides. A built-in
/// or third-party provider only implements the small typed surface below.
/// </summary>
/// <typeparam name="TSettings">The settings class (annotated with <see cref="FieldAttribute"/>).</typeparam>
/// <typeparam name="TClient">The category client interface this provider builds.</typeparam>
public abstract class ConnectionProvider<TSettings, TClient> : IConnectionProvider
    where TSettings : class, new()
    where TClient : class
{
    public abstract string Key { get; }
    public abstract ConnectionCategory Category { get; }
    public abstract string DisplayName { get; }
    public virtual int ProbeEveryNTicks => 1;

    public Type SettingsType => typeof(TSettings);
    public IReadOnlyList<FieldDescriptor> Schema { get; } = FieldSchemaBuilder.Build(typeof(TSettings));

    protected abstract TClient CreateClient(TSettings settings, ClientBuildContext context);
    protected virtual void Validate(TSettings settings, ValidationErrors errors) { }
    protected abstract Task<ProbeResult> ProbeAsync(TClient client, CancellationToken cancellationToken);
    protected abstract Task<ConnectionTestResult> TestAsync(TClient client, CancellationToken cancellationToken);

    object IConnectionProvider.CreateClient(object settings, ClientBuildContext context)
        => CreateClient((TSettings)settings, context);

    void IConnectionProvider.Validate(object settings, ValidationErrors errors)
        => Validate((TSettings)settings, errors);

    Task<ProbeResult> IConnectionProvider.ProbeAsync(object client, CancellationToken cancellationToken)
        => ProbeAsync((TClient)client, cancellationToken);

    Task<ConnectionTestResult> IConnectionProvider.TestAsync(object client, CancellationToken cancellationToken)
        => TestAsync((TClient)client, cancellationToken);
}
