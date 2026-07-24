using TMPro;
using UnityEngine;
using ND.UI.WorldMap;

/// <summary>
/// Connects screen-space map labels to the presenter rendered by the RenderTexture camera.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldMapOverlayLabelBinding : MonoBehaviour
{
    [SerializeField] private WorldMapPresenter presenter;
    [SerializeField] private TMP_Text progressPercentLabel;
    [SerializeField] private TMP_Text riskLabel;

    private void Start()
    {
        // The render prefab already owns the presenter, so the UI must not create another map instance.
        BindLabels();
    }

    public void ConfigurePresenter(WorldMapPresenter mapPresenter)
    {
        // Separate prefab assets cannot serialize scene references, so the scene instance supplies the presenter.
        presenter = mapPresenter;
    }

    private void BindLabels()
    {
        if (presenter == null)
        {
            Debug.LogWarning("[WorldMap] Overlay labels could not be bound because the render presenter is unavailable.", this);
            return;
        }

        // Labels follow the sliding UI while their data comes from the RenderTexture map presenter.
        presenter.BindOverlayLabels(progressPercentLabel, riskLabel);
    }
}
