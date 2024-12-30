using System.Collections.Generic;
using UnityEngine;
using TMPro;

[System.Serializable]
public class OscillationSettings
{
    [Tooltip("Minimum amplitude for oscillations.")]
    public float minAmplitude = 0.1f;

    [Tooltip("Maximum amplitude for oscillations.")]
    public float maxAmplitude = 1.0f;

    [Tooltip("Minimum frequency for oscillations.")]
    public float minFrequency = 1.0f;

    [Tooltip("Maximum frequency for oscillations.")]
    public float maxFrequency = 5.0f;
}

[System.Serializable]
public class BFactorAxisSettings
{
    [Tooltip("Number of fixed anchor points along the axis")]
    public int anchorPoints = 10;

    [Tooltip("Base amplitude of oscillation")]
    public float baseAmplitude = 0.015f;

    [Tooltip("How quickly the oscillation amplitude grows with distance (1 = linear, 2 = quadratic, etc)")]
    [Range(0.1f, 3.0f)]
    public float amplitudeGrowth = 1.0f;

    [Tooltip("Base frequency of oscillation")]
    public float baseFrequency = 1.0f;

    [Tooltip("How quickly the oscillation frequency increases with distance")]
    [Range(0.1f, 3.0f)]
    public float frequencyGrowth = 1.0f;

    [Tooltip("Maximum allowed oscillation offset to cap the oscillations.")]
    public float maxOscillationOffset = 0.05f; // Adjust as needed
}

public class RibbonMeshManager : MonoBehaviour
{
    // Mesh Components
    private Mesh ribbonMesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    // Position Lists
    private List<Vector3> basePositions = new List<Vector3>();
    private List<Vector3> smoothedCurrentPositions = new List<Vector3>();

    // Unfolded positions
    private List<Vector3> unfoldedPositionsStationary = new List<Vector3>();
    private List<Vector3> unfoldedPositionsOscillating = new List<Vector3>();

    // Axes Management
    private GameObject axesRoot;
    private GameObject bFactorAxis;
    private float animationTime;

    // Exposed Properties
    public List<Vector3> UnfoldedPositionsStationary => unfoldedPositionsStationary;
    public List<Vector3> UnfoldedPositionsOscillating => unfoldedPositionsOscillating;

    // Header: Axis Settings
    [Header("Axis Settings")]
    [Tooltip("Emission intensity for the axes. Higher values result in a brighter glow.")]
    public float axisEmissionIntensity = 10f;

    [Tooltip("Font size for the axis labels.")]
    public float labelFontSize = 2f;

    [Tooltip("Thickness of the axes. This value controls the scale of the axes perpendicular to their direction.")]
    public float axisThickness = 0.02f; // Adjustable via Inspector

    [Tooltip("Width of the axes.")]
    public float axisWidth = 0.05f; // Adjustable via Inspector

    [Tooltip("Offset distance for axis labels to prevent overlapping with axes.")]
    public float labelOffset = 0.3f; // Increased offset

    // Header: Emission Scaling
    [Header("Emission Scaling")]
    [Tooltip("Exponent for scaling brightness based on stability. Higher values increase contrast.")]
    [Range(1f, 5f)]
    public float stabilityExponent = 2f;

    // Header: Oscillation Settings
    [Header("Oscillation Settings")]
    [Tooltip("Settings for oscillations when the protein is folded.")]
    public OscillationSettings foldedOscillationSettings;

    [Tooltip("Settings for oscillations when the protein is unfolded.")]
    public OscillationSettings unfoldedOscillationSettings;

    [Tooltip("Multiplier for unfolded oscillation amplitude and frequency.")]
    public float unfoldedOscillationMultiplier = 2f;

    // Header: Brightness Settings
    [Header("Brightness Settings")]
    [Tooltip("Multiplier to increase brightness when the ribbon is unfolded.")]
    public float unfoldedBrightnessMultiplier = 2f;

    private float currentBrightnessMultiplier = 1f;

    // Header: B-Factor Axis Settings
    [Header("B-Factor Axis Settings")]
    public BFactorAxisSettings bFactorSettings = new BFactorAxisSettings();

    // Header: URP Shader References
    [Header("URP Shaders")]
    [Tooltip("URP-compatible shader for axes.")]
    public Shader axisShader;

    // Constants
    private const int AXIS_SEGMENTS = 100; // Increased for smoother axes

