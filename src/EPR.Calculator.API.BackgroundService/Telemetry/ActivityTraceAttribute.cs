using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using EPR.Calculator.API.BackgroundService.Telemetry.Helpers;
using EPR.Calculator.API.BackgroundService.Utils;
using Metalama.Framework.Aspects;
using Metalama.Framework.Code;

namespace EPR.Calculator.API.BackgroundService.Telemetry;

/// <summary>
///     Calls to the target method will be recorded as an activity trace.
/// </summary>
/// <remarks>
///     If the target type has an <see cref="ITelemetry" /> constructor parameter, that instance is used; otherwise, a
///     shared <see cref="Telemetry{TCategory}" /> instance categorised under the target type is used instead.
/// </remarks>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ActivityTraceAttribute : TelemetryAtribute
{
    private readonly string? argActivityName;
    private readonly string? argThreshold;

    /// <param name="activityName">
    ///     The name of the associated activity as shown in traces. Will default to the target method name.
    /// </param>
    /// <param name="threshold">
    ///     A duration threshold to compare against the activity, used to identify slow calls. Will default
    ///     to <see cref="Telemetry.DefaultDurationThreshold" />.
    /// </param>
    // ReSharper disable once ConvertToPrimaryConstructor - Not supported by Metalama
    public ActivityTraceAttribute(string? activityName = null, string? threshold = null)
    {
        argActivityName = activityName;
        argThreshold = threshold;
    }

    public override dynamic? OverrideMethod()
    {
        var activityName = argActivityName ?? meta.Target.Method.Name;
        TimeSpan? threshold = argThreshold != null ? TimeSpan.Parse(argThreshold, CultureInfo.InvariantCulture) : null;
        ITelemetry telemetry = GetTelemetryExpression(meta.Target.Type).Value!;
        return telemetry.Activity(Proceed, threshold, activityName);

        object? Proceed()
        {
            var result = meta.Proceed();

            // SpecialType.Void means that the target method returns void rather than T
            return meta.Target.Method.GetAsyncInfo().ResultType.Equals(SpecialType.Void)
                ? Unit.Value
                : result;
        }
    }

    public override async Task<dynamic?> OverrideAsyncMethod()
    {
        var activityName = argActivityName ?? meta.Target.Method.Name;
        TimeSpan? threshold = argThreshold != null ? TimeSpan.Parse(argThreshold, CultureInfo.InvariantCulture) : null;
        ITelemetry telemetry = GetTelemetryExpression(meta.Target.Type).Value!;
        return await telemetry.Activity(Proceed, threshold, activityName);

        async Task<object?> Proceed()
        {
            var result = await meta.ProceedAsync();

            // SpecialType.Void means that the target method returns Task rather than Task<T>
            return meta.Target.Method.GetAsyncInfo().ResultType.Equals(SpecialType.Void)
                ? Unit.Value
                : result;
        }
    }
}
