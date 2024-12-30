using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProteinSelectionMenu : MonoBehaviour
{
    [Header("UI References")]
    public GameObject menuPanel;
    public TMP_Dropdown proteinDropdown;
    public Button loadButton;

    private RibbonController ribbonController;
    private FlyingCamera flyingCamera;

    private bool isMenuOpen = true;

    private List<ProteinFilePair> proteinFilePairs = new List<ProteinFilePair>();

    private struct ProteinFilePair
    {
        public string displayName;
        public string jsonPath;
        public string csvPath;
    }

    private void Awake()
    {
        ribbonController = Object.FindAnyObjectByType<RibbonController>();
        flyingCamera     = Object.FindAnyObjectByType<FlyingCamera>();

        if (ribbonController == null)
            Debug.LogWarning("RibbonController not found in scene.");
        if (flyingCamera == null)
            Debug.LogWarning("FlyingCamera not found in scene.");

        string streamingDir = Application.streamingAssetsPath;
        if (!Directory.Exists(streamingDir))
        {
            Debug.LogError($"No StreamingAssets folder found at: {streamingDir}");
            return;
        }

        string[] jsonFiles = Directory.GetFiles(streamingDir, "*.json");
        foreach (var jsonFilePath in jsonFiles)
        {
            string baseName = Path.GetFileNameWithoutExtension(jsonFilePath);
            string csvName  = baseName + "_data.csv";
            string csvFullPath = Path.Combine(streamingDir, "Aggregate", csvName);
            if (!File.Exists(csvFullPath))
            {
                Debug.LogWarning($"No matching CSV found for {baseName}. Expected: {csvFullPath}");
                continue;
            }
            proteinFilePairs.Add(new ProteinFilePair
            {
                displayName = baseName,
                jsonPath    = jsonFilePath,
                csvPath     = csvFullPath
            });
        }

        proteinDropdown.ClearOptions();
        List<string> options = new List<string>();
        foreach (var pair in proteinFilePairs)
        {
            options.Add(pair.displayName);
        }
        proteinDropdown.AddOptions(options);

        loadButton.onClick.AddListener(OnLoadButtonClicked);
    }

    private void Start()
    {
        menuPanel.SetActive(true);
        isMenuOpen = true;

        if (flyingCamera != null)
            flyingCamera.SetPaused(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    private void OnLoadButtonClicked()
    {
        int idx = proteinDropdown.value;
        if (idx < 0 || idx >= proteinFilePairs.Count)
        {
            Debug.LogError("No valid protein selected in dropdown.");
            return;
        }

        ProteinFilePair selected = proteinFilePairs[idx];
        if (ribbonController != null)
            ribbonController.LoadProteinData(selected.jsonPath, selected.csvPath);
        else
            Debug.LogWarning("RibbonController is null. Cannot load protein data.");

        menuPanel.SetActive(false);
        isMenuOpen = false;

        if (flyingCamera != null)
            flyingCamera.SetPaused(false);
    }

    private void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;
        menuPanel.SetActive(isMenuOpen);

        if (flyingCamera != null)
            flyingCamera.SetPaused(isMenuOpen);
    }
}