    // Store original vertices of the B-Factor axis to prevent cumulative oscillations
    private Vector3[] originalBFactorVertices;

    // Initialization Methods
    public void InitializeRibbonMesh(Material ribbonMaterial, float ribbonWidth, float ribbonThickness)
    {
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();

        ribbonMesh = new Mesh { name = "ProteinRibbonMesh" };
        meshFilter.mesh = ribbonMesh;

        if (ribbonMaterial != null)
            meshRenderer.material = ribbonMaterial;

        meshRenderer.enabled = true;
    }

    public void InitializeFoldedState(List<Vector3> foldedPositions, int smoothFactor)
    {
        basePositions = new List<Vector3>(foldedPositions);
        smoothedCurrentPositions = SmoothCurve(basePositions, smoothFactor);
        UpdateRibbonMesh(smoothedCurrentPositions);
    }

    private float ComputeChainLength(List<Vector3> residuePositions)
    {
        float totalDist = 0f;
        for (int i = 0; i < residuePositions.Count - 1; i++)
        {
            totalDist += Vector3.Distance(residuePositions[i], residuePositions[i + 1]);
        }
        return totalDist;
    }

    public void CalculateUnfoldedPositions(
        List<Vector3> originalResiduePositions,
        Dictionary<int, float> residueAmplitudes,
        bool normalizeLength,
        float targetLength
    )
    {
        unfoldedPositionsStationary.Clear();
        unfoldedPositionsOscillating.Clear();

        int count = originalResiduePositions.Count;
        if (count < 2)
        {
            unfoldedPositionsStationary.AddRange(originalResiduePositions);
            unfoldedPositionsOscillating.AddRange(originalResiduePositions);
            return;
        }

        float chainLength = normalizeLength ? targetLength : ComputeChainLength(originalResiduePositions);
        float spacing = chainLength / (count - 1);
        float startX = 0f;

        for (int i = 0; i < count; i++)
        {
            float bVal = residueAmplitudes.ContainsKey(i + 1)
                ? residueAmplitudes[i + 1]
                : 0.5f;

            float yVal = bVal * 10f;
            float xVal = startX + i * spacing;
            float zVal = 0f;

            unfoldedPositionsStationary.Add(new Vector3(xVal, yVal, zVal));
            unfoldedPositionsOscillating.Add(new Vector3(xVal, 0f, zVal));
        }
    }

    private void CreateRibbonAxis(string axisName, List<Vector3> positions, float ribbonWidth, float ribbonThickness, Transform parent)
    {
        GameObject axisObj = new GameObject(axisName);
        axisObj.transform.SetParent(parent);
        axisObj.transform.localPosition = Vector3.zero;

        MeshFilter meshFilter = axisObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = axisObj.AddComponent<MeshRenderer>();
        Mesh axisMesh = new Mesh { name = $"{axisName}_Mesh" };

        float halfWidth = ribbonWidth * 0.5f;
        float halfThick = ribbonThickness * 0.5f;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        List<Color> colors = new List<Color>();

        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 forward = (i < positions.Count - 1)
                ? (positions[i + 1] - positions[i]).normalized
                : (positions[i] - positions[i - 1]).normalized;

            Vector3 defaultUp = Vector3.up;
            Vector3 right = Vector3.Cross(forward, defaultUp).normalized;
            if (right == Vector3.zero)
            {
                defaultUp = Vector3.forward;
                right = Vector3.Cross(forward, defaultUp).normalized;
            }
            Vector3 upLocal = Vector3.Cross(right, forward).normalized;

            Vector3 center = positions[i];
            Vector3 lt = center - right * halfWidth + upLocal * halfThick;
            Vector3 lb = center - right * halfWidth - upLocal * halfThick;
            Vector3 rb = center + right * halfWidth - upLocal * halfThick;
            Vector3 rt = center + right * halfWidth + upLocal * halfThick;

            vertices.AddRange(new[] { lt, lb, rb, rt });

            float tCoord = (float)i / (positions.Count - 1);
            for (int j = 0; j < 4; j++)
                uvs.Add(new Vector2(j < 2 ? 0 : 1, tCoord));

            // Set color based on axis type
            Color color;
            if (axisName.Contains("X-Axis"))
            {
                color = Color.red;
                color *= Mathf.Pow(tCoord, stabilityExponent); // Emissivity gradient
                color.a = axisEmissionIntensity;
            }
            else if (axisName.Contains("Y-Axis"))
            {
                // Assuming you have a gradient for frustration in RibbonController
                RibbonController rc = GetComponent<RibbonController>();
                if (rc != null && rc.frustrationGradient != null)
                {
                    color = rc.frustrationGradient.Evaluate(tCoord);
                }
                else
                {
                    color = Color.green; // Default color if gradient not set
                }
                color.a = axisEmissionIntensity;
            }
            else // Z-Axis (B-Factor)
            {
                color = new Color(0.3f, 0.3f, 1f); // Lighter blue
                color.a = axisEmissionIntensity * 0.8f;
            }

            for (int j = 0; j < 4; j++)
                colors.Add(color);
        }

