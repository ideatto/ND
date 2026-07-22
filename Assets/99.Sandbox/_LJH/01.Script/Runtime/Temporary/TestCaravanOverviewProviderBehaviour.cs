using UnityEngine;

/// <summary>
/// Exposes the temporary in-memory fixture as a scene-assignable Provider component.
/// Replace this component reference with a production Provider; Presenter code remains unchanged.
/// </summary>
[DisallowMultipleComponent]
public sealed class TestCaravanOverviewProviderBehaviour : MonoBehaviour, ICaravanOverviewViewDataProvider
{
    public CaravanOverviewViewData GetOverview()
    {
        // The wrapper contains no UI logic; it only makes the existing test Provider serializable in a scene.
        return new TestCaravanOverviewViewDataProvider().GetOverview();
    }
}
