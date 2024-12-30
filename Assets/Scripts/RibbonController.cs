using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.IO;

public class RibbonController : MonoBehaviour
{
    // --------------------------------------------------
    // Reference to other components
    // --------------------------------------------------
    private RibbonDataLoader dataLoader;
    private RibbonMeshManager meshManager;
    private ResidueFragmentsManager fragmentsManager;

    // --------------------------------------------------
    // Public Configuration Fields
    // --------------------------------------------------
    [Header("Data Files")]
    public string jsonFileName;
    public string bFactorFileName;

    [Header("Materials")]
    public Material ribbonMaterial;    // Ensure this uses a URP shader
    public Material fragmentMaterial;  // Ensure this uses a URP shader

    [Header("Rendering Settings")]
    public float ribbonWidth = 0.2f;
    public float ribbonThickness = 0.1f;
    public float unfoldDuration = 2f;
    public KeyCode unfoldKey = KeyCode.Q;
    public KeyCode foldKey = KeyCode.E;
    public int smoothFactor = 10;

    [Header("Unfolded Normalization")]
    [Tooltip("If true, the unfolded protein is scaled to 'targetUnfoldedLength'. If false, we derive its length from the actual chain length.")]
    public bool normalizeUnfoldedLength = true;

    [Tooltip("Desired length of the unfolded protein if 'normalizeUnfoldedLength' is true.")]
    public float targetUnfoldedLength = 20f;

    [Header("Unfolded Oscillation Multiplier")]
    [Tooltip("Multiplier for unfolded oscillation amplitude and frequency.")]
    public float unfoldedOscillationMultiplier = 2f;

    [Header("Structural Frustration Settings")]
    public Gradient frustrationGradient;

    [Header("Stability Emissivity Settings")]
    public float minEmissivity = 0.1f;
    public float stabilityEmissionScale = 2f;

    [Header("3D Scatter Settings")]
    public KeyCode scatterToggleKey = KeyCode.B;
    public float scatterAnimationDuration = 2f;
    public float scatterScale = 10f;

    [Header("Residue Fragment Settings")]
    public GameObject fragmentPrefab;
    public float fragmentScale = 0.1f;
    public float fragmentEmissionMultiplier = 5f;

    [Header("UI Elements")]
    public TextMeshProUGUI modeIndicator;

    // --------------------------------------------------
    // Internal States & Bookkeeping
    // --------------------------------------------------
    private bool isAnimating = false;
    private bool isSubstateAnimating = false;
    private bool oscillationEnabled = false;
    private bool inScatterMode = false;

    private float foldTime = 0f;
    private float substateProgress = 0f;
    private float oscillationTime = 0f;

    private List<Vector3> foldStartPositions;
    private List<Vector3> foldEndPositions;

    private List<Vector3> oldUnfoldedBase;
    private List<Vector3> newUnfoldedBase;

    private enum RibbonState { Folded, Unfolded }
    private RibbonState currentState = RibbonState.Folded;

    private enum UnfoldedSubState { Stationary, Oscillating }
    private UnfoldedSubState currentUnfoldedSubState = UnfoldedSubState.Stationary;

    private bool foldingToFolded = false;

    private void Awake()
    {
        dataLoader = GetComponent<RibbonDataLoader>();
        meshManager = GetComponent<RibbonMeshManager>();
        fragmentsManager = GetComponent<ResidueFragmentsManager>();
    }

    private void Start()
    {
        // Ensure that meshManager has URP-compatible materials
        if (ribbonMaterial == null)
        {
            Debug.LogError("Ribbon Material is not assigned in the Inspector!");
        }
        if (fragmentMaterial == null)
        {
            Debug.LogError("Fragment Material is not assigned in the Inspector!");
        }

        meshManager.InitializeRibbonMesh(ribbonMaterial, ribbonWidth, ribbonThickness);
        meshManager.CreateAxes(scatterScale);
        meshManager.SetAxesActive(false);
        meshManager.SetBrightnessMultiplier(1f); // Initialize brightness to default
    }

