using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// AnimalInventoryPanel의 선택 결과를 받아 최종 동물 수량과
/// 개체별 DraftAnimalType 배열만 표시한다.
/// </summary>
public sealed class DraftAnimalSelectionSummaryBridge : MonoBehaviour
{
    [Header("선택 원본")]
    [SerializeField] private AnimalInventoryPanel animalPanel;
    [SerializeField] private DraftAnimalData[] animalCatalog = Array.Empty<DraftAnimalData>();

    [Header("표시 대상")]
    [SerializeField] private TMP_Text selectedCountText;
    [SerializeField] private TMP_Text selectedTypesText;

    private int selectedDraftAnimalCount;
    private DraftAnimalType[] selectedDraftAnimalTypes = Array.Empty<DraftAnimalType>();

    public int SelectedDraftAnimalCount => selectedDraftAnimalCount;
    public DraftAnimalType[] SelectedDraftAnimalTypes =>
        (DraftAnimalType[])selectedDraftAnimalTypes.Clone();

    private void OnEnable()
    {
        if (animalPanel != null)
            animalPanel.OnSelectionChanged += HandleSelectionChanged;

        RefreshText();
    }

    private void OnDisable()
    {
        if (animalPanel != null)
            animalPanel.OnSelectionChanged -= HandleSelectionChanged;
    }

    private void HandleSelectionChanged(
        IReadOnlyList<AnimalInventoryPanel.AnimalPick> picks,
        bool _)
    {
        var selectedTypes = new List<DraftAnimalType>();

        if (picks != null)
        {
            foreach (AnimalInventoryPanel.AnimalPick pick in picks)
            {
                if (pick.count <= 0)
                    continue;

                DraftAnimalData animal = FindAnimal(pick.animalId);
                if (animal == null)
                {
                    Debug.LogWarning(
                        $"DraftAnimalData not found for animalId '{pick.animalId}'.",
                        this);
                    continue;
                }

                for (int index = 0; index < pick.count; index++)
                    selectedTypes.Add(animal.AnimalType);
            }
        }

        selectedDraftAnimalTypes = selectedTypes.ToArray();
        selectedDraftAnimalCount = selectedDraftAnimalTypes.Length;
        RefreshText();
    }

    private DraftAnimalData FindAnimal(string animalId)
    {
        if (string.IsNullOrWhiteSpace(animalId) || animalCatalog == null)
            return null;

        foreach (DraftAnimalData animal in animalCatalog)
        {
            if (animal != null && animal.DraftAnimalId == animalId)
                return animal;
        }

        return null;
    }

    private void RefreshText()
    {
        if (selectedCountText != null)
            selectedCountText.text = $"선택 동물 수량: {selectedDraftAnimalCount}";

        if (selectedTypesText == null)
            return;

        if (selectedDraftAnimalTypes.Length == 0)
        {
            selectedTypesText.text = "선택 동물 타입: 없음";
            return;
        }

        string[] typeNames = Array.ConvertAll(
            selectedDraftAnimalTypes,
            type => type.ToString());
        selectedTypesText.text = $"선택 동물 타입: [{string.Join(", ", typeNames)}]";
    }
}
