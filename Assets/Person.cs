using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnhancedPerson : MonoBehaviour
{
    [Header("Person Settings")]
    public int currentFloor = 0;   // Current floor
    public int homeFloor = 0;      // Home/base floor
    public int targetFloor = 0;    // Desired floor
    public float weight = 70f;     // Weight in kg for elevator capacity
    public float moveSpeed = 2f;   // Movement speed
    public float floorHeight = 3f; // Height of each floor (should match building)
    
    [Header("Behavior Settings")]
    public float minWaitTime = 5f;   // Minimum time between actions
    public float maxWaitTime = 15f;  // Maximum time between actions
    
    [Header("Elevator Usage Probabilities")]
    [Range(0f, 1f)]
    public float baseElevatorUsageProbability = 0.1f; // Base chance to use elevator during normal hours
    [Range(0f, 1f)]
    public float peakHourMultiplier = 3.0f;           // Multiplier during peak hours (morning/evening rush)
    [Range(0f, 1f)]
    public float lunchTimeMultiplier = 2.0f;          // Multiplier during lunch time
    [Range(0f, 1f)]
    public float perFloorDistanceBonus = 0.1f;        // Additional probability per floor of distance
    [Range(0f, 1f)]
    public float maxElevatorUsageProbability = 0.9f;  // Cap on max probability
    
    [Header("Personal Traits")]
    [Range(0f, 1f)]
    public float elevatorPreference;  // Randomized per-person preference (0=prefers stairs, 1=prefers elevator)
    
    [Header("Navigation")]
    public Vector2 buildingSize = new Vector2(20f, 20f); // Building size for random movement
    public float wallDetectionDistance = 1.0f; // Distance to detect walls
    
    // References
    [HideInInspector]
    public List<Elevator> availableElevators = new List<Elevator>();
    private Elevator targetElevator;
    private Vector3 moveTarget;
    private bool hasTarget = false;
    private bool waitingForElevator = false;
    private bool isMovingToDestination = false;
    private float targetReachedThreshold = 0.1f;
    private float elevatorCheckDistance = 2.0f;
    
    // Schedule state
    private float nextActionTime;
    private ElevatorController elevatorController;
    
    // Component references
    private Renderer personRenderer;
    
    // Time reference
    private SimplifiedSimulationManager simulationManager;
    
    // Debug
    private bool debugIssuesLogged = false;
    
    // Start is called before the first frame update
    void Start()
    {
        // Initialize random elevator preference (simulating different personalities)
        elevatorPreference = Random.value;
        
        // Find simulation manager for time reference
        simulationManager = FindObjectOfType<SimplifiedSimulationManager>();
        
        InitializePerson();
    }
    
    private void InitializePerson()
    {
        // Initialize random visual appearance
        if (TryGetComponent<Renderer>(out personRenderer))
        {
            personRenderer.material.color = GetRandomPersonColor();
        }
        else
        {
            // Create a simple visual representation if none exists
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.transform.SetParent(transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            
            // Remove the collider from the visual (we'll use the parent's collider)
            Destroy(visual.GetComponent<Collider>());
            
            // Add material
            Renderer visualRenderer = visual.GetComponent<Renderer>();
            if (visualRenderer != null)
            {
                visualRenderer.material.color = GetRandomPersonColor();
                personRenderer = visualRenderer;
            }
        }
        
        // Add a capsule collider if missing
        if (!TryGetComponent<CapsuleCollider>(out _))
        {
            CapsuleCollider collider = gameObject.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.radius = 0.5f;
            collider.center = new Vector3(0, 0, 0);
        }
        
        // Find elevator controller if not set
        if (elevatorController == null)
        {
            elevatorController = FindObjectOfType<ElevatorController>();
        }
        
        // Check if we have elevators assigned
        if (availableElevators == null || availableElevators.Count == 0)
        {
            Elevator[] elevators = FindObjectsOfType<Elevator>();
            availableElevators = new List<Elevator>(elevators);
            
            if (availableElevators.Count == 0 && !debugIssuesLogged)
            {
                Debug.LogError($"Person {gameObject.name} couldn't find any elevators!");
                debugIssuesLogged = true;
            }
        }
        
        // Schedule next action
        ScheduleNextAction();
    }
    
    // Update is called once per frame
    void Update()
    {
        // Check if it's time for next action
        if (Time.time >= nextActionTime && !waitingForElevator && !isMovingToDestination)
        {
            DecideNextAction();
        }
        
        // Handle movement if we have a target
        if (hasTarget)
        {
            MoveTowardsTarget();
        }
        
        // Check if we need to request an elevator
        if (waitingForElevator && targetElevator == null)
        {
            RequestElevator();
        }
        
        // Check if our target elevator has arrived
        if (waitingForElevator && targetElevator != null)
        {
            if (targetElevator.CurrentFloor == currentFloor && targetElevator.DoorsOpen)
            {
                // Check distance
                float distance = Vector3.Distance(transform.position, targetElevator.transform.position);
                if (distance <= elevatorCheckDistance)
                {
                    EnterElevator();
                }
                else
                {
                    // If we're not close enough, move towards the elevator
                    moveTarget = targetElevator.transform.position;
                    hasTarget = true;
                }
            }
        }
    }
    
    // Decide what to do next
    private void DecideNextAction()
    {
        // If we're in an elevator, don't make decisions
        if (transform.parent != null && transform.parent.GetComponent<Elevator>() != null)
        {
            return;
        }
        
        // First, decide if we want to go to another floor
        if (ShouldChangeFloor())
        {
            // Choose a random floor that's not the current one
            int newFloor;
            do {
                newFloor = Random.Range(0, GetHighestFloor() + 1);
            } while (newFloor == currentFloor);
            
            // Next, decide if we'll use elevator based on various factors
            if (ShouldUseElevator(newFloor))
            {
                GoToFloor(newFloor);
                Debug.Log($"Person {gameObject.name} wants to go to floor {newFloor} using elevator");
            }
            else
            {
                // Simulate taking stairs (just teleport to the new floor)
                SimulateTakingStairs(newFloor);
                Debug.Log($"Person {gameObject.name} takes stairs to floor {newFloor}");
            }
        }
        else
        {
            // Move to a random spot on the current floor
            MoveToRandomSpot();
            Debug.Log($"Person {gameObject.name} is moving to a random location on floor {currentFloor}");
        }
        
        // Schedule next action
        ScheduleNextAction();
    }
    
    // Determine if the person should change floors based on time of day
    private bool ShouldChangeFloor()
    {
        // Base chance to change floors
        float baseChangeFloorProbability = 0.15f;  // 15% chance during normal hours
        
        // Get time of day if simulation manager exists
        if (simulationManager != null)
        {
            float timeOfDay = simulationManager.CurrentHour;
            
            // Morning rush (7-9 AM): People are going to their work floors
            if (timeOfDay >= 7f && timeOfDay <= 9f)
            {
                return Random.value < 0.4f;  // 40% chance
            }
            // Lunch time (11:30-1:30 PM): People moving for lunch
            else if (timeOfDay >= 11.5f && timeOfDay <= 13.5f)
            {
                return Random.value < 0.3f;  // 30% chance
            }
            // Evening rush (4-6 PM): People leaving work
            else if (timeOfDay >= 16f && timeOfDay <= 18f)
            {
                return Random.value < 0.4f;  // 40% chance
            }
        }
        
        // Default to base probability during normal hours
        return Random.value < baseChangeFloorProbability;
    }
    
    // Determine if the person should use the elevator based on various factors
    private bool ShouldUseElevator(int destinationFloor)
    {
        // Calculate floor distance
        int floorDistance = Mathf.Abs(destinationFloor - currentFloor);
        
        // Base probability plus distance factor
        float probability = baseElevatorUsageProbability + (floorDistance * perFloorDistanceBonus);
        
        // Adjust based on time of day if simulation manager exists
        if (simulationManager != null)
        {
            float timeOfDay = simulationManager.CurrentHour;
            
            // Morning rush (7-9 AM) or evening rush (4-6 PM)
            if ((timeOfDay >= 7f && timeOfDay <= 9f) || (timeOfDay >= 16f && timeOfDay <= 18f))
            {
                probability *= peakHourMultiplier;
            }
            // Lunch time (11:30-1:30 PM)
            else if (timeOfDay >= 11.5f && timeOfDay <= 13.5f)
            {
                probability *= lunchTimeMultiplier;
            }
        }
        
        // Adjust based on personal preference
        probability = probability * (0.5f + elevatorPreference * 0.5f);
        
        // Cap at maximum probability
        probability = Mathf.Min(probability, maxElevatorUsageProbability);
        
        // Always use elevator for large distances regardless of other factors
        if (floorDistance >= 3)
        {
            probability = Mathf.Max(probability, 0.8f);
        }
        
        // Extremely close floors (1 floor difference) - many people would just take stairs
        if (floorDistance == 1 && elevatorPreference < 0.7f)
        {
            probability *= 0.5f;
        }
        
        return Random.value < probability;
    }
    
    // Simulate taking stairs to another floor
    private void SimulateTakingStairs(int targetFloor)
    {
        // Calculate time it would take to use stairs (rough estimate)
        int floorDistance = Mathf.Abs(targetFloor - currentFloor);
        float stairsTravelTime = floorDistance * 3f; // Approx 3 seconds per floor
        
        StartCoroutine(StairsTransition(targetFloor, stairsTravelTime));
    }
    
    // Coroutine to handle stairs transition with a delay
    private IEnumerator StairsTransition(int targetFloor, float travelTime)
    {
        isMovingToDestination = true;
        
        // First move to a position that would represent stairs
        float halfWidth = buildingSize.x / 2;
        Vector3 stairsPosition = new Vector3(-halfWidth + 1.5f, currentFloor * floorHeight + 1f, -buildingSize.y/4);
        
        moveTarget = stairsPosition;
        hasTarget = true;
        
        // Wait until we reach the stairs position
        while (Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z), 
            new Vector2(moveTarget.x, moveTarget.z)) > targetReachedThreshold)
        {
            yield return null;
        }
        
        // Now simulate going up/down stairs
        yield return new WaitForSeconds(travelTime);
        
        // Move to new floor
        transform.position = new Vector3(
            -halfWidth + 1.5f, 
            targetFloor * floorHeight + 1f, 
            -buildingSize.y/4
        );
        
        // Update floor
        currentFloor = targetFloor;
        
        // Finally, move to a random position on the new floor
        hasTarget = false;
        isMovingToDestination = false;
        MoveToRandomSpot();
    }
    
    // Go to a specific floor using elevator
    public void GoToFloor(int floor)
    {
        if (floor == currentFloor)
        {
            Debug.LogWarning($"Person {gameObject.name} attempted to go to the same floor they're already on ({floor})");
            return;
        }
        
        targetFloor = floor;
        
        // Find elevator to use
        if (elevatorController != null)
        {
            // Use the elevator controller to find the best elevator
            targetElevator = elevatorController.FindBestElevatorForRequest(currentFloor, targetFloor);
        }
        else if (availableElevators.Count > 0)
        {
            // Find closest elevator
            float closest = float.MaxValue;
            foreach (Elevator elevator in availableElevators)
            {
                if (elevator == null) continue;
                
                float distance = Vector3.Distance(transform.position, elevator.transform.position);
                if (distance < closest)
                {
                    closest = distance;
                    targetElevator = elevator;
                }
            }
        }
        
        if (targetElevator != null)
        {
            // Set target position near the elevator
            moveTarget = new Vector3(
                targetElevator.transform.position.x + 1.0f, // Stand slightly to the side
                currentFloor * floorHeight + 1f, // Add 1 to be above floor
                targetElevator.transform.position.z + Random.Range(-0.5f, 0.5f)
            );
            
            hasTarget = true;
            isMovingToDestination = true;
            waitingForElevator = true;
            
            // Request the elevator to come to our floor
            targetElevator.CallToFloor(currentFloor);
            Debug.Log($"Person {gameObject.name} called elevator {targetElevator.name} to floor {currentFloor}");
        }
        else
        {
            Debug.LogWarning($"Person {gameObject.name} couldn't find an elevator!");
            ScheduleNextAction(); // Try again later
        }
    }
    
    // Request an elevator
    private void RequestElevator()
    {
        if (elevatorController != null)
        {
            targetElevator = elevatorController.FindBestElevatorForRequest(currentFloor, targetFloor);
        }
        else if (availableElevators.Count > 0)
        {
            // Find closest elevator
            float closest = float.MaxValue;
            foreach (Elevator elevator in availableElevators)
            {
                if (elevator == null) continue;
                
                float distance = Vector3.Distance(transform.position, elevator.transform.position);
                if (distance < closest)
                {
                    closest = distance;
                    targetElevator = elevator;
                }
            }
        }
        
        if (targetElevator != null)
        {
            targetElevator.CallToFloor(currentFloor);
            Debug.Log($"Person {gameObject.name} called elevator {targetElevator.name} to floor {currentFloor}");
        }
        else
        {
            Debug.LogWarning($"Person {gameObject.name} couldn't find an elevator to call!");
        }
    }
    
    // Enter the elevator
    private void EnterElevator()
    {
        if (targetElevator == null)
        {
            Debug.LogError($"Person {gameObject.name} tried to enter a null elevator!");
            waitingForElevator = false;
            ScheduleNextAction();
            return;
        }
        
        float distance = Vector3.Distance(transform.position, targetElevator.transform.position);
        Debug.Log($"Person {gameObject.name} attempting to enter elevator {targetElevator.name} at distance {distance}");
        
        // Check if we can enter
        if (targetElevator.CanEnter(weight))
        {
            targetElevator.AddPassenger(gameObject);
            targetElevator.RequestFloor(targetFloor);
            
            waitingForElevator = false;
            isMovingToDestination = false;
            hasTarget = false;
            
            Debug.Log($"Person {gameObject.name} entered elevator {targetElevator.name} heading to floor {targetFloor}");
        }
        else
        {
            Debug.Log($"Person {gameObject.name} couldn't enter elevator {targetElevator.name}. Will try again.");
        }
    }
    
    // Exit the elevator
    public void ExitElevator()
    {
        if (transform.parent == null || transform.parent.GetComponent<Elevator>() == null)
        {
            Debug.LogWarning($"Person {gameObject.name} tried to exit elevator but isn't in one!");
            return;
        }
        
        Elevator currentElevator = transform.parent.GetComponent<Elevator>();
        
        // Remember elevator position before unparenting
        Vector3 elevatorPos = currentElevator.transform.position;
        
        // Unparent from elevator
        transform.parent = null;
        
        // Move away from elevator to avoid blocking
        Vector3 exitDir = new Vector3(Random.Range(1.5f, 2.5f), 0, Random.Range(-1.5f, 1.5f));
        Vector3 exitPos = elevatorPos + exitDir;
        exitPos.y = targetFloor * floorHeight + 1f; // Make sure y position is correct
        
        transform.position = exitPos;
        currentFloor = targetFloor;
        targetElevator = null;
        
        Debug.Log($"Person {gameObject.name} exited at floor {currentFloor}");
        
        // Schedule next action after a brief delay
        nextActionTime = Time.time + Random.Range(2f, 5f);
    }
    
    // Move to a random position on the current floor
    private void MoveToRandomSpot()
    {
        float halfWidth = buildingSize.x / 2 - 2f; // Keep away from walls
        float halfDepth = buildingSize.y / 2 - 2f;
        
        // Generate a random position in the level
        Vector3 randomPos = new Vector3(
            Random.Range(-halfWidth, halfWidth),
            currentFloor * floorHeight + 1f, // Add 1 to be above floor
            Random.Range(-halfDepth, halfDepth)
        );
        
        moveTarget = randomPos;
        hasTarget = true;
        isMovingToDestination = true;
    }
    
    // Move towards the current target
    private void MoveTowardsTarget()
    {
        // Calculate direction to target
        Vector3 moveDirection = (moveTarget - transform.position).normalized;
        
        // Cast ray forward to check for obstacles
        RaycastHit hit;
        bool hitObstacle = Physics.Raycast(transform.position, moveDirection, out hit, wallDetectionDistance);
        
        if (hitObstacle)
        {
            if (hit.collider.CompareTag("Wall") || hit.collider.name.Contains("Wall"))
            {
                // Debug wall hit
                Debug.DrawLine(transform.position, hit.point, Color.red, 0.5f);
                
                // Calculate avoidance direction
                Vector3 avoidDirection = Vector3.Cross(Vector3.up, moveDirection).normalized;
                
                // Try left or right randomly
                if (Random.value < 0.5f)
                {
                    avoidDirection = -avoidDirection;
                }
                
                // Modify move direction to avoid obstacle
                moveDirection = (moveDirection + avoidDirection * 1.5f).normalized;
            }
        }
        
        // Move towards target with the potentially modified direction
        Vector3 newPosition = transform.position + moveDirection * moveSpeed * Time.deltaTime;
        
        // Ensure y position is maintained (don't fall through floors)
        newPosition.y = currentFloor * floorHeight + 1f;
        
        transform.position = newPosition;
        
        // Rotate to face movement direction
        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10 * Time.deltaTime);
        }
        
        // Check if we've reached the target
        if (Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(moveTarget.x, moveTarget.z)) < targetReachedThreshold)
        {
            if (isMovingToDestination && !waitingForElevator)
            {
                isMovingToDestination = false;
                hasTarget = false;
                Debug.Log($"Person {gameObject.name} reached random destination on floor {currentFloor}");
            }
            else if (waitingForElevator)
            {
                isMovingToDestination = false;
                Debug.Log($"Person {gameObject.name} reached elevator waiting area on floor {currentFloor}");
                // Keep hasTarget true to stay close to the elevator
            }
        }
    }
    
    // Schedule the next action
    private void ScheduleNextAction()
    {
        // Base wait time
        float waitTime = Random.Range(minWaitTime, maxWaitTime);
        
        // Adjust wait time based on time of day if simulation manager exists
        if (simulationManager != null)
        {
            float timeOfDay = simulationManager.CurrentHour;
            
            // During peak hours, people make decisions more frequently
            if ((timeOfDay >= 7f && timeOfDay <= 9f) || 
                (timeOfDay >= 11.5f && timeOfDay <= 13.5f) || 
                (timeOfDay >= 16f && timeOfDay <= 18f))
            {
                waitTime *= 0.7f; // 30% shorter wait times during peak hours
            }
            else if (timeOfDay >= 22f || timeOfDay <= 6f)
            {
                waitTime *= 1.5f; // Longer wait times during night hours (less activity)
            }
        }
        
        nextActionTime = Time.time + waitTime;
    }
    
    // Helper to get the highest floor in the building
    private int GetHighestFloor()
    {
        if (availableElevators.Count > 0)
        {
            int highest = 0;
            foreach (Elevator elevator in availableElevators)
            {
                if (elevator != null && elevator.topFloor > highest)
                {
                    highest = elevator.topFloor;
                }
            }
            return highest;
        }
        return 5; // Default if no elevators found
    }
    
    // Get a random color for the person
    private Color GetRandomPersonColor()
    {
        // Generate pastel-ish colors for people
        return new Color(
            Random.Range(0.5f, 1.0f),
            Random.Range(0.5f, 1.0f),
            Random.Range(0.5f, 1.0f)
        );
    }
    
    // For debugging
    void OnDrawGizmos()
    {
        // Draw ray showing where person is trying to go
        if (hasTarget)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, moveTarget);
        }
        
        // Draw sphere showing wall detection distance
        if (waitingForElevator)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, wallDetectionDistance);
            
            // Draw line to target elevator if we have one
            if (targetElevator != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, targetElevator.transform.position);
            }
        }
    }
}