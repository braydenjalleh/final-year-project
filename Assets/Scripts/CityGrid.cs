using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

public class CityGrid : MonoBehaviour
{
    public Material terrainMaterial;
    public float waterLevel = .4f;
    public float scale = .1f;
    public int size = 100;
    private const float VerticalStreetChance = 0.3f;
    public int minStreetSpacing = 3;
    public int maxStreetSpacing = 9;
    public int roadHeightOffset = 0;

    //Road objects
    public GameObject straightRoadPrefab;
    public GameObject intersectionRoadPrefab;
    public GameObject threeWayIntersectionPrefab;
    public GameObject cornerRoadPrefab;
    //Building objects
    public GameObject[] buildingPrefabs;
    public GameObject[] housePrefabs;
    public GameObject[] apartmentPrefabs;

    public BuildingData[] buildingData;
    bool[,] occupied;

    CityCells[,] grid;


    void Start()
    {
        float[,] noiseMap = new float[size, size];
        float xOffset = UnityEngine.Random.Range(-10000f, 10000f);
        float yOffset = UnityEngine.Random.Range(-10000f, 10000f);
        //for (int y = 0; y < size; y++)
        //{
        //    for(int x = 0; x < size; x++)
        //    { 
        //        float noiseValue = Mathf.PerlinNoise(x * scale + xOffset, y * scale + yOffset);
        //        noiseMap[x, y] = noiseValue;
        //    }
        //}

        float[,] falloffMap = new float[size, size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float xv = x / (float)size * 2 - 1;
                float yv = y / (float)size * 2 - 1;
                float v = Mathf.Max(Mathf.Abs(xv), Mathf.Abs(yv));
                falloffMap[x, y] = Mathf.Pow(v, 3f) / (Mathf.Pow(v, 3f) + Mathf.Pow(2.2f - 2.2f * v, 3f));
            }
        }
        grid = new CityCells[size, size];

        occupied = new bool[size, size];
        GenerateCityLayout();