        // Build triangles
        for (int i = 0; i < positions.Count - 1; i++)
        {
            int baseA = i * 4;
            int baseB = (i + 1) * 4;

            triangles.AddRange(new[] {
                // Top face
                baseA, baseA + 3, baseB + 3,
                baseA, baseB + 3, baseB,
                // Bottom face
                baseA + 1, baseB + 1, baseB + 2,
                baseA + 1, baseB + 2, baseA + 2,
                // Left face
                baseA, baseB, baseB + 1,
                baseA, baseB + 1, baseA + 1,
                // Right face
                baseA + 2, baseB + 2, baseB + 3,
                baseA + 2, baseB + 3, baseA + 3
            });
        }

        axisMesh.vertices = vertices.ToArray();
        axisMesh.triangles = triangles.ToArray();
        axisMesh.uv = uvs.ToArray();
        axisMesh.colors = colors.ToArray();
        axisMesh.RecalculateNormals();

        meshFilter.mesh = axisMesh;
        Material mat = new Material(axisShader);
        mat.EnableKeyword("_EMISSION");

        // Set emission color based on the vertex colors
        if (axisName.Contains("X-Axis"))
        {
            mat.SetColor("_BaseColor", Color.red);
            mat.SetFloat("_EmissionIntensity", axisEmissionIntensity);
        }
        else if (axisName.Contains("Y-Axis"))
        {
            RibbonController rc = GetComponent<RibbonController>();
            if (rc != null && rc.frustrationGradient != null)
            {
                // Use frustrationGradient's color at midpoint for emission
                mat.SetColor("_BaseColor", rc.frustrationGradient.Evaluate(0.5f));
            }
            else
            {
                mat.SetColor("_BaseColor", Color.green);
            }
            mat.SetFloat("_EmissionIntensity", axisEmissionIntensity);
        }
        else // Z-Axis (B-Factor)
        {
            mat.SetColor("_BaseColor", new Color(0.3f, 0.3f, 1f)); // Lighter blue
            mat.SetFloat("_EmissionIntensity", axisEmissionIntensity * 0.8f);
        }

        meshRenderer.material = mat;