    private void Update()
    {
        HandleInput();
        HandleFoldUnfoldAnimation();
        HandleUnfoldedSubStateTransition();

        if (!inScatterMode && !fragmentsManager.IsScatterAnimating() && !isAnimating && !isSubstateAnimating)
        {
            if (currentState == RibbonState.Folded)
            {
                if (oscillationEnabled)
                    ApplyFoldedOscillation();
                else
                    meshManager.ResetMeshToBasePositions(smoothFactor);
            }
            else
            {
                if (currentUnfoldedSubState == UnfoldedSubState.Oscillating && oscillationEnabled)
                    ApplyUnfoldedOscillation();
                else
                    meshManager.ResetMeshToBasePositions(smoothFactor);
            }
        }
        fragmentsManager.HandleScatterAnimation(Time.deltaTime);
    }

    // --------------------------------------------------
    // Load Protein Data
    // --------------------------------------------------
    public void LoadProteinData(string jsonFilePath, string csvFilePath)
    {
        fragmentsManager.DestroyResidueFragments();
        meshManager.SetRibbonVisible(false);

        string jsonName = Path.GetFileName(jsonFilePath);
        string csvName = Path.GetFileName(csvFilePath);

        dataLoader.LoadResidueData(jsonName, csvName);

        meshManager.InitializeFoldedState(dataLoader.ResiduePositions, smoothFactor);
        meshManager.CalculateUnfoldedPositions(
            dataLoader.ResiduePositions,
            dataLoader.ResidueAmplitudes,
            normalizeUnfoldedLength,
            targetUnfoldedLength
        );
        fragmentsManager.InitializeScatterPositions(
            dataLoader.ResidueStabilities,
            dataLoader.ResidueFrustrations,
            dataLoader.ResidueAmplitudes,
            scatterScale
        );

        meshManager.SetRibbonVisible(true);

        currentState = RibbonState.Folded;
        inScatterMode = false;
        isAnimating = false;
        isSubstateAnimating = false;
        oscillationEnabled = false;
        foldingToFolded = false;

        // Reset brightness to default when loading new data
        meshManager.SetBrightnessMultiplier(1f);

        UpdateModeIndicatorText();
    }

    // --------------------------------------------------
    // INPUT & SCATTER
    // --------------------------------------------------
    private void HandleInput()
    {
        if (!inScatterMode)
        {
            if (Input.GetKeyDown(unfoldKey) && currentState == RibbonState.Folded && !isAnimating)
            {
                StartUnfoldAnimation();
            }
            else if (Input.GetKeyDown(foldKey) && currentState == RibbonState.Unfolded && !isAnimating && !isSubstateAnimating)
            {
                StartFoldAnimation();
            }
            if (Input.GetKeyDown(KeyCode.O))
            {
                oscillationEnabled = !oscillationEnabled;
                UpdateModeIndicatorText();

                if (currentState == RibbonState.Unfolded && !isAnimating)
                {
                    if (!isSubstateAnimating)
                    {
                        isSubstateAnimating = true;
                        substateProgress = 0f;
                        oldUnfoldedBase = meshManager.GetCurrentBasePositions();

                        currentUnfoldedSubState = oscillationEnabled
                            ? UnfoldedSubState.Oscillating
                            : UnfoldedSubState.Stationary;

                        newUnfoldedBase = (currentUnfoldedSubState == UnfoldedSubState.Oscillating)
                            ? meshManager.UnfoldedPositionsOscillating
                            : meshManager.UnfoldedPositionsStationary;
                    }
                }
            }
        }

        if (Input.GetKeyDown(scatterToggleKey))
        {
            ToggleScatterMode();
        }
    }

    private void ToggleScatterMode()
    {
        inScatterMode = !inScatterMode;

        if (inScatterMode)
        {
            meshManager.SetRibbonVisible(false);

            fragmentsManager.CreateResidueFragments(
                meshManager.GetCurrentBasePositions(),
                frustrationGradient,
                dataLoader.ResidueFrustrations,
                dataLoader.ResidueStabilities,
                minEmissivity,
                stabilityEmissionScale,
                fragmentScale,
                fragmentEmissionMultiplier
            );

            fragmentsManager.StartScatterAnimation(
                meshManager.GetCurrentBasePositions(),
                fragmentsManager.ScatterPositions,
                scatterAnimationDuration,
                true
            );

            meshManager.SetAxesActive(true);
        }
        else
        {
            var endPositions = (currentState == RibbonState.Folded)
                ? dataLoader.ResiduePositions
                : meshManager.GetCurrentBasePositions();

            fragmentsManager.StartScatterAnimation(
                fragmentsManager.ScatterPositions,
                endPositions,
                scatterAnimationDuration,
                false
            );

            meshManager.SetAxesActive(false);
        }

        UpdateModeIndicatorText();
    }