        //grid = new CityCells[size, size];
        //for (int y = 0; y < size; y++)
        //{
        //    for (int x = 0; x < size; x++)
        //    {
        //        float noiseValue = noiseMap[x, y];
        //        noiseValue -= falloffMap[x,y];
        //        bool isWater = noiseValue < waterLevel;
        //        CityCells cell = new CityCells(isWater);
        //        grid[y, x] = cell;
        //    }
        //}
        GenerateCity();
        DrawMesh(grid);
        DrawTexture(grid);
    }

    void GenerateCityLayout()
    {
        //Initializing the entire grid as a building first
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                grid[x, y] = new CityCells(false);
            }
        }

        // Create a border of roads on the perimeter of the grid
        for (int x = 0; x < size; x++)
        {
            grid[x, 0] = new CityCells(true); // Bottom border
            grid[x, size - 1] = new CityCells(true); // Top border
        }

        for (int y = 0; y < size; y++)
        {
            grid[0, y] = new CityCells(true); // Left border
            grid[size - 1, y] = new CityCells(true); // Right border
        }

        //Start drawing the streets within the grid at random points.
        List<int> horizontalStreetPosition = new List<int>();
        // Randomly add horizontal streets
        int horizontalStreets = 10/*Random.Range(minStreetSpacing, maxStreetSpacing)*/;
        while (horizontalStreets < size)
        {
            for (int x = 0; x < size; x++)
            {
                grid[x, horizontalStreets] = new CityCells(true);
            }
            //Debug.Log("Horizontal Street at Y: " + yStreet);
            horizontalStreetPosition.Add(horizontalStreets);
            horizontalStreets += 10/*Random.Range(minStreetSpacing, maxStreetSpacing)*/; //Temporarily set the increase in horizontal spacing size to 10+
        }

        /********************************************************VERTICAL STREETS SECITON***************************************************************************/

        //ORIGINAL EDITION SUPER BASIC WITH GRID LAYOUT SPREADING ACROSS THE CITY.
        // Randomly add vertical streets
        //int xStreet = Random.Range(minStreetSpacing, maxStreetSpacing);
        //while (xStreet < size)
        //{
        //    for (int y = 0; y < size; y++)
        //    {
        //        if (grid[xStreet, y].isRoad == false) // This ensures we don't overwrite where two streets cross
        //        {
        //            grid[xStreet, y] = new CityCells(true);
        //        }
        //    }
        //    Debug.Log("Vertical Street at X: " + xStreet);
        //    xStreet += Random.Range(minStreetSpacing, maxStreetSpacing);
        //}

        //EDITION 2 Basic random streets printed. *SO FAR THE BEST OPTION*
        //for (int verticalStreets = 0; verticalStreets < size; verticalStreets += Random.Range(minStreetSpacing, maxStreetSpacing))
        //{
        //    // Choose random start and end points for the vertical street among the horizontal streets
        //    int startHorizontalIndex = Random.Range(0, horizontalStreetPosition.Count);
        //    int endHorizontalIndex = Random.Range(startHorizontalIndex, horizontalStreetPosition.Count);

        //    for (int i = startHorizontalIndex; i <= endHorizontalIndex; i++)
        //    {
        //        int y = horizontalStreetPosition[i];
        //        grid[verticalStreets, y] = new CityCells(true);

        //        // Fill the space between this horizontal street and the next, if it's not the last one
        //        if (i < endHorizontalIndex)
        //        {
        //            int nextY = horizontalStreetPosition[i + 1];
        //            for (int fillY = y + 1; fillY < nextY; fillY++)
        //            {
        //                grid[verticalStreets, fillY] = new CityCells(true);
        //            }
        //        }
        //    }
        //} 

        //Working edition 
        //Edition... Doing a different logic of creating vertical streets that go up/down. Very bad and messy
        // New logic for creating vertical streets
        for (int row = 0; row < size; row += 10)
        {
            bool verticalUpStreetCreated = false;
            bool verticalDownStreetCreated = false;

            for (int col = 0; col < size; col++)
            {
                // Randomly decide whether to create a vertical street at this column
                if (!(UnityEngine.Random.value < 0.5f)) continue; // Adjust the 0.5f to desired probability

                // Choose a random direction for the street (0 for up, 1 for down)
                int randomDirection = UnityEngine.Random.Range(0, 2);
                int endRow = (randomDirection == 0) ? row - 10 : row + 10;
                if (endRow >= size) endRow = size - 1;

                // Try to create the street
                bool isCreated = TryCreateVerticalStreet(row, col, endRow, grid);
                if (isCreated && endRow < row)
                {
                    verticalUpStreetCreated = true;
                }
                else if (isCreated && endRow > row)
                {
                    verticalDownStreetCreated = true;
                }
            }

            // Ensure at least one street is created going up and down from this row
            if (!verticalUpStreetCreated && row != 0) //Adjust for first row condition
            {
                for (int col = 0; col < size; col++)
                {
                    if (TryCreateVerticalStreet(row, col, row + 10, grid)) break;
                }
            }

            if (!verticalDownStreetCreated && row != size - 10) //Adjust for last row condition
            {
                for (int col = 0; col < size; col++)
                {
                    if (TryCreateVerticalStreet(row, col, row - 10, grid)) break; 
                }
            }
        }

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (grid[x, y].isRoad)
                {
                    GameObject roadPrefab = DetermineRoadPrefab(x, y);
                    Quaternion rotation = DetermineRoadRotation(x, y); 
                    Instantiate(roadPrefab, new Vector3(x + 0.5f, roadHeightOffset, y + 0.5f), rotation);
                }
            }
        }

    }

    GameObject DetermineRoadPrefab(int x, int y)
    {
        bool north = IsRoad(x, y + 1);
        bool south = IsRoad(x, y - 1);
        bool east = IsRoad(x + 1, y);
        bool west = IsRoad(x - 1, y);

        // Check for the four corners of the map first
        if ((x == 0 && y == 0 && east && north) || // Bottom left corner
            (x == 0 && y == size - 1 && east && south) || // Top left corner
            (x == size - 1 && y == 0 && west && north) || // Bottom right corner
            (x == size - 1 && y == size - 1 && west && south)) // Top right corner
        {
            return cornerRoadPrefab;
        }

        int roadCount = (north ? 1 : 0) + (south ? 1 : 0) + (east ? 1 : 0) + (west ? 1 : 0);

        // For a four-way intersection
        if (north && south && east && west)
        {
            return intersectionRoadPrefab;
        }
        // For a three-way intersection
        else if (roadCount == 3)
        {
            return threeWayIntersectionPrefab;
        }
        else
        {
            return straightRoadPrefab;
        }
    }


    Quaternion DetermineRoadRotation(int x, int y)
    {
        bool north = IsRoad(x, y + 1);
        bool south = IsRoad(x, y - 1);
        bool east = IsRoad(x + 1, y);
        bool west = IsRoad(x - 1, y);

        // If the road runs north-south, no rotation needed
        // If the road runs east-west
        if (east && west && !north && !south)
        {
            return Quaternion.Euler(0, 90, 0); // Rotate 90 degrees to align east-west
        }
        else if (north && south && east && !west) // T-intersection opening to the east
        {
            return Quaternion.Euler(0, 90, 0);
        }
        else if(north && south && !east && west) // T-intersection opening to the west
        {
            return Quaternion.Euler(0, 270, 0);
        }
        else if(!north && south && east && west) // T-intersection opening to the south
        {
            return Quaternion.Euler(0, 180, 0);
        }
        else if (north && east && !west && !south) // Corner road
        {
            return Quaternion.Euler(0, 90, 0);
        }
        else if (!north && east && !west && south) // Corner road
        {
            return Quaternion.Euler(0, 180, 0);
        }
        else if (!north && !east && west && south) //Corner Road
        {
            return Quaternion.Euler(0, 270, 0);
        }
        // If the road runs north-south, no rotation needed
        // If the road has intersections on all four sides, no specific rotation is required
        return Quaternion.identity;
    }


    bool TryCreateVerticalStreet(int startRow, int col, int endRow, CityCells[,] grid)
    {
        //If the endRow is beyond the grid size, adjust it to the last index
        endRow = (endRow >= size) ? size - 1 : endRow;
        endRow = (endRow < 0) ? 0 : endRow;

        int step = (endRow > startRow) ? 1 : -1; // Determine the direction of the street

        // Ensure there's a minimum spacing between streets
        int minSpacing = 4; 
        for (int checkCol = Math.Max(col - minSpacing, 0); checkCol <= Math.Min(col + minSpacing, size - 1); checkCol++)
        {
            if (grid[startRow, checkCol].isRoad) // Check if the cell is a road
            {
               // Debug.LogFormat("Street creation blocked at col {0} due to existing road", checkCol); //Debugging
                return false; // Too close to another street
            }
        }

        for (int row = startRow; row != endRow; row += step)
        {
            if (row < 0 || row >= size)
            {
                Debug.LogFormat("Street creation out of bounds at row {0}", row);
                return false;
            } // Check bounds

            grid[col, row] = new CityCells(true); // Set the cell as a road
        }
        Debug.LogFormat("Street created starting at row {0}, col {1}, ending at row {2}", startRow, col, endRow);
        return true;
    }



    bool IsRoad(int x, int y)
    {
        if (x < 0 || x >= size || y < 0 || y >= size) return false; // Out of bounds check
        return grid[x, y].isRoad;
    }

    void GenerateCity()
    {
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (!grid[x, y].isRoad && !occupied[x, y])
                {
                    string zone = DetermineZone(y);
                    PlaceBuilding(x, y, zone);
                }
            }
        }
    }

    string DetermineZone(int y)
    {
        // Example: Top half is residential, bottom half is commercial
        return (y > size / 2) ? "residential" : "commercial";
    }

    bool CanPlaceBuilding(int x, int y, BuildingData building)
    {
        if (x < 0 || x + building.width > size || y < 0 || y + building.depth > size)
            return false;

        for (int offsetX = 0; offsetX < building.width; offsetX++)
        {
            for (int offsetY = 0; offsetY < building.depth; offsetY++)
            {
                int checkX = x + offsetX;
                int checkY = y + offsetY;
                if (occupied[checkX, checkY] || grid[checkX, checkY].isRoad)
                    return false;
            }
        }
        return true;
    }



    //edition 3
    void PlaceBuilding(int x, int y, string zone)
    {
        BuildingData selectedBuilding = ChooseBuildingForSpace(x, y, zone);
        if (selectedBuilding == null || !CanPlaceBuilding(x, y, selectedBuilding))
        {
            Debug.Log($"Cannot place building at {x},{y} - either out of bounds or overlaps with roads/other buildings.");
            return;
        }

        // Check if the current position is adjacent to a road, considering the building's footprint
        if (!IsAdjacentToRoad(x, y, selectedBuilding))
        {
            Debug.Log($"Cannot place building at {x},{y} - not adjacent to any road.");
            return;
        }

        Vector3 worldPosition = new Vector3(x, 0, y); // Center the building in the grid cell
        GameObject buildingInstance = Instantiate(selectedBuilding.prefab, worldPosition, Quaternion.identity);
        MarkBuildingOccupied(x, y, selectedBuilding);
    }



    bool IsAdjacentToRoad(int x, int y, BuildingData building)
    {
        // Check each cell that the building would occupy and its surroundings for a road
        for (int offsetX = 0; offsetX < building.width; offsetX++)
        {
            // Check north of the building
            if (y + building.depth < size && IsRoad(x + offsetX, y + building.depth))
                return true;

            // Check south of the building
            if (y - 1 >= 0 && IsRoad(x + offsetX, y - 1))
                return true;
        }

        for (int offsetY = 0; offsetY < building.depth; offsetY++)
        {
            // Check east of the building
            if (x + building.width < size && IsRoad(x + building.width, y + offsetY))
                return true;

            // Check west of the building
            if (x - 1 >= 0 && IsRoad(x - 1, y + offsetY))
                return true;
        }

        return false;
    }



    BuildingData ChooseBuildingForSpace(int x, int y, string zone)
    {
        List<BuildingData> potentialBuildings = new List<BuildingData>();
        foreach (var building in buildingData)
        {
            if (building.zone == zone && CanPlaceBuilding(x, y, building))
            {
                potentialBuildings.Add(building);
            }
        }
        if (potentialBuildings.Count == 0)
            return null;

        int randomIndex = UnityEngine.Random.Range(0, potentialBuildings.Count);
        return potentialBuildings[randomIndex];
    }

    void MarkBuildingOccupied(int x, int y, BuildingData building)
    {
        for (int offsetX = 0; offsetX < building.width; offsetX++)
        {
            for (int offsetY = 0; offsetY < building.depth; offsetY++)
            {
                occupied[x + offsetX, y + offsetY] = true;
            }
        }
    }

    void DrawMesh(CityCells[,] grid)
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                CityCells cell = grid[x, y];
                //if (!cell.isRoad)
                //{
                Vector3 a = new Vector3(x - .5f, 0, y + .5f);
                Vector3 b = new Vector3(x + .5f, 0, y + .5f);
                Vector3 c = new Vector3(x - .5f, 0, y - .5f);
                Vector3 d = new Vector3(x + .5f, 0, y - .5f);
                Vector2 uvA = new Vector2(x / (float)size, y / (float)size); //uvA represents the UV coordinate for the bottom-left vertex a.
                Vector2 uvB = new Vector2((x + 1) / (float)size, y / (float)size); //uvB represents the UV coordinate for the bottom-right vertex b
                Vector2 uvC = new Vector2(x / (float)size, (y + 1) / (float)size);
                Vector2 uvD = new Vector2((x + 1) / (float)size, (y + 1) / (float)size);
                Vector3[] v = new Vector3[] { a, b, c, b, d, c };
                Vector2[] uv = new Vector2[] { uvA, uvB, uvC, uvB, uvD, uvC };
                // UV to tell where the textures sho8uld go on the mesh/
                for (int i = 0; i < 6; i++)
                {
                    vertices.Add(v[i]);
                    triangles.Add(triangles.Count);
                    uvs.Add(uv[i]);
                }
                //}
            }
        }
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();

        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;


        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.material = terrainMaterial;
    }

    void DrawTexture(CityCells[,] grid)
    {
        Texture2D texture = new Texture2D(size, size);
        Color[] colorMap = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                CityCells cell = grid[x, y];
                if (cell.isRoad)
                {
                    colorMap[y * size + x] = Color.black;
                }
                else
                {
                    colorMap[(y * size) + x] = Color.green;
                }
            }
        }
        texture.filterMode = FilterMode.Point;
        texture.SetPixels(colorMap);
        texture.Apply();

        MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
        meshRenderer.material = terrainMaterial;
        meshRenderer.material.mainTexture = texture;
    }

    //void OnDrawGizmos()
    //{
    //    if (!Application.isPlaying) return;
    //    for (int y = 0; y < size; y++)
    //    {
    //        for (int x = 0; x < size; x++)
    //        {
    //            CityCells cell = grid[y, x];
    //            if (cell.isRoad)
    //                Gizmos.color = Color.black;
    //            else
    //                Gizmos.color = Color.green;
    //            Vector3 pos = new Vector3(x, 0, y);
    //            Gizmos.DrawCube(pos, Vector3.one);
    //        }
    //    }
    //}
}
