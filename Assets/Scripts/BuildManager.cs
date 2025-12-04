using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine.XR.Content.Interaction; // XRKnob

public class BuildManager : MonoBehaviour
{
    public static BuildManager Instance { get; private set; }

    // cache van originele placeables
    private readonly List<GameObject> _originalPlaceables = new List<GameObject>();

    [Header("Root Objects")]
    [SerializeField] private Transform tabletopRoot;          // TabletopBuildRoot
    [SerializeField] private string tabletopSceneName = "TabletopScene";
    [SerializeField] private string simulationSceneName = "SimulationScene";

    [Header("Options UI")]
    [SerializeField] public Slider optionSlider;
    [SerializeField] public GameObject[] tabletopOptions;
    [SerializeField] public GameObject[] simulationOptions;
    [SerializeField] private XRKnob optionKnob;               // XR-knop in VR

    [Header("Simulation Settings")]
    [SerializeField] public float simulationScale = 50f;

    [SerializeField] private int _lastActiveOptionIndex = 0;

    [SerializeField] private Camera snapshotCamera;

    private JsonManager jsonManager;

    // interne flag om recursion te voorkomen bij knob-updates
    private bool _updatingOptionKnobFromCode = false;

    // ------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
        if (tabletopRoot != null)
            DontDestroyOnLoad(tabletopRoot.gameObject);

        jsonManager = GetComponent<JsonManager>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        // Optioneel: listeners opruimen
        if (optionSlider != null)
            optionSlider.onValueChanged.RemoveListener(OnOptionSliderChanged);

