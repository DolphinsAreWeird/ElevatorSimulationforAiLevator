using UnityEngine;

public class BuildingGenerator : MonoBehaviour
{
    public GameObject floorPrefab;     // Assign the FloorPrefab here in the Inspector
    public int numberOfFloors = 5;     // Number of floors to generate
    public float floorHeight = 3f;     // Height between floors
    public int peoplePerFloor = 3;     // Number of people to spawn per floor

    void Start()
    {
        GenerateBuilding();
    }

    void GenerateBuilding()
    {
        for (int i = 0; i < numberOfFloors; i++)
        {
            // Instantiate floor at position (0, i * floorHeight, 0)
            GameObject floor = Instantiate(floorPrefab, new Vector3(0, i * floorHeight, 0), Quaternion.identity);
            floor.transform.parent = transform; // Parent floors to this GameObject
            floor.name = $"Floor_{i}";
        }
    }
}