    // --------------------------------------------------
    // FOLD / UNFOLD
    // --------------------------------------------------
    private void StartUnfoldAnimation()
    {
        isAnimating = true;
        foldTime = 0f;
        foldingToFolded = false;

        foldStartPositions = meshManager.GetCurrentBasePositions();
        foldEndPositions = oscillationEnabled
            ? meshManager.UnfoldedPositionsOscillating
            : meshManager.UnfoldedPositionsStationary;
    }

    private void StartFoldAnimation()
    {
        isAnimating = true;
        foldTime = 0f;
        foldingToFolded = true;

        foldStartPositions = meshManager.GetCurrentBasePositions();
        foldEndPositions = new List<Vector3>(dataLoader.ResiduePositions);
    }

    private void HandleFoldUnfoldAnimation()
    {
        if (!isAnimating) return;

        foldTime += Time.deltaTime / unfoldDuration;
        foldTime = Mathf.Clamp01(foldTime);

        List<Vector3> interpolated = new List<Vector3>();
        for (int i = 0; i < foldStartPositions.Count; i++)
        {
            Vector3 lerpPos = Vector3.Lerp(foldStartPositions[i], foldEndPositions[i], foldTime);
            interpolated.Add(lerpPos);
        }

        meshManager.UpdateRibbonMeshInterpolated(interpolated, smoothFactor);

        if (foldTime >= 1f)
        {
            isAnimating = false;
            meshManager.SetCurrentBasePositions(foldEndPositions);

            if (foldingToFolded)
            {
                currentState = RibbonState.Folded;
                meshManager.SetBrightnessMultiplier(1f); // Reset brightness to default
            }
            else
            {
                currentState = RibbonState.Unfolded;
                meshManager.SetBrightnessMultiplier(meshManager.unfoldedBrightnessMultiplier); // Increase brightness

                currentUnfoldedSubState = oscillationEnabled
                    ? UnfoldedSubState.Oscillating
                    : UnfoldedSubState.Stationary;
            }
            UpdateModeIndicatorText();
        }
    }

    private void HandleUnfoldedSubStateTransition()
    {
        if (!isSubstateAnimating) return;

        substateProgress += Time.deltaTime / unfoldDuration;
        substateProgress = Mathf.Clamp01(substateProgress);

        List<Vector3> interpolated = new List<Vector3>();
        for (int i = 0; i < oldUnfoldedBase.Count; i++)
        {
            Vector3 lerpPos = Vector3.Lerp(oldUnfoldedBase[i], newUnfoldedBase[i], substateProgress);
            interpolated.Add(lerpPos);
        }

        meshManager.UpdateRibbonMeshInterpolated(interpolated, smoothFactor);

        if (substateProgress >= 1f)
        {
            isSubstateAnimating = false;
            meshManager.SetCurrentBasePositions(newUnfoldedBase);
        }
    }

    // --------------------------------------------------
    // OSCILLATION
    // --------------------------------------------------
    private void ApplyFoldedOscillation()
    {
        oscillationTime += Time.deltaTime;
        var newPositions = meshManager.CalculateFoldedOscillationPositions(
            meshManager.GetCurrentBasePositions(),
            dataLoader.ResidueAmplitudes,
            oscillationTime
        );
        meshManager.UpdateRibbonMeshInterpolated(newPositions, smoothFactor);
    }

    private void ApplyUnfoldedOscillation()
    {
        oscillationTime += Time.deltaTime;
        var newPositions = meshManager.CalculateUnfoldedOscillationPositions(
            meshManager.GetCurrentBasePositions(),
            dataLoader.ResidueAmplitudes,
            oscillationTime
        );
        meshManager.UpdateRibbonMeshInterpolated(newPositions, smoothFactor);
    }

    // --------------------------------------------------
    // UI
    // --------------------------------------------------
    private void UpdateModeIndicatorText()
    {
        if (!modeIndicator) return;

        string foldState = (currentState == RibbonState.Folded) ? "Folded" : "Unfolded";
        string oscState = oscillationEnabled ? "On" : "Off";
        string scatterState = inScatterMode 
            ? "Scatter Mode (Press B to Return)" 
            : "Ribbon Mode (Press B to Scatter)";

        modeIndicator.text = 
            $"State: {foldState}\nOscillation: {oscState}\n{scatterState}\n";
    }
}