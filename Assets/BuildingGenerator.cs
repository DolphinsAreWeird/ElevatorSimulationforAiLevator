using UnityEngine;
using System.Collections.Generic;

public class EnhancedBuildingGenerator : MonoBehaviour
{
    [Header("Building Settings")]
    public GameObject floorPrefab;
    public GameObject elevatorPrefab;
    public GameObject personPrefab;
    public int numberOfFloors = 5;
    public float floorHeight = 3f;
    public Vector2 floorSize = new Vector2(20f, 20f); // Width, Depth
    
    [Header("Elevator Settings")]
    [Range(1, 4)]
    public int numberOfElevators = 1;
    public float elevatorSpacing = 5f; // Distance between multiple elevators
    
    [Header("Person Settings")]
    public int minPeoplePerFloor = 2;
    public int maxPeoplePerFloor = 5;
    
    // References to generated objects
    private List<GameObject> floors = new List<GameObject>();
    private List<Elevator> elevators = new List<Elevator>();
    private List<EnhancedPerson> people = new List<EnhancedPerson>();
    
    // References to managers
    private ElevatorController elevatorController;
    
    // Debug flag to track initialization
    private bool hasInitialized = false;
    
    void Start()
    {
        // Generate the building if it hasn't been done yet
        if (!hasInitialized)
        {
            // First check if there are existing elevators - clean them up
            CleanExistingObjects();
            
            // Now generate the building
            GenerateBuilding();
        }
    }
    
    // Clean up any existing objects that might conflict
    private void CleanExistingObjects()
    {
        // Find and destroy any existing elevators in the scene (not managed by us)
        Elevator[] existingElevators = FindObjectsOfType<Elevator>();
        foreach (Elevator elevator in existingElevators)
        {
            if (!elevators.Contains(elevator))
            {
                Debug.Log($"Destroying unmanaged elevator: {elevator.name}");
                Destroy(elevator.gameObject);
            }
        }
        
        // Find and destroy any existing elevator controllers
        ElevatorController[] existingControllers = FindObjectsOfType<ElevatorController>();
        foreach (ElevatorController controller in existingControllers)
        {
            if (controller != elevatorController)
            {
                Debug.Log($"Destroying unmanaged elevator controller: {controller.name}");
                Destroy(controller.gameObject);
            }
        }
    }
    
    public void GenerateBuilding()
    {
        // Clear existing building if any
        ClearBuilding();
        
        // Generate floors
        GenerateFloors();
        
        // Generate elevators
        GenerateElevators();
        
        // Generate people
        GeneratePeople();
        
        // Finalize setup
        FinalizeSetup();
        
        hasInitialized = true;
        Debug.Log("Building generation completed successfully");
    }
    
    private void ClearBuilding()
    {
        // Destroy all existing floors, elevators, and people
        foreach (var floor in floors)
        {
            if (floor != null) Destroy(floor);
        }
        
        foreach (var elevator in elevators)
        {
            if (elevator != null) Destroy(elevator.gameObject);
        }
        
        foreach (var person in people)
        {
            if (person != null) Destroy(person.gameObject);
        }
        
        floors.Clear();
        elevators.Clear();
        people.Clear();
        
        // Also destroy elevator controller if it exists
        if (elevatorController != null)
        {
            Destroy(elevatorController.gameObject);
            elevatorController = null;
        }
    }
    
    private void GenerateFloors()
    {
        for (int i = 0; i < numberOfFloors; i++)
        {
            // Check if prefab is assigned
            if (floorPrefab == null)
            {
                Debug.LogError("Floor prefab is not assigned!");
                return;
            }
            
            // Instantiate floor at position (0, i * floorHeight, 0)
            GameObject floor = Instantiate(floorPrefab, new Vector3(0, i * floorHeight, 0), Quaternion.identity);
            floor.transform.parent = transform;
            floor.name = $"Floor_{i}";
            
            // Scale floor to desired size
            floor.transform.localScale = new Vector3(floorSize.x, 0.1f, floorSize.y);
            
            floors.Add(floor);
            
            // Add simple walls for boundaries
            CreateWalls(floor, i);
        }
        
        Debug.Log($"Generated {numberOfFloors} floors.");
    }
    
