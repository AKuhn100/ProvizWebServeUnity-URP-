using System.Collections.Generic;
using UnityEngine;

public class ResidueFragmentsManager : MonoBehaviour
{
    [Header("Material Settings")]
    [Tooltip("Custom shader for residue fragments")]
    public Shader fragmentShader;

    private List<GameObject> fragmentResidues = new List<GameObject>();
    private List<Vector3> scatterPositions = new List<Vector3>();

    private bool isScatterAnimating = false;
    private float scatterAnimTimer = 0f;
    private float scatterAnimDuration = 2f;
    private bool goingToScatter = true;

    private List<Vector3> scatterStartPositions = new List<Vector3>();
    private List<Vector3> scatterEndPositions = new List<Vector3>();

    public List<Vector3> ScatterPositions => scatterPositions;

    public void InitializeScatterPositions(
        Dictionary<int, float> stabilities,
        Dictionary<int, float> frustrations,
        Dictionary<int, float> bFactors,
        float scatterScale
    )
    {
        scatterPositions.Clear();

        int residueCount = GetComponent<RibbonDataLoader>().ResiduePositions.Count;
        for (int i = 0; i < residueCount; i++)
        {
            float sVal = stabilities.ContainsKey(i + 1) ? stabilities[i + 1] : 0.5f;
            float fVal = frustrations.ContainsKey(i + 1) ? frustrations[i + 1] : 0.5f;
            float bVal = bFactors.ContainsKey(i + 1) ? bFactors[i + 1] : 0.5f;

            Vector3 scatterPos = new Vector3(
                sVal * scatterScale,
                fVal * scatterScale,
                bVal * scatterScale
            );
            scatterPositions.Add(scatterPos);
        }
    }

    public void CreateResidueFragments(
        List<Vector3> startPositions,
        Gradient frustrationGradient,
        Dictionary<int, float> residueFrustrations,
        Dictionary<int, float> residueStabilities,
        float minEmissivity,
        float stabilityEmissionScale,
        float fragmentScale = 0.1f,
        float emissionMultiplier = 5f
    )
    {
        DestroyResidueFragments();

        if (fragmentShader == null)
        {
            Debug.LogError("Fragment shader is not assigned! Please assign NewShaderGraph in the inspector.");
            return;
        }

        RibbonMeshManager ribbonManager = GetComponent<RibbonMeshManager>();
        if (ribbonManager == null)
        {
            Debug.LogError("RibbonMeshManager not found!");
            return;
        }

        int count = startPositions.Count;
        for (int i = 0; i < count; i++)
        {
            GameObject frag = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            frag.name = $"ResidueFragment_{i + 1}";
            frag.transform.position = startPositions[i];
            frag.transform.localScale = Vector3.one * fragmentScale;

            float frustVal = residueFrustrations.ContainsKey(i + 1)
                ? residueFrustrations[i + 1]
                : 0.5f;
            float stabVal = residueStabilities.ContainsKey(i + 1)
                ? residueStabilities[i + 1]
                : 0.5f;

            float scaledStabVal = Mathf.Pow(stabVal, ribbonManager.stabilityExponent);
            scaledStabVal = Mathf.Clamp01(scaledStabVal);

            Color baseColor = frustrationGradient.Evaluate(frustVal);
            float alpha = minEmissivity + (scaledStabVal * stabilityEmissionScale);
            alpha *= emissionMultiplier;

            Mesh mesh = frag.GetComponent<MeshFilter>().mesh;
            Color[] colors = new Color[mesh.vertexCount];
            Color vertexColor = baseColor;
            vertexColor.a = alpha;
            
            for (int v = 0; v < colors.Length; v++)
            {
                colors[v] = vertexColor;
            }
            mesh.colors = colors;

            Material matInstance = new Material(fragmentShader);
            matInstance.name = $"ResidueMaterial_{i + 1}";
            
            Renderer rend = frag.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = matInstance;
            }

            fragmentResidues.Add(frag);
        }
    }

    public void DestroyResidueFragments()
    {
        foreach (var frag in fragmentResidues)
        {
            if (frag != null)
            {
                Renderer rend = frag.GetComponent<Renderer>();
                if (rend != null && rend.material != null)
                {
                    if (Application.isPlaying)
                        Destroy(rend.material);
                    else
                        DestroyImmediate(rend.material);
                }

                if (Application.isPlaying)
                    Destroy(frag);
                else
                    DestroyImmediate(frag);
            }
        }
        fragmentResidues.Clear();
    }

    public void StartScatterAnimation(
        List<Vector3> startPos,
        List<Vector3> endPos,
        float duration,
        bool toScatter
    )
    {
        scatterAnimTimer = 0f;
        scatterAnimDuration = duration;
        goingToScatter = toScatter;

        scatterStartPositions = new List<Vector3>(startPos);
        scatterEndPositions = new List<Vector3>(endPos);

        isScatterAnimating = true;

        if (toScatter)
        {
            RibbonMeshManager meshManager = GetComponent<RibbonMeshManager>();
            meshManager.SetAxesActive(true);
        }
    }

    public void HandleScatterAnimation(float deltaTime)
    {
        if (!isScatterAnimating) return;

        scatterAnimTimer += deltaTime / scatterAnimDuration;
        float t = Mathf.Clamp01(scatterAnimTimer);

        for (int i = 0; i < fragmentResidues.Count; i++)
        {
            if (fragmentResidues[i] == null) continue;
            Vector3 sPos = scatterStartPositions[i];
            Vector3 ePos = scatterEndPositions[i];
            fragmentResidues[i].transform.position = Vector3.Lerp(sPos, ePos, t);
        }

        if (t >= 1f)
        {
            isScatterAnimating = false;

            if (!goingToScatter)
            {
                DestroyResidueFragments();

                RibbonMeshManager meshManager = GetComponent<RibbonMeshManager>();
                meshManager.SetRibbonVisible(true);
                meshManager.SetAxesActive(false);
            }
        }
    }

    public bool IsScatterAnimating()
    {
        return isScatterAnimating;
    }

    private void Update()
    {
        HandleScatterAnimation(Time.deltaTime);
    }
}