using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Added for .Any()

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
        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) // + key
        {
            wallTransparency = Mathf.Clamp01(wallTransparency + 0.1f);
            UpdateWallVisibility();
            Debug.Log("Wall transparency: " + wallTransparency);
        }

        // Decrease transparency with - key
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) // - key
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
                    if (wallTransparency < 1.0f && showWalls) // Only set transparent mode if actually transparent and shown
                    {
                        mat.SetFloat("_Mode", 3); // Transparent mode
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.EnableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = 3000; // Transparent queue
                    }
                    else // Opaque
                    {
                        mat.SetFloat("_Mode", 0); // Opaque mode
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        mat.SetInt("_ZWrite", 1);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.DisableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = -1; // Default queue
                    }


                    // Apply the updated material
                    renderer.material = mat;
                    renderer.enabled = showWalls; // Also enable/disable renderer
                }
            }
        }
    }

    // ADDED: Test the elevators after initialization
    private IEnumerator TestElevatorsAfterInitialization()
    {
        yield return new WaitForSeconds(2.0f);

        if (elevators.Count > 0 && elevators[0] != null)
        {
            Debug.Log("Testing elevator movement after initialization");
            elevators[0].RequestFloor(1); // Request floor 1 if available
        }
    }

    // Clean up any existing objects that might conflict
    private void CleanExistingObjects()
    {
        // Find and destroy any existing elevators in the scene (not managed by us)
        Elevator[] existingElevators = FindObjectsOfType<Elevator>();
        foreach (Elevator elevator in existingElevators)
        {
            if (!elevators.Contains(elevator)) // If this elevator is not in our managed list
            {
                Debug.Log($"Destroying unmanaged elevator: {elevator.name}");
                Destroy(elevator.gameObject);
            }
        }

        // Find and destroy any existing elevator controllers
        ElevatorController[] existingControllers = FindObjectsOfType<ElevatorController>();
        foreach (ElevatorController controller in existingControllers)
        {
            if (controller != elevatorController) // If this controller is not our managed one
            {
                Debug.Log($"Destroying unmanaged elevator controller: {controller.name}");
                Destroy(controller.gameObject);
            }
        }

        // Find and destroy any existing walls that were previously managed by this script
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
        // Destroy all existing floors, elevators, and people managed by this generator
        foreach (var floor in floors)
        {
            if (floor != null) Destroy(floor);
        }
        floors.Clear();

        foreach (var elevator in elevators)
        {
            if (elevator != null) Destroy(elevator.gameObject);
        }
        elevators.Clear();

        foreach (var person in people)
        {
            if (person != null) Destroy(person.gameObject);
        }
        people.Clear();

        foreach (var wall in walls)
        {
            if (wall != null) Destroy(wall);
        }
        walls.Clear();

        // Also destroy elevator controller if it exists and was created by this script
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
            Floor floorInstance = Instantiate(floorPrefab, new Vector3(0, i * floorHeight, 0), Quaternion.identity);
            floorInstance.transform.SetParent(transform); // Set parent to this object
            floorInstance.name = $"Floor_{i}";

            floorInstance.SetFloorSize(floorSize);

            // Make floor semi-transparent to see through
            Renderer floorRenderer = floorInstance.GetComponent<Renderer>();
            if (floorRenderer != null)
            {
                // Use a slightly transparent material for the floor
                Material floorMat = floorRenderer.material; // Operate on instance of material
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
                floorMat.renderQueue = 3000; // Transparent queue

                // floorRenderer.material = floorMat; // Not needed if operating on renderer.material directly
            }

            floors.Add(floorInstance.gameObject);

            // Add simple walls for boundaries
            CreateWallsForFloor(floorInstance.gameObject, i);
        }

        Debug.Log($"Generated {numberOfFloors} floors.");
    }

    private void CreateWallsForFloor(GameObject floorObj, int floorNumber) // Renamed for clarity
    {
        // Create four walls around the edge of the floor
        float wallHeight = floorHeight * 0.9f; // Slightly shorter than floor height
        float halfWidth = floorSize.x / 2;
        float halfDepth = floorSize.y / 2;
        float wallThickness = 0.3f;
        float yPos = floorObj.transform.position.y + wallHeight / 2; // Position walls relative to floor's y

        // North wall
        CreateWall(new Vector3(floorObj.transform.position.x, yPos, floorObj.transform.position.z + halfDepth - wallThickness / 2),
            new Vector3(floorSize.x, wallHeight, wallThickness), $"Wall_North_F{floorNumber}");

        // South wall
        CreateWall(new Vector3(floorObj.transform.position.x, yPos, floorObj.transform.position.z -halfDepth + wallThickness / 2),
            new Vector3(floorSize.x, wallHeight, wallThickness), $"Wall_South_F{floorNumber}");

        // East wall
        CreateWall(new Vector3(floorObj.transform.position.x + halfWidth - wallThickness / 2, yPos, floorObj.transform.position.z),
            new Vector3(wallThickness, wallHeight, floorSize.y), $"Wall_East_F{floorNumber}");

        // West wall (with potential gap for elevators - though elevators are placed outside)
        // For simplicity, creating a full West wall. Elevator placement is external.
        CreateWall(new Vector3(floorObj.transform.position.x -halfWidth + wallThickness / 2, yPos, floorObj.transform.position.z),
            new Vector3(wallThickness, wallHeight, floorSize.y), $"Wall_West_F{floorNumber}");
    }

    private void CreateWall(Vector3 worldPosition, Vector3 size, string wallName)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = worldPosition; // Use world position
        wall.transform.localScale = size;
        wall.transform.SetParent(transform); // Parent to BuildingGenerator for organization
        wall.name = wallName;

        // Set wall layer for collision detection if needed (e.g., "Wall" layer)
        // wall.layer = LayerMask.NameToLayer("Wall"); 

        walls.Add(wall); // Add to walls list for easy management (transparency, etc.)

        // Material setup will be handled by UpdateWallVisibility
    }

    private void GenerateElevators()
    {
        if (elevatorPrefab == null)
        {
            Debug.LogError("Elevator prefab is not assigned!");
            return;
        }

        float totalElevatorSpan = (numberOfElevators - 1) * elevatorSpacing;
        float startZ = -totalElevatorSpan / 2;

        for (int i = 0; i < numberOfElevators; i++)
        {
            float elevatorX = -floorSize.x / 2 - 1.5f; // Position elevators to the "west" of the building
            float elevatorZ = startZ + i * elevatorSpacing;
            // Elevators start at Y=0 (ground floor), their internal logic handles floor heights
            Vector3 elevatorPos = new Vector3(elevatorX, 0.05f , elevatorZ); // Slightly above ground

            GameObject elevatorObj = Instantiate(elevatorPrefab, elevatorPos, Quaternion.identity);
            elevatorObj.name = $"Elevator_{i}";
            elevatorObj.transform.SetParent(transform); // Parent to BuildingGenerator

            Elevator elevatorComp = elevatorObj.GetComponent<Elevator>();
            if (elevatorComp == null)
            {
                Debug.LogWarning($"Elevator prefab for {elevatorObj.name} is missing Elevator script. Adding one.");
                elevatorComp = elevatorObj.AddComponent<Elevator>();
            }

            elevatorComp.floorHeight = floorHeight;
            elevatorComp.topFloor = numberOfFloors - 1;
            // elevatorComp.InitializeElevator(); // Ensure it's initialized if Start() hasn't run

            elevators.Add(elevatorComp);
        }
        Debug.Log($"Generated {numberOfElevators} elevators.");
    }

    private void GeneratePeople()
    {
        if (personPrefab == null)
        {
            Debug.LogError("Person prefab is not assigned!");
            return;
        }

        int totalPeople = 0;

        for (int floorIdx = 0; floorIdx < numberOfFloors; floorIdx++)
        {
            int peopleCount = Random.Range(minPeoplePerFloor, maxPeoplePerFloor + 1);
            GameObject currentFloorObj = floors[floorIdx]; // Get the actual floor GameObject

            for (int i = 0; i < peopleCount; i++)
            {
                float halfWidth = floorSize.x / 2;
                float halfDepth = floorSize.y / 2;
                float margin = 1.0f; // Keep away from edges

                float posX = Random.Range(-halfWidth + margin, halfWidth - margin);
                float posZ = Random.Range(-halfDepth + margin, halfDepth - margin);
                // Person's Y position should be relative to their current floor's actual Y + half their height
                float personY = currentFloorObj.transform.position.y + (personPrefab.transform.localScale.y / 2f);


                Vector3 personPos = new Vector3(posX, personY, posZ);
                // Instantiate person at world position, then parent if desired (or not, if they move freely)
                EnhancedPerson personObj = Instantiate(personPrefab, personPos, Quaternion.identity);
                personObj.name = $"Person_F{floorIdx}_{i}";
                // Optional: Parent to a general "People" container GameObject instead of the floor itself
                // personObj.transform.SetParent(this.transform); // Or a dedicated people container

                personObj.currentFloor = floorIdx;
                personObj.homeFloor = floorIdx;
                personObj.floorHeight = floorHeight; // Pass from generator
                personObj.availableElevators = new List<Elevator>(elevators); // Give list of all elevators
                personObj.buildingSize = floorSize; // Pass building/floor dimensions

                // Corrected lines:
                personObj.baseElevatorUsageProbability = 0.7f;
                personObj.minIdleTime = 5f; // Use minIdleTime
                personObj.maxIdleTime = 10f; // Use maxIdleTime

                people.Add(personObj);
                totalPeople++;
            }
        }
        Debug.Log($"Generated {totalPeople} people across {numberOfFloors} floors.");
    }

    private void FinalizeSetup()
    {
        // Create an elevator controller if needed, and assign elevators
        GameObject controllerObj = new GameObject("ElevatorController");
        controllerObj.transform.SetParent(transform); // Parent to BuildingGenerator

        elevatorController = controllerObj.AddComponent<ElevatorController>();
        elevatorController.elevators = elevators.ToArray(); // Assign the list of created elevators

        // Ensure all people have references to the elevators and the controller
        foreach (EnhancedPerson person in people)
        {
            if (person.availableElevators == null || !person.availableElevators.Any())
            {
                person.availableElevators = new List<Elevator>(elevators);
            }
            // The EnhancedPerson script should find the ElevatorController itself in Start()
        }

        // Start coroutine to make a few people use elevators for testing
        StartCoroutine(ForcePeopleToUseElevatorsAfterDelay());
    }

    private IEnumerator ForcePeopleToUseElevatorsAfterDelay()
    {
        yield return new WaitForSeconds(3.0f); // Wait for systems to initialize

        if (people.Count > 0 && numberOfFloors > 1)
        {
            int usageCount = Mathf.Min(3, people.Count); // Force up to 3 people
            for (int i = 0; i < usageCount; i++)
            {
                EnhancedPerson person = people[Random.Range(0, people.Count)]; // Pick a random person
                if (person != null)
                {
                    int currentPFloor = person.currentFloor;
                    int targetPFloor;
                    do
                    {
                        targetPFloor = Random.Range(0, numberOfFloors);
                    } while (targetPFloor == currentPFloor); // Ensure different target floor

                    Debug.Log($"[Generator] Forcing person {person.name} on F{currentPFloor} to request elevator to F{targetPFloor}");
                    person.GoToFloor(targetPFloor); // Use the public method
                    yield return new WaitForSeconds(Random.Range(0.5f, 1.5f)); // Stagger requests
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        // Show building outline (approximated)
        Gizmos.color = Color.gray;
        Vector3 buildingCenter = transform.position + new Vector3(0, (numberOfFloors * floorHeight) / 2f - floorHeight/2f, 0);
        Vector3 buildingSizeGizmo = new Vector3(floorSize.x, numberOfFloors * floorHeight, floorSize.y);
        Gizmos.DrawWireCube(buildingCenter, buildingSizeGizmo);

        // Show elevator shaft guides (approximate positions)
        if (elevators.Any()) // Check if elevators list has been populated
        {
            Gizmos.color = Color.cyan;
            foreach(Elevator el in elevators)
            {
                if (el == null) continue; // Skip if elevator was destroyed or not yet fully init
                 Vector3 shaftBottom = new Vector3(el.transform.position.x, 0, el.transform.position.z);
                 Vector3 shaftTop = new Vector3(el.transform.position.x, (numberOfFloors -1) * floorHeight, el.transform.position.z);
                 Gizmos.DrawLine(shaftBottom, shaftTop);
                 Gizmos.DrawWireCube(el.transform.position, el.transform.localScale); // Show current elevator position
            }
        }


        // Show floor planes
        for (int i = 0; i < numberOfFloors; i++)
        {
            float y = i * floorHeight;
            Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 0.3f); // Semi-transparent gray
            Gizmos.DrawCube(new Vector3(transform.position.x, y, transform.position.z), new Vector3(floorSize.x, 0.1f, floorSize.y));
        }
    }
}

// Simple Elevator Controller - Manages multiple elevators
public class ElevatorController : MonoBehaviour
{
    public Elevator[] elevators; // Populated by BuildingGenerator

    void Start()
    {
        if (elevators != null && elevators.Length > 0)
        {
            Debug.Log($"Elevator Controller initialized with {elevators.Length} elevators.");
            for (int i = 0; i < elevators.Length; i++)
            {
                if (elevators[i] != null)
                {
                    Debug.Log($"Elevator {i} ({elevators[i].name}) initial position: {elevators[i].transform.position}, CurrentFloor: {elevators[i].CurrentFloor}");
                } else {
                    Debug.LogWarning($"Elevator Controller has a null elevator at index {i}.");
                }
            }
            // StartCoroutine(TestAllElevators()); // Optional: test elevators from controller
        }
        else
        {
            Debug.LogWarning("Elevator Controller has no elevators assigned or the array is empty!");
        }
    }

    private IEnumerator TestAllElevators()
    {
        yield return new WaitForSeconds(1.5f); // Wait a bit longer
        if (elevators != null && elevators.Length > 0)
        {
            Debug.Log("[Controller] Testing all elevators...");
            foreach (Elevator el in elevators)
            {
                if (el != null && el.topFloor > 0) // Ensure elevator exists and there's more than one floor
                {
                    int targetFloor = (el.CurrentFloor == 0) ? 1 : 0;
                    if (targetFloor > el.topFloor) targetFloor = el.topFloor;

                    Debug.Log($"[Controller] Requesting elevator {el.name} from F{el.CurrentFloor} to F{targetFloor}");
                    el.RequestFloor(targetFloor);
                    yield return new WaitForSeconds(Random.Range(0.5f, 1.0f)); // Stagger requests
                }
            }
        }
    }

    public Elevator FindBestElevatorForRequest(int requestingFloor, int destinationFloor)
    {
        if (elevators == null || elevators.Length == 0)
        {
            Debug.LogWarning("ElevatorController: No elevators available to find best request.");
            return null;
        }

        Elevator bestElevator = null;
        float bestScore = float.MaxValue;

        foreach (Elevator elevator in elevators)
        {
            if (elevator == null) continue;

            float score = 0;
            bool isMovingTowardsRequestingFloor = false;

            // Factor 1: Distance to the requesting floor
            float distanceToRequestingFloor = Mathf.Abs(elevator.CurrentFloor - requestingFloor);
            score += distanceToRequestingFloor * 1.0f; // Weight distance

            // Factor 2: Is elevator moving and in which direction?
            if (elevator.IsMoving)
            {
                bool movingUp = elevator.Status == Elevator.ElevatorStatus.MovingUp;
                bool requestIsAbove = requestingFloor > elevator.CurrentFloor;
                bool requestIsBelow = requestingFloor < elevator.CurrentFloor;

                if (movingUp && requestIsAbove) // Moving up towards the requesting floor
                {
                    score -= 2.0f; // Bonus: good direction
                    isMovingTowardsRequestingFloor = true;
                }
                else if (!movingUp && requestIsBelow) // Moving down towards the requesting floor
                {
                    score -= 2.0f; // Bonus: good direction
                    isMovingTowardsRequestingFloor = true;
                }
                else // Moving away or in wrong direction for an immediate pickup
                {
                    score += 3.0f; // Penalty
                }
            }
            else if (elevator.Status == Elevator.ElevatorStatus.Idle) // Idle elevators are good candidates
            {
                score -= 1.0f; // Bonus for being idle
            }


            // Factor 3: Number of pending requests (load)
            score += elevator.RequestedFloorsCount * 1.5f; // Higher load is less desirable

            // Factor 4: Capacity (simplified - just check if full)
            // A more complex scoring could consider currentWeight vs maxWeight
            if (elevator.CurrentWeight >= elevator.maxWeight * 0.9f ) // Nearly full or full
            {
                score += 5.0f; // Penalty if nearly full
            }

            // Factor 5: Is the elevator already stopping at the requesting floor?
            // (This is implicitly handled if it's already requested and moving towards it)

            Debug.Log($"Elevator {elevator.name}: Score {score} (Dist: {distanceToRequestingFloor}, MovingTowards: {isMovingTowardsRequestingFloor}, Pending: {elevator.RequestedFloorsCount})");

            if (score < bestScore)
            {
                bestScore = score;
                bestElevator = elevator;
            }
        }
        
        if (bestElevator == null && elevators.Any(e => e != null)) // Fallback if no "best" found but some exist
        {
            bestElevator = elevators.FirstOrDefault(e => e != null && e.Status != Elevator.ElevatorStatus.OutOfService);
        }


        Debug.Log($"Best elevator for F{requestingFloor} -> F{destinationFloor} is {bestElevator?.name} with score {bestScore}");
        return bestElevator;
    }
}

