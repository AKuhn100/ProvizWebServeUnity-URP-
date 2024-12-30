using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RibbonDataLoader : MonoBehaviour
{
    private List<Vector3> residuePositions = new List<Vector3>();
    private Dictionary<int, float> residueAmplitudes   = new Dictionary<int, float>(); 
    private Dictionary<int, float> residueFrustrations = new Dictionary<int, float>();
    private Dictionary<int, float> residueStabilities  = new Dictionary<int, float>();

    public List<Vector3> ResiduePositions                => residuePositions;
    public Dictionary<int, float> ResidueAmplitudes      => residueAmplitudes;
    public Dictionary<int, float> ResidueFrustrations    => residueFrustrations;
    public Dictionary<int, float> ResidueStabilities     => residueStabilities;

    public void LoadResidueData(string jsonFileName, string csvFileName)
    {
        residuePositions.Clear();
        residueAmplitudes.Clear();
        residueFrustrations.Clear();
        residueStabilities.Clear();

        residuePositions = LoadResiduePositions(jsonFileName);
        if (residuePositions.Count == 0)
        {
            Debug.LogError("No residue positions found. Cannot generate ribbon.");
            return;
        }

        LoadCsvData(csvFileName);
        ReindexResidues();
        CenterProtein();
    }

    private List<Vector3> LoadResiduePositions(string fileName)
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, fileName);
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"Residue JSON not found at: {fullPath}");
            return new List<Vector3>();
        }

        string jsonContent = File.ReadAllText(fullPath);
        ResidueList residueList = JsonUtility.FromJson<ResidueList>(jsonContent);

        if (residueList == null || residueList.residues == null)
        {
            Debug.LogError("Failed to parse JSON residue data.");
            return new List<Vector3>();
        }

        List<Vector3> positions = new List<Vector3>();
        foreach (var r in residueList.residues)
        {
            positions.Add(new Vector3(r.position.x, r.position.y, r.position.z));
        }
        return positions;
    }

    private void LoadCsvData(string fileName)
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, "Aggregate", fileName);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"CSV not found at: {fullPath}. Using defaults.");
            return;
        }

        string[] lines = File.ReadAllLines(fullPath);
        if (lines.Length < 2) return;

        string header = lines[0];
        string[] cols = header.Split(',');

        int residueIdx      = -1;
        int bFactorIdx      = -1;
        int frustrationIdx  = -1;
        int stabilityIdx    = -1;

        for (int i = 0; i < cols.Length; i++)
        {
            string c = cols[i].Trim().ToLower();
            if (c.Contains("residue"))     residueIdx = i;
            if (c.Contains("b_factor"))    bFactorIdx = i;
            if (c.Contains("frustration")) frustrationIdx = i;
            if (c.Contains("stability"))   stabilityIdx = i;
        }

        for (int row = 1; row < lines.Length; row++)
        {
            string[] vals = lines[row].Split(',');
            if (vals.Length == 0) continue;

            if (residueIdx < 0 || residueIdx >= vals.Length) continue;
            if (!int.TryParse(vals[residueIdx], out int resID)) continue;

            float bVal = 0.5f;
            if (bFactorIdx >= 0 && bFactorIdx < vals.Length)
                float.TryParse(vals[bFactorIdx], out bVal);
            residueAmplitudes[resID] = bVal;

            float fVal = 0.5f;
            if (frustrationIdx >= 0 && frustrationIdx < vals.Length)
                float.TryParse(vals[frustrationIdx], out fVal);
            residueFrustrations[resID] = fVal;

            float sVal = 0.5f;
            if (stabilityIdx >= 0 && stabilityIdx < vals.Length)
                float.TryParse(vals[stabilityIdx], out sVal);
            residueStabilities[resID] = sVal;
        }
    }

    private void ReindexResidues()
    {
        residuePositions = new List<Vector3>(residuePositions);
    }

    private void CenterProtein()
    {
        if (residuePositions.Count == 0) return;
        Vector3 centroid = Vector3.zero;
        foreach (var p in residuePositions) centroid += p;
        centroid /= residuePositions.Count;

        for (int i = 0; i < residuePositions.Count; i++)
            residuePositions[i] -= centroid;
    }

    [System.Serializable]
    public class ResiduePosition
    {
        public float x;
        public float y;
        public float z;
    }

    [System.Serializable]
    public class Residue
    {
        public int residue_id;
        public string residue_name;
        public ResiduePosition position;
    }

    [System.Serializable]
    public class ResidueList
    {
        public List<Residue> residues;
    }
}