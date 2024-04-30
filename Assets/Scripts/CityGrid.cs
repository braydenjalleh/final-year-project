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
    
    public float scale = .1f;
    public int size = 100;
    public int minStreetSpacing = 3;
    public int maxStreetSpacing = 15;
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
    //Props
    public float propNoiseScale = 0.6f;
    public float propDensity = 0.7f;
    public GameObject[] propsPrefabs;
    public GameObject[] floraPrefabs;
    public float floraNoiseScale = 0.5f; 
    public float floraDensity = 0.5f;

    private int totalBuildings = 0;
    private int totalProps = 0;
    private int totalFlora = 0;

    CityCells[,] grid;


    void Start()
    {
        
        grid = new CityCells[size, size];

        occupied = new bool[size, size];
        GenerateCityLayout();
        DrawMesh(grid);
        DrawTexture(grid);
        GenerateBuildings();
        GenerateProps(grid, occupied);
        GenerateFlora(grid, occupied);
        Debug.Log($"Total Buildings: {totalBuildings}, Total Props: {totalProps}, Total Flora: {totalFlora}, Total Nature: {totalFlora + totalProps}");
    }

    void GenerateCityLayout()
    {
        //Initializing the entire grid as a building cell first
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
        int horizontalStreets = 10;
        while (horizontalStreets < size)
        {
            for (int x = 0; x < size; x++)
            {
                grid[x, horizontalStreets] = new CityCells(true);
            }
            //Debug.Log("Horizontal Street at Y: " + yStreet);
            horizontalStreetPosition.Add(horizontalStreets);
            horizontalStreets += 10; 
        }

        /********************************************************VERTICAL STREETS SECITON***************************************************************************/

        //ORIGINAL EDITION SUPER BASIC WITH GRID LAYOUT SPREADING ACROSS THE CITY.
        // Randomly add vertical streets
        //int xStreet = UnityEngine.Random.Range(minStreetSpacing, maxStreetSpacing);
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
        //    xStreet += UnityEngine.Random.Range(minStreetSpacing, maxStreetSpacing);
        //}

        //
        //Edition 2. Doing a different logic of creating vertical streets that go up/down
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

    /******************************Building Generations/placement methods***********************************/

    void GenerateBuildings()
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



    bool CanPlaceBuilding(int x, int y, BuildingData building, Quaternion rotation)
    {
        // Adjust width and depth based on rotation if necessary
        int width = building.width;
        int depth = building.depth;
        if (rotation.eulerAngles.y == 90 || rotation.eulerAngles.y == 270)
        {
            width = building.depth;
            depth = building.width;
        }

        if (x < 0 || x + width > size || y < 0 || y + depth > size)
            return false;

        for (int offsetX = 0; offsetX < width; offsetX++)
        {
            for (int offsetY = 0; offsetY < depth; offsetY++)
            {
                int checkX = x + offsetX;
                int checkY = y + offsetY;
                if (occupied[checkX, checkY] || grid[checkX, checkY].isRoad || checkX > size)
                    return false;
            }
        }
        return true;
    }



    void PlaceBuilding(int x, int y, string zone)
    {
        BuildingData selectedBuilding = ChooseBuildingForSpace(x, y, zone);
        BuildingData initialBuilding = selectedBuilding; // Keep the initial choice for comparison

        while (selectedBuilding != null)
        {
            // Determine the optimal rotation for the selected building
            Quaternion rotation = DetermineBuildingRotation(x, y, selectedBuilding);

            // Check if the building can be placed with the given rotation and if it is adjacent to a road
            if (CanPlaceBuilding(x, y, selectedBuilding, rotation) && IsAdjacentToRoad(x, y, selectedBuilding))
            {
                // Adjust position based on rotation
                Vector3 worldPosition = AdjustPositionBasedOnRotation(new Vector3(x, 0, y), rotation, selectedBuilding);
                // Instantiate and mark occupied
                GameObject buildingInstance = Instantiate(selectedBuilding.prefab, worldPosition, rotation);
                MarkBuildingOccupied(x, y, selectedBuilding, rotation);
                totalBuildings++;
                return; // Successful placement
            }
            else
            {
                // If the chosen building doesn't fit, attempt to find an alternative
                Debug.Log($"Attempting to find an alternative building for {x},{y}.");
                selectedBuilding = FindAlternativeBuilding(x, y, zone, selectedBuilding);

                // Avoid infinite loop in case where no suitable building is found
                if (selectedBuilding == initialBuilding)
                {
                    Debug.Log($"Cycled through all options, no buildings fit at {x},{y}.");
                    return;
                }
            }
        }

        Debug.Log($"No suitable buildings found for {x},{y}.");
    }

    BuildingData FindAlternativeBuilding(int x, int y, string zone, BuildingData previousBuilding)
    {
        List<BuildingData> candidates = new List<BuildingData>();
        foreach (var building in buildingData)
        {
            if (building.zone == zone && building != previousBuilding)
            {
                Quaternion newRotation = DetermineBuildingRotation(x, y, building);
                if (CanPlaceBuilding(x, y, building, newRotation) && IsAdjacentToRoad(x, y, building))
                {
                    candidates.Add(building);
                }
            }
        }

        if (candidates.Count > 0)
        {
            System.Random rng = new System.Random();
            int randomIndex = rng.Next(candidates.Count);
            return candidates[randomIndex];
        }

        return null;
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
    Quaternion DetermineBuildingRotation(int x, int y, BuildingData building)
    {
        // Initialize the direction weights
        int northWeight = 0, southWeight = 0, eastWeight = 0, westWeight = 0;

        // Check each edge of the building for adjacent roads
        for (int offsetX = 0; offsetX < building.width; offsetX++)
        {
            if (y + building.depth < size && IsRoad(x + offsetX, y + building.depth))
                northWeight++;
            if (y - 1 >= 0 && IsRoad(x + offsetX, y - 1))
                southWeight++;
        }

        for (int offsetY = 0; offsetY < building.depth; offsetY++)
        {
            if (x + building.width < size && IsRoad(x + building.width, y + offsetY))
                eastWeight++;
            if (x - 1 >= 0 && IsRoad(x - 1, y + offsetY))
                westWeight++;
        }

        // Determine the direction with the maximum weight
        int maxWeight = Mathf.Max(northWeight, southWeight, eastWeight, westWeight);
        if (northWeight == maxWeight)
            return Quaternion.Euler(0, 180, 0); // Facing north
        if (southWeight == maxWeight)
            return Quaternion.identity; // Default rotation, facing south
        if (eastWeight == maxWeight)
            return Quaternion.Euler(0, 270, 0); // Facing east
        if (westWeight == maxWeight)
            return Quaternion.Euler(0, 90, 0); // Facing west

        return Quaternion.identity; // Default rotation if no roads or equal weights
    }

    Vector3 AdjustPositionBasedOnRotation(Vector3 originalPosition, Quaternion rotation, BuildingData building)
    {
        switch (rotation.eulerAngles.y)
        {
            case 0: // Facing south, no adjustment
                break;
            case 90: // Facing west
                     // Assuming that the depth and width adjustments are to align the front of the building with the grid line facing the road
                originalPosition += new Vector3(0, 0, building.width);
                break;
            case 180: // Facing north
                originalPosition += new Vector3(building.width, 0, building.depth);
                break;
            case 270: // Facing east
                originalPosition += new Vector3(building.depth , 0, 0);
                break;
        }
        return originalPosition;
    }



    BuildingData ChooseBuildingForSpace(int x, int y, string zone)
    {
        List<BuildingData> potentialBuildings = new List<BuildingData>();
        foreach (var building in buildingData)
        {
            if (building.zone == zone)
            {
                // Check if building can be placed facing its default orientation
                bool canPlace = CanPlaceBuilding(x, y, building, Quaternion.identity);  // Default orientation

                // Optionally check other orientations if necessary
                if (!canPlace) canPlace = CanPlaceBuilding(x, y, building, Quaternion.Euler(0, 90, 0));  // Rotate 90 degrees
                if (!canPlace) canPlace = CanPlaceBuilding(x, y, building, Quaternion.Euler(0, 180, 0)); // Rotate 180 degrees
                if (!canPlace) canPlace = CanPlaceBuilding(x, y, building, Quaternion.Euler(0, 270, 0)); // Rotate 270 degrees

                if (canPlace)
                {
                    potentialBuildings.Add(building);
                }
            }
        }
        if (potentialBuildings.Count == 0)
            return null;

        int randomIndex = UnityEngine.Random.Range(0, potentialBuildings.Count);
        return potentialBuildings[randomIndex];
    }

    void MarkBuildingOccupied(int x, int y, BuildingData building, Quaternion rotation)
    {
        int width = building.width;
        int depth = building.depth;
        if (rotation.eulerAngles.y == 90 || rotation.eulerAngles.y == 270)
        {
            width = building.depth;
            depth = building.width;
        }

        for (int offsetX = 0; offsetX < width; offsetX++)
        {
            for (int offsetY = 0; offsetY < depth; offsetY++)
            {
                occupied[x + offsetX, y + offsetY] = true;
            }
        }
    }
    /**********************************************Generate Props**********************************************/

    void GenerateProps(CityCells[,] grid, bool[,] occupied)
    {
        float[,] noiseMap = new float[size, size];
        float xOffset = UnityEngine.Random.Range(-10000f, 10000f);
        float yOffset = UnityEngine.Random.Range(-10000f, 10000f);

        // Generate Perlin noise map for prop placement
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noiseValue = Mathf.PerlinNoise(x * propNoiseScale + xOffset, y * propNoiseScale + yOffset);
                noiseMap[x, y] = noiseValue;
            }
        }

        // Place props based on noise map, avoiding roads and occupied cells
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                CityCells cell = grid[x, y];
                if (!cell.isRoad && !occupied[x, y]) // Check also that the cell is not occupied
                {
                    float v = UnityEngine.Random.Range(0f, propDensity);
                    if (noiseMap[x, y] < v)
                    {
                        GameObject propInstance = propsPrefabs[UnityEngine.Random.Range(0, propsPrefabs.Length)];
                        GameObject prop = Instantiate(propInstance, new Vector3(x, 0, y), Quaternion.Euler(0, UnityEngine.Random.Range(0, 360f), 0));
                        prop.transform.localScale = Vector3.one * UnityEngine.Random.Range(.8f, 1.2f);
                        totalProps++;
                    }
                }
            }
        }
    }
    void GenerateFlora(CityCells[,] grid, bool[,] occupied)
    {
        float[,] noiseMap = new float[size, size];
        float xOffset = UnityEngine.Random.Range(-10000f, 10000f);
        float yOffset = UnityEngine.Random.Range(-10000f, 10000f);

        // Generate Perlin noise map for prop placement
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noiseValue = Mathf.PerlinNoise(x * floraNoiseScale + xOffset, y * floraNoiseScale + yOffset);
                noiseMap[x, y] = noiseValue;
            }
        }

        // Place props based on noise map, avoiding roads and occupied cells
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                CityCells cell = grid[x, y];
                if (!cell.isRoad && !occupied[x, y]) // Check also that the cell is not occupied
                {
                    float v = UnityEngine.Random.Range(0f, floraDensity);
                    if (noiseMap[x, y] < v)
                    {
                        GameObject propInstance = floraPrefabs[UnityEngine.Random.Range(0, floraPrefabs.Length)];
                        GameObject prop = Instantiate(propInstance, new Vector3(x, 0, y), Quaternion.Euler(0, UnityEngine.Random.Range(0, 360f), 0));
                        prop.transform.localScale = Vector3.one * UnityEngine.Random.Range(.8f, 1.2f);
                        totalFlora++;
                    }
                }
            }
        }
    }


    /**********************************************Draw Mesh****************************************/

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

    /*****************************************Drawing Textures**************************************/
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
                else if (occupied[x, y])
                {
                    colorMap[y * size + x] = Color.red;
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
}
