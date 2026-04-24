using System.Collections.Generic;
using Godot;
using LadyBug.Gameplay.Gates;

/// <summary>
/// Runtime owner of the rotating gate views and logical gate state for one level.
/// </summary>
/// <remarks>
/// Gates are authored as <see cref="RotatingGateView"/> instances directly under
/// the Level/Gates node. This class converts those placed views into the runtime
/// <see cref="GateSystem"/>, keeps visual gate instances synchronized with their
/// runtime state, and exposes the small operations needed by movement code:
/// ticking gate timers and pushing one gate.
///
/// Keeping this logic outside <c>Level</c> lets the level remain a coordinator
/// instead of owning every detail of gate authoring, runtime state, and rendering.
/// </remarks>
public sealed class LevelGateRuntime
{
    private readonly Node2D _gatesRoot;
    private readonly Dictionary<int, RotatingGateView> _gateViewsById = new();

    private GateSystem _gateSystem = null!;

    /// <summary>
    /// Gets the runtime logical gate system built from the placed gate views.
    /// </summary>
    public GateSystem GateSystem => _gateSystem;

    /// <summary>
    /// Creates a gate runtime wrapper for the placed gate views under the given root node.
    /// </summary>
    /// <param name="gatesRoot">The Level/Gates node containing placed gate views.</param>
    public LevelGateRuntime(Node2D gatesRoot)
    {
        _gatesRoot = gatesRoot;
        CachePlacedGateViews();
    }

    /// <summary>
    /// Reapplies the editor-authored gate definitions to every placed gate view.
    /// </summary>
    public void RefreshPlacedGateViewsFromDefinitions()
    {
        foreach (RotatingGateView gateView in _gateViewsById.Values)
        {
            gateView.RefreshFromDefinition();
        }
    }

    /// <summary>
    /// Builds the runtime <see cref="GateSystem"/> from the currently placed gate views.
    /// </summary>
    /// <remarks>
    /// This should be called once during runtime level initialization, after the
    /// scene-authored gate definitions have been refreshed.
    /// </remarks>
    public void BuildRuntimeGateSystem()
    {
        List<RotatingGateRuntimeState> gateStates = new();

        foreach (RotatingGateView gateView in _gateViewsById.Values)
        {
            gateStates.Add(gateView.CreateInitialRuntimeState());
        }

        _gateSystem = GateSystem.FromRuntimeStates(gateStates);
    }

    /// <summary>
    /// Advances all rotating-gate timers by one simulation tick and refreshes views.
    /// </summary>
    public void AdvanceOneTick()
    {
        _gateSystem.AdvanceOneTick();
        SyncGateViewsFromRuntimeState();
    }

    /// <summary>
    /// Attempts to push one gate and immediately syncs visuals if the push succeeds.
    /// </summary>
    /// <param name="gateId">Identifier of the gate to push.</param>
    /// <param name="moveDir">Attempted movement direction.</param>
    /// <param name="contactHalf">Half of the gate being pushed.</param>
    /// <returns>True if the push was accepted; otherwise false.</returns>
    public bool TryPushGate(int gateId, Vector2I moveDir, GateContactHalf contactHalf)
    {
        bool pushed = _gateSystem.TryPush(gateId, moveDir, contactHalf);
        if (pushed)
            SyncGateViewsFromRuntimeState();

        return pushed;
    }

    /// <summary>
    /// Refreshes all gate views so they match the current runtime gate state.
    /// </summary>
    public void SyncGateViewsFromRuntimeState()
    {
        foreach (RotatingGateRuntimeState gateState in _gateSystem.Gates)
        {
            if (!_gateViewsById.TryGetValue(gateState.Id, out RotatingGateView? gateView))
                continue;

            if (gateState.VisualState == GateVisualState.Turning)
            {
                gateView.SetTurningVisual(gateState.TurningVisual);
            }
            else
            {
                gateView.SetOrientation(gateState.GetStableOrientation());
            }
        }
    }

    /// <summary>
    /// Rebuilds the lookup of gate views placed under the Gates node.
    /// </summary>
    private void CachePlacedGateViews()
    {
        _gateViewsById.Clear();

        foreach (Node child in _gatesRoot.GetChildren())
        {
            if (child is not RotatingGateView gateView)
                continue;

            if (_gateViewsById.ContainsKey(gateView.GateId))
            {
                GD.PushError($"Duplicate rotating gate id '{gateView.GateId}' in Gates node.");
                continue;
            }

            _gateViewsById.Add(gateView.GateId, gateView);
        }
    }
}
