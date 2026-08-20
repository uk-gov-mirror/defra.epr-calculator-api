using System.Diagnostics.CodeAnalysis;
using Metalama.Framework.Aspects;
using Metalama.Framework.Code;
using Metalama.Framework.Code.SyntaxBuilders;

namespace EPR.Calculator.API.BackgroundService.Telemetry.Helpers;

[ExcludeFromCodeCoverage]
internal abstract class TelemetryAtribute : OverrideMethodAspect
{
    /// <summary>
    ///     Returns an expression yielding an <see cref="ITelemetry" /> for <paramref name="type" />: its
    ///     <see cref="ITelemetry" /> constructor parameter, if it has one, or otherwise the shared
    ///     <see cref="Telemetry{TCategory}.Instance" /> categorised under <paramref name="type" /> itself.
    /// </summary>
    protected static IExpression GetTelemetryExpression(INamedType type)
    {
        foreach (var constructor in type.Constructors)
        {
            // meta.This.telemetry would always emit `this.telemetry`, but a primary-constructor parameter is only
            // in scope via its bare (unqualified) name - it is not a member and cannot be accessed as `this.name`
            // unless separately captured into an explicit field. Parsing the name directly sidesteps that entirely.
            var telemetryParam = constructor.Parameters.FirstOrDefault(p => p.Type.IsConvertibleTo(typeof(ITelemetry)));

            if (telemetryParam != null)
                return ExpressionFactory.Parse(telemetryParam.Name);
        }

        var telemetryType = TypeFactory.GetNamedType(typeof(Telemetry<>)).MakeGenericInstance([type]);
        return telemetryType.Fields.Single(f => f.Name == nameof(Telemetry<>.Instance));
    }
}
