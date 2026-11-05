using TheSpanishWay.Domain.Actions.Enumerations;
using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.ValueObjects;

namespace TheSpanishWay.Domain.Actions.Entities;

/// <summary>
/// One thing a party actually did, on a particular day, with particular parameters.
/// <para>
/// An append-only record. It carries its provenance so that every change it caused can be
/// traced back here, which is what lets the game answer "why did my support fall?" without
/// replaying anything.
/// </para>
/// </summary>
public sealed class ActionExecution : Entity<ActionExecutionId>
{
    private ActionExecution(
        ActionExecutionId id,
        ActionDefinitionId definition,
        PartyId actor,
        GameDate executedOn,
        IReadOnlyDictionary<string, string> parameters,
        ActionExecutionStatus status,
        string summary)
        : base(id)
    {
        DefinitionId = definition;
        Actor = actor;
        ExecutedOn = executedOn;
        Parameters = parameters;
        Status = status;
        Summary = summary;
    }

    public ActionDefinitionId DefinitionId { get; }

    public PartyId Actor { get; }

    public GameDate ExecutedOn { get; }

    public IReadOnlyDictionary<string, string> Parameters { get; }

    public ActionExecutionStatus Status { get; }

    public string Summary { get; }

    public static ActionExecution Executed(
        ActionExecutionId id,
        ActionDefinitionId definition,
        PartyId actor,
        GameDate on,
        IReadOnlyDictionary<string, string> parameters,
        string summary) =>
        new(id, definition, actor, on, parameters, ActionExecutionStatus.Executed, summary);

    /// <summary>The provenance tag every change this action caused will carry.</summary>
    public Provenance Provenance => new(
        ChangeSource.ActionExecution, Id.ToString(), 0, Simulation.Enumerations.ChangeOrigin.Policy);
}
