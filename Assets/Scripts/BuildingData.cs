using UnityEngine;

[System.Serializable]
public class BuildingData
{
    public GameObject prefab;
    public int width; // Number of grid cells in the x-direction
    public int depth; // Number of grid cells in the y-direction
    public string zone;
}
