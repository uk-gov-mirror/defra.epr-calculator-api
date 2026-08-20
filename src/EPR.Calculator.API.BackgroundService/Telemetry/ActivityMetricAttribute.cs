using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Globalization;
using EPR.Calculator.API.BackgroundService.Telemetry.Helpers;
using EPR.Calculator.API.BackgroundService.Utils;
using Metalama.Framework.Aspects;
using Metalama.Framework.Code;
using Metalama.Framework.Eligibility;

namespace EPR.Calculator.API.BackgroundService.Telemetry;

/// <summary>
///     Calls to the target method will be recorded as an activity trace and have the call duration recorded against the
///     specified metric, as defined in <see cref="Metrics" />.
/// </summary>
/// <remarks>
///     If the target type has an <see cref="ITelemetry" /> constructor parameter, that instance is used; otherwise, a
///     shared <see cref="Telemetry{TCategory}" /> instance categorised under the target type is used instead.
/// </remarks>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ActivityMetricAttribute : TelemetryAtribute
{
    private readonly string? argActivityName;
    private readonly string argMetricName;
    private readonly string? argThreshold;

    /// <param name="metricName">
    ///     Metric to record against, as defined in <see cref="Metrics" />.
    /// </param>
    /// <param name="activityName">
    ///     The name of the associated activity as shown in traces. Will default to the target method name.
    /// </param>
    /// <param name="threshold">
    ///     A duration threshold to compare against the activity, used to identify slow calls. Will default
    ///     to <see cref="Telemetry.DefaultDurationThreshold" />.
    /// </param>
    // ReSharper disable once ConvertToPrimaryConstructor - Not supported by Metalama
    public ActivityMetricAttribute(string metricName, string? activityName = null, string? threshold = null)
    {
        argMetricName = metricName;
        argActivityName = activityName;
        argThreshold = threshold;
    }

    public override void BuildEligibility(IEligibilityBuilder<IMethod> builder)
    {
        base.BuildEligibility(builder);

        // There currently aren't any synchronous methods that need metrics recording, so they aren't supported.
        // This generates a compile-time error if attempted.
        builder.MustSatisfy(
            m => m.GetAsyncInfo().IsAsync == true,
            m => $"{m} must be an async method, as {nameof(ActivityMetricAttribute)} only supports async methods");
    }

    // Unreachable in practice: BuildEligibility above rejects non-async methods at compile time.
    // Kept as a defensive fallback since OverrideMethod() is abstract on the base class.
    public override dynamic OverrideMethod()
        => throw new NotSupportedException($"{nameof(ActivityMetricAttribute)} only supports async methods.");

    public override async Task<dynamic?> OverrideAsyncMethod()
    {
        Histogram<double> histogram = TypeFactory.GetNamedType(typeof(Metrics)).Fields.Single(f => f.Name == argMetricName).Value!;
        var activityName = argActivityName ?? meta.Target.Method.Name;
        TimeSpan? threshold = argThreshold != null ? TimeSpan.Parse(argThreshold, CultureInfo.InvariantCulture) : null;
        ITelemetry telemetry = GetTelemetryExpression(meta.Target.Type).Value!;
        return await telemetry.Metric(histogram, Proceed, threshold, activityName);

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
