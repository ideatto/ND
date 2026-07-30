using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
/// <summary>
/// BuildingDetailViewData를 화면에 표시하고 사용자 의도만 외부로 전달한다.
/// 요구조건 판정, 비용 차감, 건설 실행 및 저장은 Presenter 밖에서 처리한다.
/// </summary>
public sealed class BuildingDetailPopupPresenter : MonoBehaviour
{
    [Header("Popup")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button closeBackdropButton;

    [Header("Building_Information")]
    [SerializeField] private TMP_Text buildingNameText;
    [SerializeField] private TMP_Text currentLevelText;
    [SerializeField] private TMP_Text nextLevelText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Requirements")]
    [SerializeField] private RectTransform requirementContainer;
    [SerializeField] private BuildingRequirementSlot requirementSlotPrefab;

    [SerializeField, Min(0)] private int initialRequirementSlotPoolSize = 6;

    [Header("Preview_Model")]
    [SerializeField] private MeshFilter previewMeshFilter;
    [SerializeField] private MeshRenderer previewMeshRenderer;
    [SerializeField, Min(0.01f)] private float previewModelSize = 2f;

    [Header("Preview_Rendering")]
    [SerializeField] private Camera previewCamera;
    [SerializeField] private RenderTexture previewRenderTexture;
    [SerializeField] private RawImage previewImage;
    [SerializeField] private TMP_Text previewPlaceholderText;

    [Header("Action")]
    [SerializeField] private Button buildButton;
    [SerializeField] private TMP_Text buildButtonText;

    // 슬롯은 부족할 때만 추가 생성하고 이후 활성/비활성으로 재사용한다.
    private List<BuildingRequirementSlot> requirementSlots = new List<BuildingRequirementSlot>();
    private BuildingDetailViewData currentViewData;
    private int activeRequirementSlotCount;
    private Mesh previewGeneratedMesh;

    /// <summary>진행 가능한 건설 버튼을 누르면 선택한 buildId를 전달한다.</summary>
    public event Action<string> BuildRequested;

    /// <summary>상세 Popup 닫기 요청을 전달한다.</summary>
    public event Action CloseRequested;

    private void Awake()
    {
        if(previewPlaceholderText == null && previewImage != null)
        {
            Transform placeholderTransform =
                previewImage.transform.parent?.Find("PreviewPlaceholderText");

            previewPlaceholderText = placeholderTransform != null
                ? placeholderTransform.GetComponent<TMP_Text>()
                : null;
        }

        closeBackdropButton?.onClick.AddListener(HandleCloseClicked);
        buildButton?.onClick.AddListener(HandleBuildClicked);

        if(previewCamera != null)
        {
            previewCamera.enabled = false;
        }

        PrewarmRequirementSlotPool();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        closeBackdropButton?.onClick.RemoveListener(HandleCloseClicked);
        buildButton?.onClick.RemoveListener(HandleBuildClicked);
        ReleasePreviewMesh();
    }

    /// <summary>전달받은 화면 데이터를 렌더링하고 Popup을 표시한다.</summary>
    public void Show(BuildingDetailViewData viewData)
    {
        if(viewData == null)
        {
            Hide();
            return;
        }

        currentViewData = viewData;

        RenderBuildingInformation(viewData);
        RenderRequirements(viewData);
        RenderBuildButton(viewData);
        RenderPreview(viewData.previewPrefab);

        SetVisible(true);
    }

    /// <summary>Popup을 숨기고 사용 중인 요구조건 슬롯을 풀로 반환한다.</summary>
    public void Hide()
    {
        currentViewData = null;

        HideAllRequirementSlots();
        SetVisible(false);
    }

    private void RenderBuildingInformation(BuildingDetailViewData viewData)
    {
        if(buildingNameText != null)
        {
            buildingNameText.text = string.IsNullOrWhiteSpace(viewData.displayName) ? "-" : viewData.displayName;
        }

        if(currentLevelText != null)
        {
            currentLevelText.text = $"Lv.{viewData.currentLevel}";
        }

        if(nextLevelText != null)
        {
            nextLevelText.text = $"Lv.{viewData.targetLevel}";
        }

        if(descriptionText != null)
        {
            descriptionText.text = viewData.description ?? string.Empty;
        }
    }

    private void RenderRequirements(BuildingDetailViewData viewData)
    {
        activeRequirementSlotCount = 0;

        ItemRequirementViewData[] items = viewData.itemRequirements ?? Array.Empty<ItemRequirementViewData>();

        for(int i = 0; i < items.Length; i++)
        {
            ItemRequirementViewData item = items[i];

            if(item == null)
            {
                continue;
            }

            BindNextRequirementSlot(item.displayName, item.icon, item.ownedQuantity, item.requiredQuantity, item.isSatisfied);
        }

        HideUnusedRequirementSlots();
    }

    private void BindNextRequirementSlot(string displayName, Sprite icon, long ownedAmount, long requiredAmount, bool isSatisfied)
    {
        BuildingRequirementSlot slot = GetOrCreateRequirementSlot(activeRequirementSlotCount);

        if(slot == null)
        {
            return;
        }

        slot.Bind(displayName, icon, ownedAmount, requiredAmount, isSatisfied);

        activeRequirementSlotCount++;
    }

    // 일반적인 Popup 표시에서 Instantiate가 발생하지 않도록 초기 슬롯을 준비한다.
    private void PrewarmRequirementSlotPool()
    {
        if(requirementContainer == null || requirementSlotPrefab == null)
        {
            return;
        }

        for(int i = requirementSlots.Count; i < initialRequirementSlotPoolSize; i++)
        {
            CreateRequirementSlot();
        }
    }

    // 요청한 인덱스의 슬롯이 없을 때만 풀을 확장한다.
    private BuildingRequirementSlot GetOrCreateRequirementSlot(int index)
    {
        if(index < 0)
        {
            return null;
        }

        if(index < requirementSlots.Count)
        {
            return requirementSlots[index];
        }

        if(requirementContainer == null || requirementSlotPrefab == null)
        {
            return null;
        }

        while(requirementSlots.Count <= index)
        {
            BuildingRequirementSlot newSlot = CreateRequirementSlot();

            if(newSlot == null)
            {
                return null;
            }
        }

        return requirementSlots[index];
    }

    // 새 슬롯은 비활성 상태로 보관하고 Bind가 호출될 때 활성화한다.
    private BuildingRequirementSlot CreateRequirementSlot()
    {
        if(requirementContainer == null || requirementSlotPrefab == null)
        {
            return null;
        }

        BuildingRequirementSlot slot = Instantiate(requirementSlotPrefab, requirementContainer, false);

        slot.gameObject.SetActive(false);
        requirementSlots.Add(slot);

        return slot;
    }

    private void HideUnusedRequirementSlots()
    {
        for(int i = activeRequirementSlotCount; i < requirementSlots.Count; i++)
        {
            BuildingRequirementSlot slot = requirementSlots[i];

            if(slot != null)
            {
                slot.gameObject.SetActive(false);
            }
        }
    }

    private void HideAllRequirementSlots()
    {
        activeRequirementSlotCount = 0;

        for(int i = 0; i < requirementSlots.Count; i++)
        {
            BuildingRequirementSlot slot = requirementSlots[i];

            if(slot != null)
            {
                slot.gameObject.SetActive(false);
            }
        }
    }

    // 선택한 건물 외형을 적용하고 RenderTexture를 한 번만 갱신한다.
    private void RenderPreview(GameObject buildingPrefab)
    {
        if (!TryApplyPreviewAppearance(buildingPrefab))
        {
            SetPreviewVisible(false);
            return;
        }

        if(previewCamera == null || previewRenderTexture == null)
        {
            SetPreviewVisible(false);
            return;
        }

        previewCamera.targetTexture = previewRenderTexture;
        previewCamera.Render();

        if(previewImage != null)
        {
            previewImage.texture = previewRenderTexture;
        }

        SetPreviewVisible(true);
    }

    // Prefab 아래의 모든 Mesh와 Material을 하나의 Preview Mesh로 결합한다.
    private bool TryApplyPreviewAppearance(GameObject buildingPrefab)
    {
        if(buildingPrefab == null || previewMeshFilter == null || previewMeshRenderer == null)
        {
            return false;
        }

        MeshFilter[] sourceMeshFilters =
            buildingPrefab.GetComponentsInChildren<MeshFilter>(true);

        if(sourceMeshFilters == null || sourceMeshFilters.Length == 0)
        {
            return false;
        }

        if(sourceMeshFilters.Length == 1)
        {
            return TryApplySingleMeshAppearance(
                buildingPrefab.transform,
                sourceMeshFilters[0]);
        }

        var combineInstances = new List<CombineInstance>();
        var combinedMaterials = new List<Material>();
        Matrix4x4 rootWorldToLocal = buildingPrefab.transform.worldToLocalMatrix;

        for(int filterIndex = 0; filterIndex < sourceMeshFilters.Length; filterIndex++)
        {
            MeshFilter sourceMeshFilter = sourceMeshFilters[filterIndex];

            if(sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null)
            {
                continue;
            }

            MeshRenderer sourceMeshRenderer =
                sourceMeshFilter.GetComponent<MeshRenderer>();

            if(sourceMeshRenderer == null)
            {
                continue;
            }

            Mesh sourceMesh = sourceMeshFilter.sharedMesh;
            Material[] sourceMaterials = sourceMeshRenderer.sharedMaterials;
            int subMeshCount = sourceMesh.subMeshCount;

            for(int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                if(sourceMaterials == null || subMeshIndex >= sourceMaterials.Length)
                {
                    continue;
                }

                combineInstances.Add(new CombineInstance
                {
                    mesh = sourceMesh,
                    subMeshIndex = subMeshIndex,
                    transform =
                        rootWorldToLocal * sourceMeshFilter.transform.localToWorldMatrix
                });

                combinedMaterials.Add(sourceMaterials[subMeshIndex]);
            }
        }

        if(combineInstances.Count == 0)
        {
            return false;
        }

        ReleasePreviewMesh();

        previewGeneratedMesh = new Mesh
        {
            name = $"{buildingPrefab.name}_PreviewMesh",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };

        previewGeneratedMesh.CombineMeshes(
            combineInstances.ToArray(),
            false,
            true,
            false);

        previewGeneratedMesh.RecalculateBounds();

        previewMeshFilter.sharedMesh = previewGeneratedMesh;
        previewMeshRenderer.sharedMaterials = combinedMaterials.ToArray();

        FitPreviewMesh(previewGeneratedMesh);
        return true;
    }

    private bool TryApplySingleMeshAppearance(
        Transform prefabRoot,
        MeshFilter sourceMeshFilter)
    {
        if(prefabRoot == null
            || sourceMeshFilter == null
            || sourceMeshFilter.sharedMesh == null)
        {
            return false;
        }

        MeshRenderer sourceMeshRenderer =
            sourceMeshFilter.GetComponent<MeshRenderer>();

        if(sourceMeshRenderer == null)
        {
            return false;
        }

        ReleasePreviewMesh();

        previewMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;
        previewMeshRenderer.sharedMaterials =
            sourceMeshRenderer.sharedMaterials;

        FitPreviewMesh(
            sourceMeshFilter.sharedMesh,
            prefabRoot,
            sourceMeshFilter.transform);

        return true;
    }

    // 결합된 Mesh의 크기를 정규화하고 X/Z 중앙 및 Y 바닥을 Preview 원점에 맞춘다.
    private void FitPreviewMesh(Mesh mesh)
    {
        if(previewMeshFilter == null || mesh == null)
        {
            return;
        }

        Bounds bounds = mesh.bounds;

        float largestSize = Mathf.Max(
            bounds.size.x,
            bounds.size.y,
            bounds.size.z);

        float scale = largestSize > Mathf.Epsilon ? previewModelSize / largestSize : 1f;

        Transform previewTransform = previewMeshFilter.transform;

        previewTransform.localRotation = Quaternion.identity;
        previewTransform.localScale = Vector3.one * scale;
        previewTransform.localPosition = new Vector3(
            -bounds.center.x * scale,
            -bounds.min.y * scale,
            -bounds.center.z * scale);
    }

    private void FitPreviewMesh(
        Mesh mesh,
        Transform prefabRoot,
        Transform sourceTransform)
    {
        if(previewMeshFilter == null
            || mesh == null
            || prefabRoot == null
            || sourceTransform == null)
        {
            return;
        }

        Quaternion sourceRotation =
            Quaternion.Inverse(prefabRoot.rotation) * sourceTransform.rotation;

        Bounds rotatedBounds =
            CalculateRotatedBounds(mesh.bounds, sourceRotation);

        float largestSize = Mathf.Max(
            rotatedBounds.size.x,
            rotatedBounds.size.y,
            rotatedBounds.size.z);

        float scale =
            largestSize > Mathf.Epsilon ? previewModelSize / largestSize : 1f;

        Transform previewTransform = previewMeshFilter.transform;

        previewTransform.localRotation = sourceRotation;
        previewTransform.localScale = Vector3.one * scale;
        previewTransform.localPosition = new Vector3(
            -rotatedBounds.center.x * scale,
            -rotatedBounds.min.y * scale,
            -rotatedBounds.center.z * scale);
    }

    private static Bounds CalculateRotatedBounds(
        Bounds sourceBounds,
        Quaternion rotation)
    {
        Vector3 center = rotation * sourceBounds.center;
        Vector3 extents = sourceBounds.extents;
        Matrix4x4 rotationMatrix = Matrix4x4.Rotate(rotation);

        Vector3 rotatedExtents = new Vector3(
            Mathf.Abs(rotationMatrix.m00) * extents.x
                + Mathf.Abs(rotationMatrix.m01) * extents.y
                + Mathf.Abs(rotationMatrix.m02) * extents.z,
            Mathf.Abs(rotationMatrix.m10) * extents.x
                + Mathf.Abs(rotationMatrix.m11) * extents.y
                + Mathf.Abs(rotationMatrix.m12) * extents.z,
            Mathf.Abs(rotationMatrix.m20) * extents.x
                + Mathf.Abs(rotationMatrix.m21) * extents.y
                + Mathf.Abs(rotationMatrix.m22) * extents.z);

        return new Bounds(center, rotatedExtents * 2f);
    }

    private void ReleasePreviewMesh()
    {
        if(previewGeneratedMesh == null)
        {
            return;
        }

        if(previewMeshFilter != null
            && previewMeshFilter.sharedMesh == previewGeneratedMesh)
        {
            previewMeshFilter.sharedMesh = null;
        }

        Destroy(previewGeneratedMesh);
        previewGeneratedMesh = null;
    }

    private void SetPreviewVisible(bool visible)
    {
        if(previewMeshRenderer != null)
        {
            previewMeshRenderer.enabled = visible;
        }

        if(previewImage != null)
        {
            previewImage.enabled = visible;
        }

        if(previewPlaceholderText != null)
        {
            previewPlaceholderText.gameObject.SetActive(!visible);
        }
    }

    private void RenderBuildButton(BuildingDetailViewData viewData)
    {
        if(buildButton != null)
        {
            buildButton.interactable = viewData.canProceed;
        }

        if(buildButtonText != null)
        {
            buildButtonText.text = viewData.isConstruction ? "건설" : "증축";
        }
    }

    // Presenter는 실행하지 않고 buildId만 전달하며 Confirm 전환은 Binding이 담당한다.
    private void HandleBuildClicked()
    {
        if (currentViewData == null || !currentViewData.canProceed || string.IsNullOrWhiteSpace(currentViewData.buildId))
        {
            return;
        }

        BuildRequested?.Invoke(currentViewData.buildId);
    }

    private void HandleCloseClicked()
    {
        CloseRequested?.Invoke();
        Hide();
    }

    // 오브젝트를 유지한 채 CanvasGroup으로 표시와 입력을 함께 제어한다.
    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            gameObject.SetActive(visible);
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

}
