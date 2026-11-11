namespace Usercode;

using Turian.Engine.UI;

/// <summary>
/// Code-behind for <c>Assets/UI/showcase.ui</c>.
///
/// Uses the runtime that exists today (<c>Turian.Engine.UI</c>): a <see cref="UiController"/>
/// whose public parameterless <c>void</c> methods are auto-wired to the <c>click</c> /
/// <c>value-changed</c> attributes in the document by name, and whose public properties are
/// resolvable by data bindings such as <c>binding-text="{Status}"</c>.
/// </summary>
public class ShowcaseController : UiController
{
    private bool _animate = true;
    private float _elapsed;

    /// <summary>Bound one-way to <c>{Status}</c> on the status bar label.</summary>
    public string Status { get; private set; } = "Ready.";

    /// <summary>Bound one-way to <c>{Clock}</c>; an always-changing value that shows live refresh.</summary>
    public string Clock => $"t+{_elapsed:0.0}s";

    /// <inheritdoc/>
    public override void OnUpdate(float deltaTime) => _elapsed += deltaTime;

    // ── event handlers (referenced by name from showcase.ui) ─────────────────

    /// <summary>Handles <c>click="OnPower"</c>.</summary>
    public void OnPower() => Status = "Power button pressed.";

    /// <summary>Handles <c>click="OnRefresh"</c>.</summary>
    public void OnRefresh() => Status = "Refresh button pressed.";

    /// <summary>Handles <c>click="OnWide"</c>.</summary>
    public void OnWide() => Status = "Wide button pressed.";

    /// <summary>Handles <c>click="OnSettings"</c>.</summary>
    public void OnSettings() => Status = "Settings button pressed.";

    /// <summary>Handles <c>value-changed="OnToggleAnim"</c> on the Animate toggle.</summary>
    public void OnToggleAnim()
    {
        _animate = !_animate;
        Status = _animate ? "Gradient animation on." : "Gradient animation off.";
    }
}
