using System;
using System.Collections.Generic;
using Godot;

namespace LadyBug.Gameplay.Gates;

/// <summary>
/// Owns the runtime state of all rotating gates in the active level.
/// </summary>
/// <remarks>
/// This system is the runtime source of truth for:
/// - gate lookup by id
/// - gate lookup by pivot
/// - push attempts
/// - short turning-timer updates
///
/// It does not decide yet whether a movement step is blocked by a gate.
/// That playfield integration will be added in the next step.
/// </remarks>
public sealed class GateSystem
{
    private readonly List<RotatingGateRuntimeState> _gates = new();
    private readonly Dictionary<int, RotatingGateRuntimeState> _gatesById = new();
    private readonly Dictionary<Vector2I, RotatingGateRuntimeState> _gatesByPivot = new();

    /// <summary>
    /// Gets all runtime gates of the active level.
    /// </summary>
    public IReadOnlyList<RotatingGateRuntimeState> Gates => _gates;

    private GateSystem()
    {
    }

    /// <summary>
    /// Builds a runtime gate system from the deserialized gate entries in maze.json.
    /// </summary>
    /// <param name="gateDataFiles">Serialized gate definitions.</param>
    /// <returns>A fully initialized runtime gate system.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if duplicate gate ids or duplicate pivots are found.
    /// </exception>
    public static GateSystem FromDataFiles(IEnumerable<RotatingGateDataFile> gateDataFiles)
    {
        GateSystem system = new();

        foreach (RotatingGateDataFile gateData in gateDataFiles)
        {
            RotatingGateRuntimeState gate = RotatingGateRuntimeState.FromDataFile(gateData);

            if (system._gatesById.ContainsKey(gate.Id))
            {
                throw new InvalidOperationException(
                    $"Duplicate rotating gate id '{gate.Id}' found in maze data.");
            }

            if (system._gatesByPivot.ContainsKey(gate.Pivot))
            {
                throw new InvalidOperationException(
                    $"Duplicate rotating gate pivot '{gate.Pivot}' found in maze data.");
            }

            system._gates.Add(gate);
            system._gatesById.Add(gate.Id, gate);
            system._gatesByPivot.Add(gate.Pivot, gate);
        }

        return system;
    }

    /// <summary>
    /// Tries to get one runtime gate by its unique identifier.
    /// </summary>
    /// <param name="gateId">Gate identifier.</param>
    /// <param name="gate">Returned gate if found.</param>
    /// <returns>True if the gate exists; otherwise false.</returns>
    public bool TryGetGateById(int gateId, out RotatingGateRuntimeState gate)
    {
        if (_gatesById.TryGetValue(gateId, out RotatingGateRuntimeState? foundGate))
        {
            gate = foundGate;
            return true;
        }

        gate = null!;
        return false;
    }

    /// <summary>
    /// Tries to get one runtime gate by its logical pivot.
    /// </summary>
    /// <param name="pivot">Logical gate pivot.</param>
    /// <param name="gate">Returned gate if found.</param>
    /// <returns>True if the gate exists at that pivot; otherwise false.</returns>
    public bool TryGetGateByPivot(Vector2I pivot, out RotatingGateRuntimeState gate)
    {
        if (_gatesByPivot.TryGetValue(pivot, out RotatingGateRuntimeState? foundGate))
        {
            gate = foundGate;
            return true;
        }

        gate = null!;
        return false;
    }

    /// <summary>
    /// Attempts to push one gate using the attempted gameplay movement direction
    /// and contacted half.
    /// </summary>
    /// <param name="gateId">Identifier of the gate to push.</param>
    /// <param name="moveDir">Attempted one-pixel gameplay movement direction.</param>
    /// <param name="contactHalf">Half of the gate that is being pushed.</param>
    /// <returns>
    /// True if the push is accepted and the gate begins rotating;
    /// otherwise false.
    /// </returns>
    public bool TryPush(int gateId, Vector2I moveDir, GateContactHalf contactHalf)
    {
        if (!TryGetGateById(gateId, out RotatingGateRuntimeState gate))
            return false;

        return gate.TryBeginPush(moveDir, contactHalf);
    }

    /// <summary>
    /// Advances the short turning timer of every runtime gate by one simulation tick.
    /// </summary>
    public void AdvanceOneTick()
    {
        foreach (RotatingGateRuntimeState gate in _gates)
        {
            gate.AdvanceOneTick();
        }
    }
}