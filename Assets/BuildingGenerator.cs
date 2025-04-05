using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnhancedBuildingGenerator : MonoBehaviour
{
    [Header("Building Settings")]

    [SerializeField]
    private Floor floorPrefab;
    [SerializeField]
    private GameObject elevatorPrefab;
    [SerializeField]
    private EnhancedPerson personPrefab;

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

    [Header("Visualization Settings")]
    [Range(0f, 1f)]
    public float wallTransparency = 0.3f; // How transparent the walls should be
    public bool showWalls = true; // Toggle wall visibility
    public Color wallColor = new Color(0.8f, 0.8f, 0.9f); // Light blue walls

    // References to generated objects
    private List<GameObject> floors = new List<GameObject>();
    private List<Elevator> elevators = new List<Elevator>();
    private List<EnhancedPerson> people = new List<EnhancedPerson>();
    private List<GameObject> walls = new List<GameObject>();

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

            // ADDED: Force an elevator to move after setup
            StartCoroutine(TestElevatorsAfterInitialization());

            // Set wall transparency according to settings
            UpdateWallVisibility();
        }
    }

    // Update method to handle user input for wall visibility
    void Update()
    {
        // Toggle wall visibility when W key is pressed
        if (Input.GetKeyDown(KeyCode.W))
        {
            showWalls = !showWalls;
            UpdateWallVisibility();
            Debug.Log("Wall visibility toggled: " + showWalls);
        }

        // Increase transparency with + key
        if (Input.GetKeyDown(KeyCode.Equals)) // + key
        {
            wallTransparency = Mathf.Clamp01(wallTransparency + 0.1f);
            UpdateWallVisibility();
            Debug.Log("Wall transparency: " + wallTransparency);
        }

        // Decrease transparency with - key
        if (Input.GetKeyDown(KeyCode.Minus)) // - key
        {
            wallTransparency = Mathf.Clamp01(wallTransparency - 0.1f);
            UpdateWallVisibility();
            Debug.Log("Wall transparency: " + wallTransparency);
        }
    }

    // Update wall visibility based on settings
    private void UpdateWallVisibility()
    {
        foreach (GameObject wall in walls)
        {
            if (wall != null)
            {
                Renderer renderer = wall.GetComponent<Renderer>();
                if (renderer != null)
                {
                    // Set material's transparency based on settings
                    Color color = wallColor;
                    color.a = showWalls ? wallTransparency : 0f;

                    // Create or modify material to be transparent
                    Material mat = renderer.material;
                    mat.color = color;

                    // Set the appropriate rendering mode based on transparency
                    if (wallTransparency < 1.0f)
                    {
                        mat.SetFloat("_Mode", 3); // Transparent mode
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.EnableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = 3000;
                    }

                    // Apply the updated material
                    renderer.material = mat;
                }
            }
        }
    }

    // ADDED: Test the elevators after initialization
    private IEnumerator TestElevatorsAfterInitialization()
    {
        yield return new WaitForSeconds(2.0f);

        if (elevators.Count > 0)
        {
            Debug.Log("Testing elevator movement after initialization");
            elevators[0].RequestFloor(1);
        }

        // TODO: Example of using enumuration.
        /*
        IEnumerable<EnhancedPerson> personEnumerator = elevators[0].GetPersonBelowWeight(80.0f);
        foreach (EnhancedPerson person in personEnumerator)
        {
            if (person != null)
            {
                Debug.Log($"Person {person.name} is weight {person.weight}");
            }
        }
        */
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

        // Find and destroy any existing walls
        foreach (GameObject wall in walls)
        {
            if (wall != null)
            {
                Destroy(wall);
            }
        }
        walls.Clear();
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

        foreach (var wall in walls)
        {
            if (wall != null) Destroy(wall);
        }

        floors.Clear();
        elevators.Clear();
        people.Clear();
        walls.Clear();

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
            Floor floor = Instantiate(floorPrefab, new Vector3(0, i * floorHeight, 0), Quaternion.identity);
            floor.transform.SetParent(transform); // Set parent to this object
            floor.name = $"Floor_{i}";

            // TODO: Scale floor to desired size
            floor.SetFloorSize(floorSize);

            // Make floor semi-transparent to see through
            Renderer floorRenderer = floor.GetComponent<Renderer>();
            if (floorRenderer != null)
            {
                // Use a slightly transparent material for the floor
                Material floorMat = floorRenderer.material;
                Color floorColor = new Color(0.9f, 0.9f, 0.9f, 0.7f);
                floorMat.color = floorColor;

                // Set up transparent rendering
                floorMat.SetFloat("_Mode", 3); // Transparent mode
                floorMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                floorMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                floorMat.SetInt("_ZWrite", 0);
                floorMat.DisableKeyword("_ALPHATEST_ON");
                floorMat.EnableKeyword("_ALPHABLEND_ON");
                floorMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                floorMat.renderQueue = 3000;

                floorRenderer.material = floorMat;
            }

            floors.Add(floor.gameObject);

            // Add simple walls for boundaries
            CreateWalls(floor.gameObject, i);
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
        CreateWall(floor, new Vector3(0, wallHeight / 2, halfDepth - wallThickness / 2),
            new Vector3(floorSize.x, wallHeight, wallThickness));

        // South wall
        CreateWall(floor, new Vector3(0, wallHeight / 2, -halfDepth + wallThickness / 2),
            new Vector3(floorSize.x, wallHeight, wallThickness));

        // East wall
        CreateWall(floor, new Vector3(halfWidth - wallThickness / 2, wallHeight / 2, 0),
            new Vector3(wallThickness, wallHeight, floorSize.y));

        // West wall with gap for elevator
        float doorWidth = 3.0f * numberOfElevators; // Make door wider if more elevators
        float leftWallWidth = (floorSize.y - doorWidth) / 2;
        float rightWallWidth = (floorSize.y - doorWidth) / 2;

        // Left portion of west wall
        if (leftWallWidth > 0)
        {
            CreateWall(floor, new Vector3(-halfWidth + wallThickness / 2, wallHeight / 2, -halfDepth + leftWallWidth / 2),
                new Vector3(wallThickness, wallHeight, leftWallWidth));
        }

        // Right portion of west wall
        if (rightWallWidth > 0)
        {
            CreateWall(floor, new Vector3(-halfWidth + wallThickness / 2, wallHeight / 2, halfDepth - rightWallWidth / 2),
                new Vector3(wallThickness, wallHeight, rightWallWidth));
        }
    }

    private void CreateWall(GameObject parent, Vector3 localPosition, Vector3 size)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.localPosition = localPosition;
        wall.transform.localScale = size;
        wall.transform.SetParent(transform);
        wall.name = "Wall";

        // Set wall layer for collision detection
        wall.layer = LayerMask.NameToLayer("Default");

        // Add to walls list for easy management
        walls.Add(wall);

        // Set up transparent material
        Renderer renderer = wall.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material wallMat = renderer.material;
            Color transparentColor = wallColor;
            transparentColor.a = wallTransparency;

            wallMat.color = transparentColor;

            // Set up transparency
            wallMat.SetFloat("_Mode", 3); // Transparent mode
            wallMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            wallMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            wallMat.SetInt("_ZWrite", 0);
            wallMat.DisableKeyword("_ALPHATEST_ON");
            wallMat.EnableKeyword("_ALPHABLEND_ON");
            wallMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            wallMat.renderQueue = 3000;

            renderer.material = wallMat;
        }
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

                EnhancedPerson personObj = Instantiate(personPrefab, personPos, Quaternion.identity);
                personObj.name = $"Person_Floor{floor}_{i}";
                personObj.transform.SetParent(floors[floor].transform);

                // Configure person
                personObj.currentFloor = floor;
                personObj.homeFloor = floor;
                personObj.floorHeight = floorHeight;
                personObj.availableElevators = new List<Elevator>(elevators);
                personObj.buildingSize = floorSize;

                // ADDED: Make people more likely to use elevators
                personObj.baseElevatorUsageProbability = 0.7f;
                personObj.minWaitTime = 5f; // Make them more active
                personObj.maxWaitTime = 10f;

                people.Add(personObj);
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

        // ADDED: Make a few people immediately use elevators
        StartCoroutine(ForcePeopleToUseElevators());
    }

    // ADDED: Force some people to use elevators right away
    private IEnumerator ForcePeopleToUseElevators()
    {
        // Wait a bit for the simulation to settle
        yield return new WaitForSeconds(3.0f);

        // Make 3 random people use elevators
        int usageCount = Mathf.Min(3, people.Count);
        for (int i = 0; i < usageCount; i++)
        {
            if (i < people.Count)
            {
                EnhancedPerson person = people[i];
                int currentFloor = person.currentFloor;
                int targetFloor = (currentFloor + 1) % numberOfFloors;

                Debug.Log($"Forcing person {person.name} to request elevator to floor {targetFloor}");
                person.GoToFloor(targetFloor);

                yield return new WaitForSeconds(0.5f);
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
        Gizmos.DrawWireCube(center + new Vector3(0, size.y / 2, 0), size);

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

        // Show floor heights
        for (int i = 0; i < numberOfFloors; i++)
        {
            float y = i * floorHeight;
            Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);
            Gizmos.DrawWireCube(new Vector3(0, y, 0), new Vector3(floorSize.x, 0.1f, floorSize.y));
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

            // ADDED: Test elevators are responsive
            StartCoroutine(TestElevators());
        }
        else
        {
            Debug.LogWarning("Elevator Controller has no elevators assigned!");
        }
    }

    // ADDED: Test elevators after initialization
    private IEnumerator TestElevators()
    {
        yield return new WaitForSeconds(1.0f);

        if (elevators != null && elevators.Length > 0)
        {
            Debug.Log("Controller testing elevators...");
            for (int i = 0; i < elevators.Length; i++)
            {
                int targetFloor = elevators[i].CurrentFloor == 0 ? 1 : 0;
                Debug.Log($"Controller requesting elevator {i} to move to floor {targetFloor}");
                elevators[i].RequestFloor(targetFloor);
                yield return new WaitForSeconds(0.5f);
            }
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

        // IMPROVED: More robust handling of null elevators
        if (bestElevator == null && elevators.Length > 0)
        {
            for (int i = 0; i < elevators.Length; i++)
            {
                if (elevators[i] != null)
                {
                    bestElevator = elevators[i];
                    break;
                }
            }
        }

        return bestElevator; // May still be null if all elevators are null
    }
}