    private void CreateWalls(GameObject floor, int floorNumber)
    {
        // Create four walls around the edge of the floor
        float wallHeight = floorHeight * 0.9f;
        float halfWidth = floorSize.x / 2;
        float halfDepth = floorSize.y / 2;
        float wallThickness = 0.3f;
        
        // North wall
        CreateWall(floor, new Vector3(0, wallHeight/2, halfDepth - wallThickness/2), 
            new Vector3(floorSize.x, wallHeight, wallThickness));
        
        // South wall
        CreateWall(floor, new Vector3(0, wallHeight/2, -halfDepth + wallThickness/2), 
            new Vector3(floorSize.x, wallHeight, wallThickness));
        
        // East wall
        CreateWall(floor, new Vector3(halfWidth - wallThickness/2, wallHeight/2, 0), 
            new Vector3(wallThickness, wallHeight, floorSize.y));
        
        // West wall with gap for elevator
        float doorWidth = 3.0f * numberOfElevators; // Make door wider if more elevators
        float leftWallWidth = (floorSize.y - doorWidth) / 2;
        float rightWallWidth = (floorSize.y - doorWidth) / 2;
        
        // Left portion of west wall
        if (leftWallWidth > 0)
        {
            CreateWall(floor, new Vector3(-halfWidth + wallThickness/2, wallHeight/2, -halfDepth + leftWallWidth/2), 
                new Vector3(wallThickness, wallHeight, leftWallWidth));
        }
        
        // Right portion of west wall
        if (rightWallWidth > 0)
        {
            CreateWall(floor, new Vector3(-halfWidth + wallThickness/2, wallHeight/2, halfDepth - rightWallWidth/2), 
                new Vector3(wallThickness, wallHeight, rightWallWidth));
        }
    }
    
    private void CreateWall(GameObject parent, Vector3 localPosition, Vector3 size)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.parent = parent.transform;
        wall.transform.localPosition = localPosition;
        wall.transform.localScale = size;
        wall.name = "Wall";
        
        // Set wall tag and layer for collision detection
        wall.tag = "Wall";
        