        // If this is the B-Factor axis, store its original vertices
        if (axisName.Contains("Z-Axis"))
        {
            originalBFactorVertices = axisMesh.vertices.Clone() as Vector3[];
        }
    }

    public void CreateAxes(float scatterScale)
    {
        axesRoot = new GameObject("ScatterAxesRoot");
        axesRoot.transform.position = Vector3.zero;

        RibbonController rc = GetComponent<RibbonController>();
        if (rc == null)
        {
            Debug.LogError("RibbonController not found!");
            return;
        }

        // Generate positions for each axis
        int axisPoints = 50; // Number of points along each axis

        // X-Axis positions (Stability/Emissivity)
        List<Vector3> xAxisPositions = new List<Vector3>();
        for (int i = 0; i < axisPoints; i++)
        {
            float t = i / (float)(axisPoints - 1);
            xAxisPositions.Add(new Vector3(t * scatterScale, 0, 0));
        }

        // Y-Axis positions (Frustration/Color)
        List<Vector3> yAxisPositions = new List<Vector3>();
        for (int i = 0; i < axisPoints; i++)
        {
            float t = i / (float)(axisPoints - 1);
            yAxisPositions.Add(new Vector3(0, t * scatterScale, 0));
        }

        // Z-Axis positions (B-Factor/Oscillation)
        List<Vector3> zAxisPositions = new List<Vector3>();
        for (int i = 0; i < axisPoints; i++)
        {
            float t = i / (float)(axisPoints - 1);
            zAxisPositions.Add(new Vector3(0, 0, t * scatterScale));
        }

        // Create axis GameObjects with proper parenting
        CreateRibbonAxis("X-Axis (Stability)", xAxisPositions, axisWidth, axisThickness, axesRoot.transform);
        CreateRibbonAxis("Y-Axis (Frustration)", yAxisPositions, axisWidth, axisThickness, axesRoot.transform);
        CreateRibbonAxis("Z-Axis (B-Factor)", zAxisPositions, axisWidth, axisThickness, axesRoot.transform);

        // Store B-Factor axis reference for animation
        Transform zAxisTransform = axesRoot.transform.Find("Z-Axis (B-Factor)");
        if (zAxisTransform != null)
        {
            bFactorAxis = zAxisTransform.gameObject;
        }
        else
        {
            Debug.LogError("Z-Axis (B-Factor) not found under ScatterAxesRoot!");
        }

        // Create labels with increased offset
        CreateAxisLabel("X-Axis-Label", "Evolutionary Frustration",
            new Vector3(scatterScale + labelOffset, 0f, 0f),
            axesRoot.transform);

        CreateAxisLabel("Y-Axis-Label", "Experimental Frustration",
            new Vector3(0f, scatterScale + labelOffset, 0f),
            axesRoot.transform);

        CreateAxisLabel("Z-Axis-Label", "B-Factor",
            new Vector3(0f, 0f, scatterScale + labelOffset),
            axesRoot.transform);
    }

    private void AnimateBFactorAxis()
    {
        if (bFactorAxis == null) return;

        MeshFilter meshFilter = bFactorAxis.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.mesh == null) return;

        Mesh mesh = meshFilter.mesh;
        Vector3[] vertices = mesh.vertices;

        if (originalBFactorVertices == null || originalBFactorVertices.Length != vertices.Length)
        {
            Debug.LogWarning("Original B-Factor vertices not set or mismatched. Skipping animation.");
            return;
        }

        // Calculate segment length based on number of anchor points
        int crossSections = originalBFactorVertices.Length / 4; // Assuming 4 vertices per cross-section
        float segmentLength = (float)crossSections / bFactorSettings.anchorPoints;

        for (int i = 0; i < crossSections; i++)
        {
            float normalizedPos = (float)i / (crossSections - 1);

            // Calculate position within current segment
            float segmentIndex = i / segmentLength;
            float localT = segmentIndex - Mathf.Floor(segmentIndex);

            // Create smooth damping that goes to zero at segment boundaries
            float segmentDamping = Mathf.Sin(localT * Mathf.PI);

            // Scale amplitude and frequency based on distance from origin
            float scaledAmplitude = bFactorSettings.baseAmplitude *
                Mathf.Pow(normalizedPos, bFactorSettings.amplitudeGrowth);

            float scaledFrequency = bFactorSettings.baseFrequency *
                (1f + normalizedPos * bFactorSettings.frequencyGrowth);

            // Calculate noise with increasing frequency
            float noise = Mathf.PerlinNoise(i * 0.2f, animationTime * scaledFrequency + 100f) * 2f - 1f;

            // Apply vertical offset only, scaled by segment damping
            Vector3 offset = new Vector3(0, noise * scaledAmplitude * segmentDamping, 0);

            // Clamp the offset to prevent excessive oscillation
            offset = Vector3.ClampMagnitude(offset, bFactorSettings.maxOscillationOffset);

            int baseIndex = i * 4;
            for (int j = 0; j < 4; j++)
            {
                // Apply the offset relative to the original vertices
                vertices[baseIndex + j] = originalBFactorVertices[baseIndex + j] + offset;
            }
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
    }

    private void CreateAxisLabel(string labelName, string text, Vector3 offset, Transform parent)
    {
        GameObject labelObj = new GameObject(labelName);
        labelObj.transform.SetParent(parent);
        labelObj.transform.localPosition = offset;

        TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = labelFontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.transform.rotation = Quaternion.identity;

        // Assuming you have a Billboard script to make the label face the camera
        // If not, you can remove this line or implement a simple Billboard functionality
        Billboard billboard = labelObj.AddComponent<Billboard>();

        labelObj.transform.localScale = Vector3.one;
    }

    private void Update()
    {
        if (axesRoot != null && axesRoot.activeSelf && bFactorAxis != null)
        {
            animationTime += Time.deltaTime;
            AnimateBFactorAxis();
        }
    }

    // Mesh Update Methods
    public void UpdateRibbonMeshInterpolated(List<Vector3> interpolatedPositions, int smoothFactor)
    {
        smoothedCurrentPositions = SmoothCurve(interpolatedPositions, smoothFactor);
        UpdateRibbonMesh(smoothedCurrentPositions);
    }

    public void ResetMeshToBasePositions(int smoothFactor)
    {
        smoothedCurrentPositions = SmoothCurve(basePositions, smoothFactor);
        UpdateRibbonMesh(smoothedCurrentPositions);
    }

    public void SetCurrentBasePositions(List<Vector3> newBase)
    {
        basePositions = new List<Vector3>(newBase);
    }

    public List<Vector3> GetCurrentBasePositions()
    {
        return basePositions;
    }

    // Mesh Building Methods
    private void UpdateRibbonMesh(List<Vector3> positions)
    {
        if (positions.Count < 2) return;

        ribbonMesh.Clear();

        RibbonController rc = GetComponent<RibbonController>();
        if (rc == null)
        {
            Debug.LogError("RibbonController component not found!");
            return;
        }

        float ribbonWidth = axisWidth;
        float ribbonThickness = axisThickness;

        RibbonDataLoader dataLoader = GetComponent<RibbonDataLoader>();
        if (dataLoader == null)
        {
            Debug.LogError("RibbonDataLoader component not found!");
            return;
        }

        int nResidues = dataLoader.ResiduePositions.Count;
        Dictionary<int, float> residueFrustrations = dataLoader.ResidueFrustrations;
        Dictionary<int, float> residueStabilities = dataLoader.ResidueStabilities;
        Gradient frustrationGradient = rc.frustrationGradient;

        float halfWidth = ribbonWidth * 0.5f;
        float halfThick = ribbonThickness * 0.5f;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        List<Color> colors = new List<Color>();

        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 forward = (i < positions.Count - 1)
                ? (positions[i + 1] - positions[i]).normalized
                : (positions[i] - positions[i - 1]).normalized;

            Vector3 defaultUp = Vector3.up;
            Vector3 right = Vector3.Cross(forward, defaultUp).normalized;
            if (right == Vector3.zero)
            {
                defaultUp = Vector3.forward;
                right = Vector3.Cross(forward, defaultUp).normalized;
            }
            Vector3 upLocal = Vector3.Cross(right, forward).normalized;

            Vector3 center = positions[i];
            Vector3 lt = center - right * halfWidth + upLocal * halfThick;
            Vector3 lb = center - right * halfWidth - upLocal * halfThick;
            Vector3 rb = center + right * halfWidth - upLocal * halfThick;
            Vector3 rt = center + right * halfWidth + upLocal * halfThick;

            vertices.AddRange(new[] { lt, lb, rb, rt });

            float vCoord = (float)i / (positions.Count - 1);
            for (int j = 0; j < 4; j++)
                uvs.Add(new Vector2(j < 2 ? 0 : 1, vCoord));

            float fraction = (float)i / (positions.Count - 1);
            float mapped = fraction * (nResidues - 1);
            int residueIdx = Mathf.Clamp(Mathf.RoundToInt(mapped), 0, nResidues - 1);

            float frustVal = residueFrustrations.ContainsKey(residueIdx + 1)
                ? residueFrustrations[residueIdx + 1]
                : 0.5f;
            Color c = frustrationGradient.Evaluate(frustVal);

            float stabVal = residueStabilities.ContainsKey(residueIdx + 1)
                ? residueStabilities[residueIdx + 1]
                : 0.5f;

            float scaledStabVal = Mathf.Pow(stabVal, stabilityExponent);
            scaledStabVal = Mathf.Clamp01(scaledStabVal);

            c.a = (rc.minEmissivity + (scaledStabVal * rc.stabilityEmissionScale)) * currentBrightnessMultiplier;

            for (int j = 0; j < 4; j++)
                colors.Add(c);
        }

        // Build triangles
        for (int i = 0; i < positions.Count - 1; i++)
        {
            int baseA = i * 4;
            int baseB = (i + 1) * 4;

            // Top face
            triangles.AddRange(new[] { baseA, baseA + 3, baseB + 3, baseA, baseB + 3, baseB });

            // Bottom face
            triangles.AddRange(new[] { baseA + 1, baseB + 1, baseB + 2, baseA + 1, baseB + 2, baseA + 2 });

            // Left face
            triangles.AddRange(new[] { baseA, baseB, baseB + 1, baseA, baseB + 1, baseA + 1 });

            // Right face
            triangles.AddRange(new[] { baseA + 2, baseB + 2, baseB + 3, baseA + 2, baseB + 3, baseA + 3 });
        }

        ribbonMesh.vertices = vertices.ToArray();
        ribbonMesh.triangles = triangles.ToArray();
        ribbonMesh.uv = uvs.ToArray();
        ribbonMesh.colors = colors.ToArray();
        ribbonMesh.RecalculateNormals();
    }

    // Oscillation Methods
    private List<Vector3> CalculateOscillationPositions(
        List<Vector3> basePos,
        Dictionary<int, float> residueAmplitudes,
        OscillationSettings settings,
        float oscillationMultiplier,
        float oscTime,
        bool isFolded
    )
    {
        List<Vector3> result = new List<Vector3>();
        for (int i = 0; i < basePos.Count; i++)
        {
            float bVal = residueAmplitudes.ContainsKey(i + 1) ? residueAmplitudes[i + 1] : 0.5f;
            float amplitude = Mathf.Lerp(settings.minAmplitude, settings.maxAmplitude, bVal) * oscillationMultiplier;
            float frequency = Mathf.Lerp(settings.minFrequency, settings.maxFrequency, bVal) * oscillationMultiplier;

            Vector3 noise = Vector3.zero;

            if (isFolded)
            {
                float noiseX = Mathf.PerlinNoise(i * 0.1f, oscTime * frequency) * amplitude - amplitude / 2f;
                float noiseY = Mathf.PerlinNoise(i * 0.2f, oscTime * frequency + 100f) * amplitude - amplitude / 2f;
                float noiseZ = Mathf.PerlinNoise(i * 0.3f, oscTime * frequency + 200f) * amplitude - amplitude / 2f;
                noise = new Vector3(noiseX, noiseY, noiseZ);
            }
            else
            {
                float noiseY = Mathf.PerlinNoise(i * 0.2f, oscTime * frequency + 100f) * amplitude - amplitude / 2f;
                noise = new Vector3(0f, noiseY, 0f);
            }

            result.Add(basePos[i] + noise);
        }
        return result;
    }

    public List<Vector3> CalculateFoldedOscillationPositions(
        List<Vector3> basePos,
        Dictionary<int, float> residueAmplitudes,
        float oscTime
    )
    {
        return CalculateOscillationPositions(
            basePos,
            residueAmplitudes,
            foldedOscillationSettings,
            1f,
            oscTime,
            true
        );
    }

    public List<Vector3> CalculateUnfoldedOscillationPositions(
        List<Vector3> basePos,
        Dictionary<int, float> residueAmplitudes,
        float oscTime
    )
    {
        return CalculateOscillationPositions(
            basePos,
            residueAmplitudes,
            unfoldedOscillationSettings,
            unfoldedOscillationMultiplier,
            oscTime,
            false
        );
    }

    // Smoothing Methods (Catmull-Rom)
    private List<Vector3> SmoothCurve(List<Vector3> positions, int factor)
    {
        if (positions.Count < 3)
            return new List<Vector3>(positions);

        List<Vector3> smoothed = new List<Vector3>();
        for (int i = 0; i < positions.Count - 1; i++)
        {
            Vector3 p0 = positions[Mathf.Max(0, i - 1)];
            Vector3 p1 = positions[i];
            Vector3 p2 = positions[Mathf.Min(positions.Count - 1, i + 1)];
            Vector3 p3 = positions[Mathf.Min(positions.Count - 1, i + 2)];

            for (int j = 0; j < factor; j++)
            {
                float t = j / (float)factor;
                smoothed.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }
        smoothed.Add(positions[positions.Count - 1]);
        return smoothed;
    }

    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * (t * t) +
            (-p0 + 3f * p1 - 3f * p2 + p3) * (t * t * t)
        );
    }

    // Visibility and Utility Methods
    public void SetRibbonVisible(bool visible)
    {
        if (meshRenderer != null)
            meshRenderer.enabled = visible;
    }

    public void SetBrightnessMultiplier(float multiplier)
    {
        currentBrightnessMultiplier = Mathf.Clamp(multiplier, 0f, 10f);
        UpdateRibbonMesh(smoothedCurrentPositions);
    }

    public void SetAxesActive(bool active)
    {
        if (axesRoot != null)
            axesRoot.SetActive(active);
    }
}