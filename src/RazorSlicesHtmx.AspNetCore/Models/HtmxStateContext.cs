namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// A scoped service that holds <see cref="IHasState"/> instances registered during the current request
/// for inline rendering on full-page responses.
/// </summary>
/// <remarks>
/// Workspace slices inject this service and call <see cref="Serialize{TState}"/> to render either the registered
/// state values or an empty slot div, ensuring OOB swap targets exist in the DOM even when the list page
/// has not been rendered yet.
/// </remarks>
public sealed class HtmxStateContext
{
    private readonly List<IHasState> _states = [];

    /// <summary>Registers a state instance for inline rendering in the current request.</summary>
    public void Register(IHasState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _states.Add(state);
    }

    /// <summary>The states registered for the current request.</summary>
    public IReadOnlyList<IHasState> States => _states;

    /// <summary>
    /// Renders the state container for <typeparamref name="TState"/>.
    /// If a <typeparamref name="TState"/> instance was registered via <see cref="Register"/>, its values are serialized inline.
    /// Otherwise an empty slot div is rendered so that subsequent HTMX OOB swaps have a target in the DOM.
    /// </summary>
    /// <param name="stateId">The HTML element id for the state container — use the <c>StateId</c> const on <typeparamref name="TState"/>.</param>
    public Microsoft.AspNetCore.Html.IHtmlContent Serialize<TState>(string stateId) where TState : class, IHasState
    {
        var found = _states.OfType<TState>().FirstOrDefault();
        return found is not null ? found.Serialize() : StateSerializer.SerializeEmpty(stateId);
    }
}
