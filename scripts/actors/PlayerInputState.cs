using Godot;

namespace LadyBug.Actors;

/// <summary>
/// Stores directional input state for the player.
/// </summary>
/// <remarks>
/// This class implements the "last pressed wins" rule used by the current
/// movement controller.
///
/// It tracks:
/// - which movement directions are currently held
/// - the relative order in which they were pressed
///
/// The most recently pressed direction that is still held is returned as the
/// current intended direction.
/// </remarks>
public sealed class PlayerInputState
{
    // Raw pressed-state tracking for each cardinal direction.
    private bool _leftPressed;
    private bool _rightPressed;
    private bool _upPressed;
    private bool _downPressed;

    // Press order values used to decide which held direction wins.
    private long _leftPressOrder;
    private long _rightPressOrder;
    private long _upPressOrder;
    private long _downPressOrder;

    // Monotonic counter incremented each time a direction is pressed.
    private long _pressOrderCounter;

    /// <summary>
    /// Rebuilds the input state from the movement actions currently held.
    /// </summary>
    public void InitializeFromCurrentInput()
    {
        _leftPressed = Input.IsActionPressed("move_left");
        _rightPressed = Input.IsActionPressed("move_right");
        _upPressed = Input.IsActionPressed("move_up");
        _downPressed = Input.IsActionPressed("move_down");

        _leftPressOrder = 0;
        _rightPressOrder = 0;
        _upPressOrder = 0;
        _downPressOrder = 0;
        _pressOrderCounter = 0;
    }

    /// <summary>
    /// Updates the directional input state from one Godot input event.
    /// </summary>
    /// <param name="event">Input event received from Godot.</param>
    public void Update(InputEvent @event)
    {
        UpdateDirectionActionState(@event, "move_left", Vector2I.Left);
        UpdateDirectionActionState(@event, "move_right", Vector2I.Right);
        UpdateDirectionActionState(@event, "move_up", Vector2I.Up);
        UpdateDirectionActionState(@event, "move_down", Vector2I.Down);
    }

    /// <summary>
    /// Returns the currently active intended direction.
    /// </summary>
    /// <returns>
    /// The last pressed direction that is still held, or <c>Vector2I.Zero</c>
    /// if no movement input is currently active.
    /// </returns>
    public Vector2I ReadPressedDirection()
    {
        Vector2I bestDirection = Vector2I.Zero;
        long bestOrder = long.MinValue;

        if (_leftPressed && _leftPressOrder > bestOrder)
        {
            bestOrder = _leftPressOrder;
            bestDirection = Vector2I.Left;
        }

        if (_rightPressed && _rightPressOrder > bestOrder)
        {
            bestOrder = _rightPressOrder;
            bestDirection = Vector2I.Right;
        }

        if (_upPressed && _upPressOrder > bestOrder)
        {
            bestOrder = _upPressOrder;
            bestDirection = Vector2I.Up;
        }

        if (_downPressed && _downPressOrder > bestOrder)
        {
            bestOrder = _downPressOrder;
            bestDirection = Vector2I.Down;
        }

        return bestDirection;
    }

    /// <summary>
    /// Updates raw directional pressed-state and press order for one input action.
    /// </summary>
    /// <param name="event">Input event to inspect.</param>
    /// <param name="actionName">Godot action name.</param>
    /// <param name="direction">Direction associated with that action.</param>
    private void UpdateDirectionActionState(InputEvent @event, string actionName, Vector2I direction)
    {
        if (@event.IsActionPressed(actionName))
        {
            _pressOrderCounter++;

            if (direction == Vector2I.Left)
            {
                _leftPressed = true;
                _leftPressOrder = _pressOrderCounter;
            }
            else if (direction == Vector2I.Right)
            {
                _rightPressed = true;
                _rightPressOrder = _pressOrderCounter;
            }
            else if (direction == Vector2I.Up)
            {
                _upPressed = true;
                _upPressOrder = _pressOrderCounter;
            }
            else if (direction == Vector2I.Down)
            {
                _downPressed = true;
                _downPressOrder = _pressOrderCounter;
            }
        }

        if (@event.IsActionReleased(actionName))
        {
            if (direction == Vector2I.Left)
                _leftPressed = false;
            else if (direction == Vector2I.Right)
                _rightPressed = false;
            else if (direction == Vector2I.Up)
                _upPressed = false;
            else if (direction == Vector2I.Down)
                _downPressed = false;
        }
    }
}