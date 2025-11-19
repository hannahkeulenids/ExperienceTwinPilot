using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using XRMultiplayer; // NetworkBaseInteractable

public class PrefabSelectionManager : NetworkBehaviour
{
    [Header("Alle interactable prefabs (bv. 9 stuks)")]
    [SerializeField] private List<NetworkBaseInteractable> allPrefabs = new();

    [Header("Holders/Spawners (in volgorde links→rechts)")]
    [SerializeField] private List<SpawnHelper> holders = new();

    [Header("Slider die de pagina kiest")]
    [SerializeField] private Slider groenSlider;

    // Server authoritative → sync naar iedereen
    public NetworkVariable<int> CurrentPage = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int HolderCount => Mathf.Max(holders?.Count ?? 0, 0);
    public int PageCount
    {
        get
        {
            int hc = HolderCount;
            if (hc <= 0) return 0;
            int n = allPrefabs?.Count ?? 0;
            return Mathf.CeilToInt(n / (float)hc);
        }
    }

    // ---------- Lifecycle ----------
    public override void OnNetworkSpawn()
    {
        CurrentPage.OnValueChanged += OnPageChanged;
        ApplyPageToHolders(CurrentPage.Value);
        InitSlider();
    }

    public override void OnNetworkDespawn()
    {
        CurrentPage.OnValueChanged -= OnPageChanged;
    }

    private void OnDisable()
    {
        if (groenSlider != null)
            groenSlider.onValueChanged.RemoveListener(OnSliderChanged);
    }

    // ---------- Slider ----------
    private void InitSlider()
    {
        if (!groenSlider) return;

        groenSlider.wholeNumbers = true;
        groenSlider.minValue = 0;
        groenSlider.maxValue = Mathf.Max(0, PageCount - 1);
        groenSlider.value = Mathf.Clamp(CurrentPage.Value, 0, groenSlider.maxValue);

        groenSlider.onValueChanged.RemoveListener(OnSliderChanged);
        groenSlider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnSliderChanged(float v)
    {
        RequestSetPage((int)v);
    }

    // ---------- Page set ----------
    private void OnPageChanged(int oldV, int newV)
    {
        ApplyPageToHolders(newV);

        if (groenSlider)
            groenSlider.SetValueWithoutNotify(Mathf.Clamp(newV, 0, Mathf.Max(0, PageCount - 1)));
    }

    /// UI roept deze variant direct aan met een int (optioneel)
    public void RequestSetPage(int page)
    {
        page = Mathf.Clamp(page, 0, Mathf.Max(0, PageCount - 1));

        if (IsServer) CurrentPage.Value = page;
        else SetPage_ServerRpc(page);
    }

    /// UI zonder parameter (bv. vanuit OnClick, als je die ooit wilt)
    public void RequestSetPageFromSlider()
    {
        if (!groenSlider) return;
        RequestSetPage(Mathf.RoundToInt(groenSlider.value));
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPage_ServerRpc(int page)
    {
        page = Mathf.Clamp(page, 0, Mathf.Max(0, PageCount - 1));
        CurrentPage.Value = page;
    }

    // ---------- Apply ----------
    private void ApplyPageToHolders(int page)
    {
        int hc = HolderCount;
        if (hc == 0) return;

        for (int h = 0; h < hc; h++)
        {
            int idx = page * hc + h;

            NetworkBaseInteractable prefab = (allPrefabs != null && idx >= 0 && idx < allPrefabs.Count)
                ? allPrefabs[idx]
                : null;

            var helper = holders[h];
            if (!helper) continue;

            helper.ApplyPrefabAndRefresh(prefab);

        }
    }
}
