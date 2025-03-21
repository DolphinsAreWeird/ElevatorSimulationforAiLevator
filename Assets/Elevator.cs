using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Elevator : MonoBehaviour
{
    [Header("Elevator Settings")]
    public float floorHeight = 3f;         // Vertical distance between floors
    public float speed = 2f;               // Speed of elevator movement
    public int capacity = 6;               // Maximum number of passengers
    public float maxWeight = 500f;         // Maximum weight capacity in kg
    public float doorOpenTime = 3f;        // Time doors stay open in seconds
    public int topFloor = 10;              // Highest floor this elevator can reach

    [Header("Visual Components")]
    public GameObject elevatorCabin;       // The visual model of the elevator cabin
    public GameObject doorLeft;            // Left door visual
    public GameObject doorRight;           // Right door visual
    public float doorOpenDistance = 0.5f;  // How far doors open
    
    // Elevator state
    private int currentFloor = 0;           // Current floor the elevator is on
    private bool isMoving = false;          // Whether the elevator is currently moving
    private bool doorsOpen = false;         // Whether the doors are currently open
    private float currentWeight = 0f;       // Current weight of passengers
    private List<int> requestedFloors = new List<int>(); // List of floors to visit
    private Coroutine moveCoroutine;        // Reference to the movement coroutine
    private Coroutine doorCoroutine;        // Reference to the door animation coroutine

    // Status lights and indicators
    private ElevatorStatus elevatorStatus = ElevatorStatus.Idle;
    
    // Debug flag
    private bool isInitialized = false;
    
    // Public properties
    public int CurrentFloor => currentFloor;
    public bool IsMoving => isMoving;
    public bool DoorsOpen => doorsOpen;
    public float CurrentWeight => currentWeight;
    public float RemainingCapacity => maxWeight - currentWeight;
    public int RequestedFloorsCount => requestedFloors.Count;
    public ElevatorStatus Status => elevatorStatus;
    
    void Awake()
    {
        // Fix initial position during awake to ensure it's on top of the floor
        Vector3 position = transform.position;
        position.y = 0.05f; // Slightly above zero to avoid clipping
        transform.position = position;
        
        Debug.Log($"Elevator {gameObject.name} - Initial position set to: {transform.position}");
    }
    
    void Start()
    {
        InitializeElevator();
    }
    
    void InitializeElevator()
    {
        if (isInitialized) return;
        
        // Initialize elevator visuals
        if (elevatorCabin == null)
        {
            elevatorCabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            elevatorCabin.transform.parent = transform;
            elevatorCabin.transform.localPosition = Vector3.zero;
            elevatorCabin.transform.localScale = new Vector3(1.8f, 2.5f, 1.8f);
            elevatorCabin.name = "ElevatorCabin";
            
            // Add material to differentiate it
            Renderer renderer = elevatorCabin.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.8f, 0.8f, 0.8f);
            }
        }

        if (doorLeft == null)
        {
            doorLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorLeft.transform.parent = transform;
            doorLeft.transform.localPosition = new Vector3(-0.45f, 0, 0.9f);
            doorLeft.transform.localScale = new Vector3(0.9f, 2.5f, 0.1f);
            doorLeft.name = "DoorLeft";
            
            // Add material to differentiate it
            Renderer renderer = doorLeft.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.5f, 0.5f, 0.7f);
            }
        }

        if (doorRight == null)
        {
            doorRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorRight.transform.parent = transform;
            doorRight.transform.localPosition = new Vector3(0.45f, 0, 0.9f);
            doorRight.transform.localScale = new Vector3(0.9f, 2.5f, 0.1f);
            doorRight.name = "DoorRight";
            
            // Add material to differentiate it
            Renderer renderer = doorRight.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.5f, 0.5f, 0.7f);
            }
        }
        
        // Log initialization
        Debug.Log($"Elevator {gameObject.name} initialized at floor {currentFloor} and position {transform.position}");
        isInitialized = true;
    }

    // ADDED: Debug update method to periodically check for stuck elevators
    void Update()
    {
        // Every 5 seconds, check if we have requested floors but aren't moving
        if (Time.frameCount % 300 == 0) {
            if (requestedFloors.Count > 0 && !isMoving) {
                Debug.LogWarning($"Elevator {gameObject.name} has {requestedFloors.Count} pending floors but isn't moving. Restarting movement.");
                if (moveCoroutine != null) {
                    StopCoroutine(moveCoroutine);
                }
                moveCoroutine = StartCoroutine(MoveElevator());
            }
        }
    }

    // Check if the elevator can accept more passengers
    public bool CanEnter(float weight)
    {
        // Check if doors are open
        if (!doorsOpen)
        {
            Debug.LogWarning($"Cannot enter elevator {gameObject.name} because doors are closed");
            return false;
        }
        
        // Check weight limit
        if (currentWeight + weight > maxWeight)
        {
            Debug.LogWarning($"Cannot enter elevator {gameObject.name} because it would exceed weight limit");
            return false;
        }
        
        // Count actual passengers (excluding elevator components)
        int passengerCount = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.gameObject != elevatorCabin && child.gameObject != doorLeft && child.gameObject != doorRight)
            {
                passengerCount++;
            }
        }
        
        // Check capacity limit
        if (passengerCount >= capacity)
        {
            Debug.LogWarning($"Cannot enter elevator {gameObject.name} because it is at capacity ({passengerCount}/{capacity})");
            return false;
        }
        
        return true;
    }

    // Add a passenger to the elevator
    public void AddPassenger(GameObject passenger)
    {
        if (passenger == null)
        {
            Debug.LogError("Attempted to add null passenger to elevator!");
            return;
        }
        
        if (CanEnter(70f)) // Assuming average weight for simplicity
        {
            // Store original parent and position for debugging
            Transform originalParent = passenger.transform.parent;
            Vector3 originalPos = passenger.transform.position;
            
            passenger.transform.SetParent(transform);
            
            // Position inside elevator with some random offset
            float offsetX = Random.Range(-0.6f, 0.6f);
            float offsetZ = Random.Range(-0.6f, 0.6f);
            passenger.transform.localPosition = new Vector3(offsetX, 0, offsetZ);
            
            // Update weight
            EnhancedPerson person = passenger.GetComponent<EnhancedPerson>();
            if (person != null)
            {
                currentWeight += person.weight;
            }
            else
            {
                currentWeight += 70f; // Default weight
            }
            
            Debug.Log($"Passenger {passenger.name} added to elevator {gameObject.name} at floor {currentFloor}. " +
                     $"Current weight: {currentWeight}kg. Position changed from {originalPos} to {passenger.transform.position}");
        }
        else
        {
            Debug.LogWarning($"Cannot add passenger {passenger.name} to elevator {gameObject.name}. Elevator full or doors closed.");
        }
    }

    // Request a floor for the elevator to visit
    public void RequestFloor(int floor)
    {
        // Validate floor number
        if (floor < 0 || floor > topFloor)
        {
            Debug.LogWarning($"Invalid floor request: {floor}. Floor must be between 0 and {topFloor}.");
            return;
        }
        
        // IMPROVED: Extra debug info
        Debug.Log($"Elevator {gameObject.name} received request for floor {floor}. Current floor: {currentFloor}, Moving: {isMoving}, Status: {elevatorStatus}");
        
        // Add floor to requested floors if not already there and not current floor
        if (!requestedFloors.Contains(floor) && floor != currentFloor)
        {
            requestedFloors.Add(floor);
            
            // If the elevator is idle, start moving
            if (!isMoving)
            {
                // IMPROVED: Better coroutine management
                if (moveCoroutine != null)
                {
                    StopCoroutine(moveCoroutine);
                    moveCoroutine = null;
                }
                
                moveCoroutine = StartCoroutine(MoveElevator());
                Debug.Log($"Starting movement coroutine for elevator {gameObject.name}");
            }
            
            Debug.Log($"Floor {floor} requested for elevator {gameObject.name}. Requested floors: {string.Join(", ", requestedFloors)}");
        }
        else if (floor == currentFloor && !isMoving)
        {
            // We're already at the requested floor, just open the doors
            Debug.Log($"Elevator {gameObject.name} is already at floor {floor}. Opening doors.");
            if (doorCoroutine != null)
            {
                StopCoroutine(doorCoroutine);
            }
            doorCoroutine = StartCoroutine(AnimateDoors(true));
        }
    }

    // Call elevator to floor (external button press)
    public void CallToFloor(int floor)
    {
        RequestFloor(floor);
    }

    // Coroutine to move the elevator to requested floors
    private IEnumerator MoveElevator()
    {
        if (isMoving)
        {
            Debug.LogWarning($"Elevator {gameObject.name} is already moving. Ignoring move request.");
            yield break;
        }
        
        isMoving = true;
        elevatorStatus = ElevatorStatus.Moving;
        
        Debug.Log($"Elevator {gameObject.name} starting movement to floors: {string.Join(", ", requestedFloors)}");
        
        while (requestedFloors.Count > 0)
        {
            // Sort requests to be more efficient
            SortRequests();
            
            int targetFloor = requestedFloors[0];
            float targetY = targetFloor * floorHeight + 0.05f; // Add small offset to avoid clipping
            Vector3 targetPosition = new Vector3(transform.position.x, targetY, transform.position.z);
            
            // Determine movement direction for status
            if (targetFloor > currentFloor)
                elevatorStatus = ElevatorStatus.MovingUp;
            else
                elevatorStatus = ElevatorStatus.MovingDown;
            
            // Close doors before moving if open
            if (doorsOpen)
            {
                if (doorCoroutine != null)
                {
                    StopCoroutine(doorCoroutine);
                }
                
                doorCoroutine = StartCoroutine(AnimateDoors(false));
                yield return doorCoroutine;
            }
            
            // Move to requested floor
            Debug.Log($"Elevator {gameObject.name} moving from floor {currentFloor} (y={transform.position.y}) to floor {targetFloor} (y={targetY})");
            
            float startTime = Time.time;
            float journeyLength = Mathf.Abs(transform.position.y - targetPosition.y);
            float distanceCovered = 0;
            
            // Only move if we're not already at the target position
            if (journeyLength > 0.01f)
            {
                while (distanceCovered < journeyLength)
                {
                    float distanceThisFrame = speed * Time.deltaTime;
                    distanceCovered += distanceThisFrame;
                    
                    // Move towards target
                    transform.position = Vector3.MoveTowards(
                        transform.position, 
                        targetPosition, 
                        distanceThisFrame
                    );
                    
                    // Log position every few frames for debugging
                    if (Random.value < 0.05f) // Only log occasionally to avoid spam
                    {
                        Debug.Log($"Elevator {gameObject.name} moving: current={transform.position.y}, target={targetY}, distance={journeyLength-distanceCovered}");
                    }
                    
                    yield return null;
                }
            }
            
            // Snap to exact position
            transform.position = targetPosition;
            currentFloor = targetFloor;
            requestedFloors.RemoveAt(0);
            
            Debug.Log($"Elevator {gameObject.name} arrived at floor {currentFloor}, position: {transform.position}");
            
            // Open doors
            elevatorStatus = ElevatorStatus.Loading;
            if (doorCoroutine != null)
            {
                StopCoroutine(doorCoroutine);
            }
            
            doorCoroutine = StartCoroutine(AnimateDoors(true));
            yield return doorCoroutine;
            
            // Notify passengers to exit if this is their destination
            NotifyPassengers();
            
            // Wait for loading/unloading
            yield return new WaitForSeconds(doorOpenTime);
            
            // Close doors before moving to next floor
            if (requestedFloors.Count > 0)
            {
                if (doorCoroutine != null)
                {
                    StopCoroutine(doorCoroutine);
                }
                
                doorCoroutine = StartCoroutine(AnimateDoors(false));
                yield return doorCoroutine;
            }
        }
        
        isMoving = false;
        elevatorStatus = ElevatorStatus.Idle;
        Debug.Log($"Elevator {gameObject.name} completed all requested floors and is now idle.");
    }

    // Sort requested floors to be more efficient
    private void SortRequests()
    {
        // Simple elevator algorithm: continue in current direction until no more requests,
        // then change direction.
        
        // This is a simplified version that just processes floors in the current direction first
        if (requestedFloors.Count <= 1) return;
        
        List<int> upRequests = new List<int>();
        List<int> downRequests = new List<int>();
        
        foreach (int floor in requestedFloors)
        {
            if (floor >= currentFloor)
                upRequests.Add(floor);
            else
                downRequests.Add(floor);
        }
        
        // Sort up requests in ascending order
        upRequests.Sort();
        
        // Sort down requests in descending order
        downRequests.Sort((a, b) => b.CompareTo(a));
        
        // Clear and repopulate the list based on current direction
        requestedFloors.Clear();
        
        if (elevatorStatus == ElevatorStatus.MovingUp || elevatorStatus == ElevatorStatus.Idle)
        {
            requestedFloors.AddRange(upRequests);
            requestedFloors.AddRange(downRequests);
        }
        else
        {
            requestedFloors.AddRange(downRequests);
            requestedFloors.AddRange(upRequests);
        }
    }

    // Animate doors opening or closing
    private IEnumerator AnimateDoors(bool opening)
    {
        float duration = 1.0f;
        float elapsedTime = 0f;
        
        // Get initial positions
        Vector3 leftStartPos = doorLeft.transform.localPosition;
        Vector3 rightStartPos = doorRight.transform.localPosition;
        
        // Calculate target positions
        Vector3 leftEndPos = leftStartPos;
        Vector3 rightEndPos = rightStartPos;
        
        if (opening)
        {
            leftEndPos.x = leftStartPos.x - doorOpenDistance;
            rightEndPos.x = rightStartPos.x + doorOpenDistance;
        }
        else
        {
            leftEndPos.x = -0.45f;
            rightEndPos.x = 0.45f;
        }
        
        // Animate
        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            
            // Ease in-out function
            t = t < 0.5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
            
            doorLeft.transform.localPosition = Vector3.Lerp(leftStartPos, leftEndPos, t);
            doorRight.transform.localPosition = Vector3.Lerp(rightStartPos, rightEndPos, t);
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Ensure doors reach final position
        doorLeft.transform.localPosition = leftEndPos;
        doorRight.transform.localPosition = rightEndPos;
        
        doorsOpen = opening;
        
        Debug.Log($"Elevator {gameObject.name} doors are now {(opening ? "open" : "closed")} at floor {currentFloor}");
    }

    // Notify passengers when arriving at a floor
    private void NotifyPassengers()
    {
        // Debug passenger count
        int passengerCount = 0;
        int exitingCount = 0;
        
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            
            // Skip elevator components
            if (child.gameObject == elevatorCabin || child.gameObject == doorLeft || child.gameObject == doorRight)
                continue;
            
            passengerCount++;
            
            EnhancedPerson person = child.GetComponent<EnhancedPerson>();
            if (person != null && person.targetFloor == currentFloor)
            {
                exitingCount++;
                // Update weight before removing
                currentWeight -= person.weight;
                person.ExitElevator();
            }
        }
        
        Debug.Log($"Elevator {gameObject.name} has {passengerCount} passengers at floor {currentFloor}, {exitingCount} exiting");
    }
    
    public enum ElevatorStatus
    {
        Idle,
        Moving,
        MovingUp,
        MovingDown,
        Loading,
        Maintenance,
        OutOfService
    }
    
    // For debugging
    void OnDrawGizmos()
    {
        // Draw a vertical line showing elevator path
        Gizmos.color = Color.yellow;
        Vector3 bottom = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 top = new Vector3(transform.position.x, topFloor * floorHeight, transform.position.z);
        Gizmos.DrawLine(bottom, top);
        
        // Draw current position
        Gizmos.color = isMoving ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(1.8f, 2.5f, 1.8f));
        
        // Mark floors along the path
        for (int i = 0; i <= topFloor; i++)
        {
            float y = i * floorHeight + 0.05f;
            Gizmos.color = (i == currentFloor) ? Color.green : Color.gray;
            Gizmos.DrawWireSphere(new Vector3(transform.position.x, y, transform.position.z), 0.2f);
        }
    }
}