        // Add collider for physics interactions (it's already added by CreatePrimitive)
    }
    
    private void GenerateElevators()
    {
        // Check if prefab is assigned
        if (elevatorPrefab == null)
        {
            Debug.LogError("Elevator prefab is not assigned!");
            return;
        }
        
        // Calculate elevator positions
        float halfWidth = floorSize.x / 2;
        float startZ = -(numberOfElevators - 1) * elevatorSpacing / 2;
        
        // Debug positioning calculation
        Debug.Log($"Elevator positioning - Half width: {halfWidth}, Start Z: {startZ}, Spacing: {elevatorSpacing}");
        
        for (int i = 0; i < numberOfElevators; i++)
        {
            float elevatorZ = startZ + i * elevatorSpacing;
            
            // Position elevator outside the building (beyond west wall) and ON TOP of the floor
            Vector3 elevatorPos = new Vector3(
                -halfWidth - 1.5f,  // X: 1.5 units outside the west wall
                0 + 0.05f,          // Y: Slightly above y=0 (not inside floor)
                elevatorZ           // Z: Based on spacing calculation
            );
            
            Debug.Log($"Creating Elevator {i} at position: {elevatorPos}");
            
            GameObject elevatorObj = Instantiate(elevatorPrefab, elevatorPos, Quaternion.identity);
            elevatorObj.name = $"Elevator_{i}";
            elevatorObj.transform.parent = transform;
            
            Elevator elevatorComp = elevatorObj.GetComponent<Elevator>();
            if (elevatorComp == null)
            {
                elevatorComp = elevatorObj.AddComponent<Elevator>();
            }
            
            // Configure elevator
            elevatorComp.floorHeight = floorHeight;
            elevatorComp.topFloor = numberOfFloors - 1;
            
            elevators.Add(elevatorComp);
        }
        
        Debug.Log($"Generated {numberOfElevators} elevators.");
    }
    
    private void GeneratePeople()
    {
        // Check if prefab is assigned
        if (personPrefab == null)
        {
            Debug.LogError("Person prefab is not assigned!");
            return;
        }
        
        int totalPeople = 0;
        
        for (int floor = 0; floor < numberOfFloors; floor++)
        {
            int peopleCount = Random.Range(minPeoplePerFloor, maxPeoplePerFloor + 1);
            
            for (int i = 0; i < peopleCount; i++)
            {
                // Calculate random position on the floor
                float halfWidth = floorSize.x / 2;
                float halfDepth = floorSize.y / 2;
                float margin = 2.0f; // Keep away from walls
                
                float posX = Random.Range(-halfWidth + margin, halfWidth - margin);
                float posZ = Random.Range(-halfDepth + margin, halfDepth - margin);
                
                // Position person ON TOP of the floor (+1 for height)
                Vector3 personPos = new Vector3(posX, floor * floorHeight + 1f, posZ);
                
                GameObject personObj = Instantiate(personPrefab, personPos, Quaternion.identity);
                personObj.name = $"Person_Floor{floor}_{i}";
                personObj.transform.parent = floors[floor].transform;
                
                EnhancedPerson personComp = personObj.GetComponent<EnhancedPerson>();
                if (personComp == null)
                {
                    personComp = personObj.AddComponent<EnhancedPerson>();
                }
                
                // Configure person
                personComp.currentFloor = floor;
                personComp.homeFloor = floor;
                personComp.floorHeight = floorHeight;
                personComp.availableElevators = new List<Elevator>(elevators);
                personComp.buildingSize = floorSize;
                
                people.Add(personComp);
                totalPeople++;
            }
        }
        
        Debug.Log($"Generated {totalPeople} people across {numberOfFloors} floors.");
    }
    
    private void FinalizeSetup()
    {
        // Create an elevator controller if needed
        GameObject controllerObj = new GameObject("ElevatorController");
        controllerObj.transform.parent = transform;
        
        elevatorController = controllerObj.AddComponent<ElevatorController>();
        elevatorController.elevators = elevators.ToArray();
        
        // Double-check all connections
        foreach (var person in people)
        {
            if (person.availableElevators == null || person.availableElevators.Count == 0)
            {
                person.availableElevators = new List<Elevator>(elevators);
                Debug.LogWarning($"Fixed missing elevator reference for {person.name}");
            }
        }
    }

    // Draw gizmos to show building layout in editor
    private void OnDrawGizmos()
    {
        // Show building outline
        Gizmos.color = Color.yellow;
        Vector3 center = transform.position;
        Vector3 size = new Vector3(floorSize.x, numberOfFloors * floorHeight, floorSize.y);
        Gizmos.DrawWireCube(center + new Vector3(0, size.y/2, 0), size);
        
        // Show elevator positions
        Gizmos.color = Color.green;
        float halfWidth = floorSize.x / 2;
        float startZ = -(numberOfElevators - 1) * elevatorSpacing / 2;
        
        for (int i = 0; i < numberOfElevators; i++)
        {
            float elevatorZ = startZ + i * elevatorSpacing;
            Vector3 elevatorPos = new Vector3(-halfWidth - 1.5f, 0, elevatorZ);
            Gizmos.DrawCube(elevatorPos + new Vector3(0, 1.25f, 0), new Vector3(1.8f, 2.5f, 1.8f));
        }
    }
}

// Simple Elevator Controller - Manages multiple elevators
public class ElevatorController : MonoBehaviour
{
    public Elevator[] elevators;
    
    void Start()
    {
        // Log status of elevators
        if (elevators != null && elevators.Length > 0)
        {
            Debug.Log($"Elevator Controller initialized with {elevators.Length} elevators.");
            
            // Log positions of all elevators
            for (int i = 0; i < elevators.Length; i++)
            {
                if (elevators[i] != null)
                {
                    Debug.Log($"Elevator {i} position: {elevators[i].transform.position}");
                }
            }
        }
        else
        {
            Debug.LogWarning("Elevator Controller has no elevators assigned!");
        }
    }
    
    // Find the best elevator to serve a request
    public Elevator FindBestElevatorForRequest(int floor, int destinationFloor)
    {
        if (elevators == null || elevators.Length == 0)
            return null;
            
        // Simple algorithm: find the closest available elevator
        Elevator bestElevator = null;
        float bestScore = float.MaxValue;
        
        foreach (Elevator elevator in elevators)
        {
            if (elevator == null) continue;
            
            // Calculate score based on distance and number of pending requests
            float distanceScore = Mathf.Abs(elevator.CurrentFloor - floor);
            float pendingScore = elevator.RequestedFloorsCount * 2; // Pending requests are weighted higher
            
            float totalScore = distanceScore + pendingScore;
            
            if (totalScore < bestScore)
            {
                bestScore = totalScore;
                bestElevator = elevator;
            }
        }
        
        return bestElevator ?? elevators[0]; // Default to first elevator if none found
    }
}