        if (optionKnob != null)
            optionKnob.onValueChange.RemoveListener(OnOptionKnobChanged);
    }

    private void RebindTabletopRefs()
    {
        // probeer bindings-object in de huidige scene te vinden
        var bindings = FindObjectOfType<TabletopSceneBindings>();
        if (bindings == null)
        {
            Debug.LogWarning("[BuildManager] TabletopSceneBindings niet gevonden in deze scene.");
            return;
        }

        optionSlider = bindings.optionSlider;
        tabletopOptions = bindings.tabletopOptions;
        optionKnob = bindings.optionKnob;

        Debug.Log($"[BuildManager] Tabletop refs hersteld: " +
                  $"slider = {(optionSlider ? optionSlider.name : "null")}, " +
                  $"knob = {(optionKnob ? optionKnob.name : "null")}, " +
                  $"options = {(tabletopOptions != null ? tabletopOptions.Length : 0)}");
    }

    // ============================================================
    //  UI INIT
    // ============================================================

    private void InitOptionsUI()
    {
        int maxIndex = (tabletopOptions != null && tabletopOptions.Length > 0)
            ? tabletopOptions.Length - 1
            : 0;

        // Slider (desktop / debug)
        if (optionSlider != null)
        {
            optionSlider.wholeNumbers = true;
            optionSlider.minValue = 0;
            optionSlider.maxValue = maxIndex;

            optionSlider.onValueChanged.RemoveListener(OnOptionSliderChanged);
            optionSlider.onValueChanged.AddListener(OnOptionSliderChanged);
        }

        // XRKnob (VR)
        if (optionKnob != null)
        {
            optionKnob.onValueChange.RemoveListener(OnOptionKnobChanged);
            optionKnob.onValueChange.AddListener(OnOptionKnobChanged);

            SyncOptionKnobWithIndex(_lastActiveOptionIndex);
        }
    }

    private void OnOptionSliderChanged(float value)
    {
        int index = Mathf.RoundToInt(value);
        OnTabletopOptionChanged(index);
    }

    private void OnOptionKnobChanged(float knobValue)
    {
        if (_updatingOptionKnobFromCode)
            return;

        int maxIndex = (tabletopOptions != null && tabletopOptions.Length > 0)
            ? tabletopOptions.Length - 1
            : 0;

        if (maxIndex <= 0)
        {
            OnTabletopOptionChanged(0);
            return;
        }

        // 0..1 → 0..maxIndex
        float idxFloat = knobValue * maxIndex;
        int index = Mathf.RoundToInt(idxFloat);
        index = Mathf.Clamp(index, 0, maxIndex);

        OnTabletopOptionChanged(index);
    }

    private void SyncOptionKnobWithIndex(int index)
    {
        if (optionKnob == null || tabletopOptions == null || tabletopOptions.Length <= 1)
        {
            if (optionKnob != null)
            {
                _updatingOptionKnobFromCode = true;
                optionKnob.value = 0f;
                _updatingOptionKnobFromCode = false;
            }
            return;
        }

        int maxIndex = tabletopOptions.Length - 1;
        float normalized = Mathf.Clamp01(index / (float)maxIndex);

        _updatingOptionKnobFromCode = true;
        optionKnob.value = normalized;
        _updatingOptionKnobFromCode = false;
    }

    // ============================================================
    //  UI ENTRY POINTS
    // ============================================================

    public void StartSimulation(string buildName = "MyBuild")
    {
        Debug.Log("[BuildManager] StartSimulation UI call");

        if (tabletopRoot == null)
        {
            Debug.LogError("[BuildManager] tabletopRoot is NULL. Sleep TabletopBuildRoot in de inspector!");
            return;
        }

        // 0) cache ALLE originele placeables (los in de scene, NIET clones)
        CacheOriginalPlaceables();

        // Huidige actieve optie-index gebruiken (komt uit slider/knob via OnTabletopOptionChanged)
        int activeIndex = _lastActiveOptionIndex;

        // 1) JSON snapshot maken
        string json = jsonManager.CreateSnapshotJSON(tabletopRoot, buildName, activeIndex);
        _lastActiveOptionIndex = activeIndex;
        Debug.Log($"[BuildManager] StartSimulation → lastActiveOptionIndex = {_lastActiveOptionIndex}");

        string fileName = SaveSystem.MakeTimestampedName(buildName);
        SaveSystem.SaveJson(fileName, json);
        Debug.Log($"[BuildManager] Snapshot saved: {fileName}.json");

        // Snapshot van de snapshotCamera opslaan met dezelfde basename
        SaveSnapshotPNG(fileName);

        // 2) Clones maken in TABLETOP (onder tabletopRoot)
        CreateClones();

        // 3) Originelen verbergen
        SetOriginalsActive(false);

        // 4) Scene wisselen naar Simulation (via netcode als die aan staat)
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.SceneManager != null && nm.IsListening)
            nm.SceneManager.LoadScene(simulationSceneName, LoadSceneMode.Single);
        else
            SceneManager.LoadScene(simulationSceneName, LoadSceneMode.Single);
    }

    public void ReturnToTabletop()
    {
        Debug.Log("[BuildManager] ReturnToTabletop UI call");

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.SceneManager != null && nm.IsListening)
        {
            nm.SceneManager.LoadScene(tabletopSceneName, LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(tabletopSceneName, LoadSceneMode.Single);
        }
    }

    // ============================================================
    //  CLONES & ORIGINALS
    // ============================================================

    private void CreateClones()
    {
        var originals = GameObject.FindGameObjectsWithTag("Placeable");
        int count = 0;

        foreach (var orig in originals)
        {
            if (orig == null) continue;

            // safety: sla eventuele oude clones over
            if (orig.GetComponent<SimCloneMarker>() != null)
                continue;

            GameObject clone = Instantiate(orig);
            clone.name = orig.name + "_SimClone";

            // clones zijn puur visueel, dus eigen tag
            clone.tag = "SimClone";

            // onder tabletopRoot hangen (in tabletop scene)
            clone.transform.SetParent(tabletopRoot, worldPositionStays: true);

            // Netcode-component weghalen
            var no = clone.GetComponent<NetworkObject>();
            if (no) Destroy(no);

            // XR interactables uit
            foreach (var b in clone.GetComponentsInChildren<MonoBehaviour>())
            {
                if (b == null) continue;
                string t = b.GetType().Name;
                if (t.Contains("Grab") || t.Contains("Socket") || t.Contains("Interactable"))
                    b.enabled = false;
            }

            if (clone.GetComponent<SimCloneMarker>() == null)
                clone.AddComponent<SimCloneMarker>();

            count++;
        }

        Debug.Log($"[BuildManager] CreateClones: created {count} clone(s)");
    }

    private void SetOriginalsActive(bool active)
    {
        int toggled = 0;

        // loop van achter naar voren, dan kunnen we nulls uit de lijst halen
        for (int i = _originalPlaceables.Count - 1; i >= 0; i--)
        {
            var go = _originalPlaceables[i];

            if (go == null)
            {
                // object is misschien vernietigd, haal uit de lijst
                _originalPlaceables.RemoveAt(i);
                continue;
            }

            go.SetActive(active);
            toggled++;
        }

        Debug.Log($"[BuildManager] SetOriginalsActive({active}) → {toggled} gecachte originele Placeable(s) gezet.");
    }

    // ============================================================
    //  SCENE LOADED CALLBACK
    // ============================================================

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == simulationSceneName)
        {
            SimulationLoaded();
        }
        else if (scene.name == tabletopSceneName)
        {
            TabletopLoaded();
        }
    }

    // ------------------------------------------------------------

    private void SimulationLoaded()
    {
        Debug.Log("[BuildManager] SimulationLoaded");

        // 1) Zorg dat ALLE clones onder tabletopRoot hangen (voor de zekerheid)
        if (tabletopRoot != null)
        {
            var clones = GameObject.FindGameObjectsWithTag("SimClone");
            foreach (var c in clones)
            {
                if (c == null) continue;
                c.transform.SetParent(tabletopRoot, true);
            }
            Debug.Log($"[BuildManager] Reparented {clones.Length} SimClone(s) onder {tabletopRoot.name}");

            // 2) HELE tabletopRoot schalen → omgeving + opties + clones mee
            tabletopRoot.localScale = Vector3.one * simulationScale;
            Debug.Log("[BuildManager] tabletopRoot scaled x" + simulationScale);
        }

        // 3) Simulation-opties activeren
        int idx = _lastActiveOptionIndex;

        if (simulationOptions != null && simulationOptions.Length > 0)
        {
            idx = Mathf.Clamp(idx, 0, simulationOptions.Length - 1);

            for (int i = 0; i < simulationOptions.Length; i++)
                if (simulationOptions[i] != null)
                    simulationOptions[i].SetActive(i == idx);
        }
    }

    private void TabletopLoaded()
    {
        Debug.Log("[BuildManager] TabletopLoaded");

        RebindTabletopRefs();
        InitOptionsUI();

        if (tabletopOptions != null && tabletopOptions.Length > 0)
        {
            int idx = Mathf.Clamp(_lastActiveOptionIndex, 0, tabletopOptions.Length - 1);
            OnTabletopOptionChanged(idx);
            Debug.Log($"[BuildManager] TabletopLoaded → optie/index hersteld naar {idx}");
        }

        // 1) schaal resetten
        if (tabletopRoot != null)
            tabletopRoot.localScale = Vector3.one;

        // 2) clones opruimen
        var clones = GameObject.FindGameObjectsWithTag("SimClone");
        foreach (var c in clones)
            if (c != null) Destroy(c);

        // 3) originelen weer zichtbaar
        SetOriginalsActive(true);

        Debug.Log("[BuildManager] Tabletop restored (clones weg, originelen actief).");
    }

    // ============================================================
    //  UI: BACK TO TABLETOP BUTTON
    // ============================================================
    public void OnBackToTabletopButton()
    {
        Debug.Log("[BuildManager] Back to Tabletop button pressed");

        // Originelen weer zichtbaar maken
        SetOriginalsActive(true);

        // Schaal resetten
        if (tabletopRoot != null)
            tabletopRoot.localScale = Vector3.one;

        // Clones verwijderen
        var clones = GameObject.FindGameObjectsWithTag("SimClone");
        foreach (var c in clones)
            if (c != null) Destroy(c);

        Debug.Log("[BuildManager] Clones removed, originals restored");

        // Scene terugladen (via netcode indien actief)
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.SceneManager != null && nm.IsListening)
        {
            nm.SceneManager.LoadScene(tabletopSceneName, LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(tabletopSceneName, LoadSceneMode.Single);
        }
    }

    private void CacheOriginalPlaceables()
    {
        _originalPlaceables.Clear();

        var all = GameObject.FindGameObjectsWithTag("Placeable");
        int cached = 0;

        foreach (var go in all)
        {
            if (go == null) continue;

            // clones (voor de zekerheid) overslaan
            if (go.GetComponent<SimCloneMarker>() != null)
                continue;

            _originalPlaceables.Add(go);
            cached++;
        }

        Debug.Log($"[BuildManager] CacheOriginalPlaceables: cached {cached} originele Placeable(s).");
    }

    public void OnTabletopOptionChanged(int index)
    {
        if (tabletopOptions == null || tabletopOptions.Length == 0)
        {
            Debug.LogWarning("[BuildManager] Geen tabletopOptions ingesteld.");
            return;
        }

        int clamped = Mathf.Clamp(index, 0, tabletopOptions.Length - 1);
        _lastActiveOptionIndex = clamped;

        for (int i = 0; i < tabletopOptions.Length; i++)
        {
            if (tabletopOptions[i] != null)
                tabletopOptions[i].SetActive(i == clamped);
        }

        // Slider in sync houden
        if (optionSlider != null)
            optionSlider.SetValueWithoutNotify(clamped);

        // Knob in sync houden
        SyncOptionKnobWithIndex(clamped);

        Debug.Log($"[BuildManager] Tabletop optie {clamped} geactiveerd.");
    }

    private string SaveSnapshotPNG(string baseFileName)
    {
        if (snapshotCamera == null)
        {
            Debug.LogWarning("[BuildManager] No snapshotCamera assigned; skipping screenshot.");
            return null;
        }

        if (snapshotCamera.targetTexture == null)
        {
            Debug.LogError("[BuildManager] Snapshot camera has no RenderTexture assigned!");
            return null;
        }

        // Forceer één render naar de targettexture
        snapshotCamera.Render();

        // Lees pixels uit het toegewezen RenderTexture
        RenderTexture rt = snapshotCamera.targetTexture;
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        RenderTexture.active = null;

        // Schrijf PNG naar Saves-folder
        string folder = Path.Combine(UnityEngine.Application.persistentDataPath, "Saves");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, baseFileName + ".png");

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);

        Debug.Log($"[BuildManager] Snapshot saved → {path}");
        return path;
    }
}
