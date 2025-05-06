using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[SelectionBase]
public class EnhancedPerson : MonoBehaviour
{
    [Header("Person Settings")]
    public int currentFloor = 0;   // Current floor
    public int homeFloor = 0;      // Home/base floor
    public int targetFloor = 0;    // Desired floor
    public float weight = 70f;     // Weight in kg for elevator capacity
    public float moveSpeed = 1.0f; // REDUCED: Movement speed (was 2.0f)
    public float floorHeight = 3f; // Height of each floor (should match building)
    
    [Header("Behavior Settings")]
    public float minWaitTime = 3f;   // Minimum time between actions
    public float maxWaitTime = 10f;  // Maximum time between actions
    
    // Enum definition first, then Header attribute on fields that use it
    public enum PersonType { OfficeWorker, Tourist, Maintenance, Delivery, Resident }
    
    [Header("Bangkok Style Settings")]
    public PersonType personType = PersonType.OfficeWorker;
    public bool isInGroup = false;
    public bool isInRush = false;
    [Range(1f, 1.5f)] // REDUCED max multiplier from 2.0f to 1.5f
    public float rushSpeedMultiplier = 1.2f; // REDUCED from 1.5f
    [Range(0f, 1f)]
    public float socialBehaviorChance = 0.3f; // Chance to follow others
    
    [Header("Elevator Usage Probabilities")]
    [Range(0f, 1f)]
    public float baseElevatorUsageProbability = 0.7f; // Higher for Bangkok high-rises
    [Range(0f, 1f)]
    public float peakHourMultiplier = 3.0f;           // Multiplier during peak hours (morning/evening rush)
    [Range(0f, 1f)]
    public float lunchTimeMultiplier = 2.5f;          // Multiplier during lunch time (important in Bangkok)
    [Range(0f, 1f)]
    public float perFloorDistanceBonus = 0.1f;        // Additional probability per floor of distance
    [Range(0f, 1f)]
    public float maxElevatorUsageProbability = 0.95f;  // Cap on max probability
    
    [Header("Personal Traits")]
    [Range(0f, 1f)]
    public float elevatorPreference;  // Randomized per-person preference (0=prefers stairs, 1=prefers elevator)
    [Range(0f, 1f)]
    public float patienceLevel;       // How long they'll wait for elevators
    [Range(0f, 1f)]
    public float socialness;          // Tendency to follow groups
    
    [Header("Navigation")]
    public Vector2 buildingSize = new Vector2(20f, 20f); // Building size for random movement
    public float wallDetectionDistance = 1.0f; // Distance to detect walls
    public float maxSpeed = 1.5f; // ADDED: Cap on maximum possible speed
    public float positionClamp = 100f; // ADDED: Maximum allowed position value to prevent stretching
    
    // Group behavior
    private EnhancedPerson groupLeader;
    private List<EnhancedPerson> groupFollowers = new List<EnhancedPerson>();
    
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
    
    // Public property for easier access from other scripts
    public bool IsWaitingForElevator => waitingForElevator;
    
    // Timing and patience
    private float elevatorWaitStartTime;
    private float maxWaitTimeForElevator = 15f; // Maximum time to wait before giving up
    
    // Schedule state
    private float nextActionTime;
    private ElevatorController elevatorController;

    // Component references
    [SerializeField]
    private Renderer personRenderer;
    [SerializeField]
    private CapsuleCollider personCollider;

    // Time reference
    private SimplifiedSimulationManager simulationManager;
    
    // Debug and error prevention
    private bool debugIssuesLogged = false;
    private float lastMoveTime;
    private Vector3 lastPosition;
    private bool possibleStretchDetected = false;
    
    // Start is called before the first frame update
    void Start()
    {
        // Initialize random traits based on person type
        InitializePersonType();
        
        // Initialize random elevator preference (simulating different personalities)
        elevatorPreference = Random.value;
        patienceLevel = Random.value;
        socialness = Random.value;
        
        // Chance to be in a rush based on time of day
        UpdateRushStatus();
        
        // Find simulation manager for time reference
        simulationManager = FindObjectOfType<SimplifiedSimulationManager>();
        
        // Store initial position for stretch detection
        lastPosition = transform.position;
        lastMoveTime = Time.time;
        
        InitializePerson();
    }
    
    private void InitializePersonType()
    {
        float rand = Random.value;
        if (rand < 0.7f)
        {
            personType = PersonType.OfficeWorker;
            moveSpeed = Random.Range(0.8f, 1.2f); // REDUCED from 1.8-2.5
            weight = Random.Range(50f, 85f);
        }
        else if (rand < 0.8f)
        {
            personType = PersonType.Tourist;
            moveSpeed = Random.Range(0.5f, 0.9f); // REDUCED from 1.0-1.8
            weight = Random.Range(55f, 90f);
            baseElevatorUsageProbability = 0.9f; // Tourists almost always use elevators
        }
        else if (rand < 0.9f)
        {
            personType = PersonType.Resident;
            moveSpeed = Random.Range(0.7f, 1.1f); // REDUCED from 1.5-2.2
            weight = Random.Range(45f, 80f);
        }
        else if (rand < 0.95f)
        {
            personType = PersonType.Maintenance;
            moveSpeed = Random.Range(0.6f, 1.0f); // REDUCED from 1.2-2.0
            weight = Random.Range(60f, 95f);
            baseElevatorUsageProbability = 0.5f; // Maintenance often uses stairs
        }
        else
        {
            personType = PersonType.Delivery;
            moveSpeed = Random.Range(0.9f, 1.3f); // REDUCED from 2.0-3.0
            weight = Random.Range(60f, 90f);
            baseElevatorUsageProbability = 0.6f;
        }
        
        // Enforce the speed cap
        moveSpeed = Mathf.Min(moveSpeed, maxSpeed);
        
        // Chance to be in a group
        isInGroup = Random.value < (personType == PersonType.Tourist ? 0.7f : 0.2f);
    }
    
    private void UpdateRushStatus()
    {
        if (simulationManager != null)
        {
            float timeOfDay = simulationManager.CurrentHour;
            
            // Morning rush (7-9 AM)
            if (timeOfDay >= 7f && timeOfDay <= 9f)
            {
                isInRush = Random.value < 0.7f;
            }
            // Lunch rush (11:30-1:30 PM)
            else if (timeOfDay >= 11.5f && timeOfDay <= 13.5f)
            {
                isInRush = Random.value < 0.5f;
            }
            // Evening rush (4-7 PM is longer in Bangkok due to traffic)
            else if (timeOfDay >= 16f && timeOfDay <= 19f)
            {
                isInRush = Random.value < 0.7f;
            }
            else
            {
                isInRush = Random.value < 0.1f;
            }
        }
        
        // Apply speed changes based on rush status
        if (isInRush)
        {
            moveSpeed *= rushSpeedMultiplier;
            // Enforce the speed cap after applying the multiplier
            moveSpeed = Mathf.Min(moveSpeed, maxSpeed);
        }
    }
    
    private void InitializePerson()
    {
        if (personRenderer)
        {
            personRenderer.material.color = GetColorForPersonType();
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
    
    private Color GetColorForPersonType()
    {
        switch (personType)
        {
            case PersonType.OfficeWorker:
                return new Color(0.1f, 0.3f, 0.8f); // Blue for office workers
            case PersonType.Tourist:
                return new Color(0.8f, 0.3f, 0.1f); // Orange for tourists
            case PersonType.Maintenance:
                return new Color(0.7f, 0.7f, 0.1f); // Yellow for maintenance
            case PersonType.Delivery:
                return new Color(0.1f, 0.7f, 0.3f); // Green for delivery
            case PersonType.Resident:
                return new Color(0.7f, 0.2f, 0.7f); // Purple for residents
            default:
                return new Color(Random.Range(0.5f, 1.0f), Random.Range(0.5f, 1.0f), Random.Range(0.5f, 1.0f));
        }
    }
    
    // Update is called once per frame
    void Update()
    {
        // Check if there's any stretched capsule issue
        CheckForVisualIssues();
        
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
            else
            {
                // Check for stuck waiting conditions
                if (Time.frameCount % 300 == 0 && targetElevator != null) {
                    float distance = Vector3.Distance(transform.position, targetElevator.transform.position);
                    Debug.Log($"Person {gameObject.name} waiting for elevator. Elevator floor: {targetElevator.CurrentFloor}, Person floor: {currentFloor}, Distance: {distance}, Doors open: {targetElevator.DoorsOpen}");
                    
                    // If elevator hasn't arrived after a while, re-request it
                    if (!targetElevator.IsMoving && targetElevator.CurrentFloor != currentFloor) {
                        Debug.Log($"Re-requesting elevator to come to floor {currentFloor}");
                        targetElevator.CallToFloor(currentFloor);
                    }
                    
                    // Check if we've been waiting too long based on patience
                    float waitTime = Time.time - elevatorWaitStartTime;
                    float adjustedMaxWaitTime = maxWaitTimeForElevator * patienceLevel;
                    
                    if (waitTime > adjustedMaxWaitTime)
                    {
                        Debug.Log($"Person {gameObject.name} got tired of waiting and is taking stairs");
                        waitingForElevator = false;
                        SimulateTakingStairs(targetFloor);
                        targetElevator = null;
                    }
                }
            }
        }
        
        // Update rush status occasionally based on time
        if (Time.frameCount % 1000 == 0 && simulationManager != null)
        {
            UpdateRushStatus();
        }
        
        // Group behavior - if we're a follower, adjust targets based on leader
        if (isInGroup && groupLeader != null && Random.value < 0.1f)
        {
            float distanceToLeader = Vector3.Distance(transform.position, groupLeader.transform.position);
            if (distanceToLeader > 3f)
            {
                // Follow leader
                moveTarget = groupLeader.transform.position - (groupLeader.transform.position - transform.position).normalized;
                hasTarget = true;
                isMovingToDestination = true;
            }
        }
        
        // Store position periodically for stretch detection
        if (Time.time - lastMoveTime > 0.5f)
        {
            lastPosition = transform.position;
            lastMoveTime = Time.time;
        }
    }
    
    // Check for visual issues and try to fix them
    private void CheckForVisualIssues()
    {
        // Check if position values are reasonable
        if (Mathf.Abs(transform.position.x) > positionClamp || 
            Mathf.Abs(transform.position.y) > positionClamp || 
            Mathf.Abs(transform.position.z) > positionClamp)
        {
            Debug.LogWarning($"Person {gameObject.name} has unreasonable position: {transform.position}. Resetting to last valid position.");
            transform.position = lastPosition;
        }
        
        // Check for extreme velocity which might cause stretching
        float velocity = Vector3.Distance(transform.position, lastPosition) / (Time.time - lastMoveTime);
        if (velocity > maxSpeed * 5 && !possibleStretchDetected)
        {
            Debug.LogWarning($"Person {gameObject.name} may be experiencing stretching. Velocity: {velocity}");
            possibleStretchDetected = true;
            
            // Try to fix by resetting position
            transform.position = lastPosition;
        }
        else if (velocity <= maxSpeed * 2)
        {
            possibleStretchDetected = false;
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
        
        // Group behavior - if leader, make decisions for group
        if (isInGroup && groupFollowers.Count > 0)
        {
            // As a leader, make group decisions
            if (ShouldChangeFloor())
            {
                // Choose a random floor that's not the current one for the whole group
                int newFloor;
                do {
                    newFloor = Random.Range(0, GetHighestFloor() + 1);
                } while (newFloor == currentFloor);
                
                // Determine if the group uses elevator
                bool useElevator = ShouldUseElevator(newFloor);
                
                // Leader's decision
                if (useElevator)
                {
                    GoToFloor(newFloor);
                    Debug.Log($"Group leader {gameObject.name} takes group to floor {newFloor} using elevator");
                    
                    // Notify followers
                    foreach (var follower in groupFollowers)
                    {
                        if (follower != null)
                        {
                            follower.FollowToFloor(newFloor, true);
                        }
                    }
                }
                else
                {
                    SimulateTakingStairs(newFloor);
                    Debug.Log($"Group leader {gameObject.name} takes group to floor {newFloor} using stairs");
                    
                    // Notify followers
                    foreach (var follower in groupFollowers)
                    {
                        if (follower != null)
                        {
                            follower.FollowToFloor(newFloor, false);
                        }
                    }
                }
            }
            else
            {
                // Move the group to a random spot
                MoveToRandomSpot();
                
                // Notify followers to follow
                foreach (var follower in groupFollowers)
                {
                    if (follower != null && Random.value < 0.7f)
                    {
                        follower.FollowToPosition(moveTarget);
                    }
                }
            }
        }
        // If a solo person or a follower with no active following task
        else if (!isInGroup || groupLeader == null)
        {
            // Social behavior - chance to join a group if we're social
            if (!isInGroup && socialness > 0.7f && Random.value < socialBehaviorChance)
            {
                TryJoinGroup();
            }
            // Otherwise, make our own decisions
            else if (!isFollowingOrder)
            {
                // First, decide if we want to go to another floor
                if (ShouldChangeFloor())
                {
                    // Choose a random floor that's not the current one
                    int newFloor;
                    do {
                        newFloor = Random.Range(0, GetHighestFloor() + 1);
                    } while (newFloor == currentFloor);
                    
                    // Special cases for different person types
                    AdjustTargetFloorByPersonType(ref newFloor);
                    
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
            }
        }
        
        // Schedule next action
        ScheduleNextAction();
    }
    
    // Following order flag and related data
    private bool isFollowingOrder = false;
    private Vector3 followPosition;
    private int followFloor;
    private bool followUseElevator;
    
    // Method to follow a leader to a position
    public void FollowToPosition(Vector3 position)
    {
        isFollowingOrder = true;
        followPosition = position;
        moveTarget = position + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
        hasTarget = true;
        isMovingToDestination = true;
        
        // Clear this order after a while
        StartCoroutine(ClearFollowOrderAfterDelay(Random.Range(3f, 7f)));
    }
    
    // Method to follow a leader to a floor
    public void FollowToFloor(int floor, bool useElevator)
    {
        isFollowingOrder = true;
        followFloor = floor;
        followUseElevator = useElevator;
        
        if (useElevator)
        {
            GoToFloor(floor);
        }
        else
        {
            SimulateTakingStairs(floor);
        }
        
        // This order will clear itself when the movement is complete
    }
    
    private IEnumerator ClearFollowOrderAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        isFollowingOrder = false;
    }
    
    private void TryJoinGroup()
    {
        // Find nearby people
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, 3f);
        List<EnhancedPerson> nearbyPeople = new List<EnhancedPerson>();
        
        foreach (var collider in nearbyColliders)
        {
            EnhancedPerson person = collider.GetComponent<EnhancedPerson>();
            if (person != null && person != this && person.currentFloor == currentFloor)
            {
                nearbyPeople.Add(person);
            }
        }
        
        if (nearbyPeople.Count > 0)
        {
            // Find a leader
            EnhancedPerson potentialLeader = nearbyPeople[Random.Range(0, nearbyPeople.Count)];
            
            if (potentialLeader.isInGroup && potentialLeader.groupLeader == null) // They're a leader
            {
                // Join their group
                groupLeader = potentialLeader;
                isInGroup = true;
                potentialLeader.groupFollowers.Add(this);
                Debug.Log($"Person {gameObject.name} joined group led by {potentialLeader.name}");
                
                // Follow them around
                FollowToPosition(potentialLeader.transform.position);
            }
            else if (!potentialLeader.isInGroup)
            {
                // Create a new group
                isInGroup = true;
                groupLeader = null; // We're the leader
                potentialLeader.isInGroup = true;
                potentialLeader.groupLeader = this;
                groupFollowers.Add(potentialLeader);
                Debug.Log($"Person {gameObject.name} formed a new group with {potentialLeader.name}");
            }
        }
    }
    
    private void AdjustTargetFloorByPersonType(ref int targetFloor)
    {
        switch (personType)
        {
            case PersonType.OfficeWorker:
                // Office workers tend to stay on their home floor during work hours
                if (simulationManager != null && simulationManager.CurrentHour >= 9 && simulationManager.CurrentHour <= 16)
                {
                    if (Random.value < 0.7f)
                    {
                        targetFloor = homeFloor;
                    }
                }
                break;
                
            case PersonType.Maintenance:
                // Maintenance workers visit all floors
                // No adjustment needed, random is good
                break;
                
            case PersonType.Tourist:
                // Tourists prefer top and bottom floors
                if (Random.value < 0.6f)
                {
                    targetFloor = (Random.value < 0.5f) ? 0 : GetHighestFloor();
                }
                break;
                
            case PersonType.Delivery:
                // Delivery people go to random floors but return to ground frequently
                if (currentFloor != 0 && Random.value < 0.4f)
                {
                    targetFloor = 0;
                }
                break;
                
            case PersonType.Resident:
                // Residents usually stay on their home floor or go to ground floor
                if (Random.value < 0.7f)
                {
                    targetFloor = (Random.value < 0.6f) ? homeFloor : 0;
                }
                break;
        }
    }
    
    // Determine if the person should change floors based on time of day
    private bool ShouldChangeFloor()
    {
        // Increased base chance for Bangkok buildings - more floor changes
        float baseChangeFloorProbability = 0.25f;
        
        // Person type modifications
        switch (personType)
        {
            case PersonType.OfficeWorker:
                // Office workers change floors less during work hours
                if (simulationManager != null && simulationManager.CurrentHour >= 10 && simulationManager.CurrentHour <= 15)
                {
                    baseChangeFloorProbability *= 0.6f;
                }
                break;
                
            case PersonType.Maintenance:
                // Maintenance workers change floors more often
                baseChangeFloorProbability *= 1.5f;
                break;
                
            case PersonType.Tourist:
                // Tourists explore a lot
                baseChangeFloorProbability *= 1.7f;
                break;
                
            case PersonType.Delivery:
                // Delivery people change floors very frequently
                baseChangeFloorProbability *= 2.0f;
                break;
        }
        
        // Get time of day if simulation manager exists
        if (simulationManager != null)
        {
            float timeOfDay = simulationManager.CurrentHour;
            
            // Bangkok specific patterns:
            
            // Morning rush (7-9 AM): People are going to their work floors
            if (timeOfDay >= 7f && timeOfDay <= 9f)
            {
                return Random.value < 0.6f;  // 60% chance - INCREASED for Bangkok
            }
            // 9:00-10:00 - Post-arrival movements (getting coffee, meetings)
            else if (timeOfDay >= 9f && timeOfDay <= 10f)
            {
                return Random.value < 0.4f;  // 40% chance
            }
            // Lunch time (11:30-1:30 PM): People moving for lunch
            else if (timeOfDay >= 11.5f && timeOfDay <= 13.5f)
            {
                return Random.value < 0.5f;  // 50% chance - INCREASED for Bangkok food culture
            }
            // Afternoon slump (2-3 PM): Less movement
            else if (timeOfDay >= 14f && timeOfDay <= 15f)
            {
                return Random.value < 0.2f;  // 20% chance - Thai afternoon slump
            }
            // Evening rush (4-7 PM): People leaving work (longer in Bangkok due to traffic planning)
            else if (timeOfDay >= 16f && timeOfDay <= 19f)
            {
                return Random.value < 0.6f;  // 60% chance - INCREASED
            }
            // Late evening (after 8 PM): Residents/night workers
            else if (timeOfDay >= 20f && timeOfDay <= 22f)
            {
                return Random.value < 0.3f;  // 30% chance
            }
            // Late night: Very little movement except security and cleaning
            else if (timeOfDay > 22f || timeOfDay < 6f)
            {
                return Random.value < 0.1f;  // 10% chance
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
        
        // IMPROVED: Make elevator usage more likely for Bangkok style
        float probability = baseElevatorUsageProbability + (floorDistance * perFloorDistanceBonus);
        
        // Adjust based on time of day if simulation manager exists
        if (simulationManager != null)
        {
            float timeOfDay = simulationManager.CurrentHour;
            
            // Morning rush (7-9 AM) or evening rush (4-7 PM) - extended for Bangkok
            if ((timeOfDay >= 7f && timeOfDay <= 9f) || (timeOfDay >= 16f && timeOfDay <= 19f))
            {
                probability *= peakHourMultiplier;
            }
            // Lunch time (11:30-1:30 PM) - important in Thai culture
            else if (timeOfDay >= 11.5f && timeOfDay <= 13.5f)
            {
                probability *= lunchTimeMultiplier;
            }
            // Rain hour adjustments (assuming afternoon rain in Bangkok)
            else if (timeOfDay >= 15f && timeOfDay <= 16f)
            {
                probability *= 1.5f; // People avoid stairs when it rains
            }
        }
        
        // Adjust based on person type
        switch (personType)
        {
            case PersonType.Tourist:
                probability *= 1.3f; // Tourists use elevators more
                break;
            case PersonType.Maintenance:
                probability *= 0.7f; // Maintenance sometimes uses stairs
                break;
            case PersonType.Delivery:
                probability *= 0.8f; // Delivery people might use stairs for speed
                break;
        }
        
        // Adjust based on personal preference
        probability = probability * (0.5f + elevatorPreference * 0.5f);
        
        // In rush increases elevator use
        if (isInRush)
        {
            probability *= 1.2f;
        }
        
        // Cap at maximum probability
        probability = Mathf.Min(probability, maxElevatorUsageProbability);
        
        // IMPROVED: Always use elevator for 2+ floor distances in Bangkok high-rises
        if (floorDistance >= 2)
        {
            probability = Mathf.Max(probability, 0.95f);
        }
        
        // Extremely close floors (1 floor difference) - many people would just take stairs
        if (floorDistance == 1 && elevatorPreference < 0.7f)
        {
            probability *= 0.7f; // Less reduction than before (was 0.5f)
        }
        
        // ADDED: During testing, make elevators more likely to be used
        #if UNITY_EDITOR
        probability = Mathf.Max(probability, 0.8f);
        #endif
        
        return Random.value < probability;
    }
    
    // Simulate taking stairs to another floor
    private void SimulateTakingStairs(int targetFloor)
    {
        // Calculate time it would take to use stairs (rough estimate)
        int floorDistance = Mathf.Abs(targetFloor - currentFloor);
        float stairsTravelTime = floorDistance * 3f; // Approx 3 seconds per floor
        
        // Adjust for person type and rush
        if (isInRush)
        {
            stairsTravelTime *= 0.7f; // 30% faster when in a rush
        }
        
        switch (personType)
        {
            case PersonType.Delivery:
                stairsTravelTime *= 0.6f; // Delivery people are fast on stairs
                break;
            case PersonType.Tourist:
                stairsTravelTime *= 1.5f; // Tourists are slow on stairs
                break;
        }
        
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
        float timeout = 0;
        while (Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z), 
            new Vector2(moveTarget.x, moveTarget.z)) > targetReachedThreshold && timeout < 10f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }
        
        // Now simulate going up/down stairs
        yield return new WaitForSeconds(travelTime);
        
        // Move to new floor
        Vector3 newPosition = new Vector3(
            -halfWidth + 1.5f, 
            targetFloor * floorHeight + 1f, 
            -buildingSize.y/4
        );
        
        // Use safe position change to avoid stretching
        transform.position = Vector3.ClampMagnitude(newPosition, positionClamp);
        
        // Update floor
        currentFloor = targetFloor;
        
        // Finally, move to a random position on the new floor
        hasTarget = false;
        isMovingToDestination = false;
        isFollowingOrder = false; // Clear any following order
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
            // Bangkok style - slight crowd forming around elevators
            Vector3 elevatorWaitPosition = targetElevator.transform.position +
                new Vector3(Random.Range(0.8f, 1.5f), 0, Random.Range(-1.0f, 1.0f));
            
            moveTarget = new Vector3(
                elevatorWaitPosition.x, 
                currentFloor * floorHeight + 1f, // Add 1 to be above floor
                elevatorWaitPosition.z
            );
            
            hasTarget = true;
            isMovingToDestination = true;
            waitingForElevator = true;
            elevatorWaitStartTime = Time.time;
            
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
            isFollowingOrder = false; // Clear any following order
            
            Debug.Log($"Person {gameObject.name} entered elevator {targetElevator.name} heading to floor {targetFloor}");
        }
        else
        {
            Debug.Log($"Person {gameObject.name} couldn't enter elevator {targetElevator.name}. Will try again.");
            
            // If elevator is full, maybe try another one or wait with Thai patience
            if (patienceLevel < 0.3f) // Impatient people
            {
                StartCoroutine(TryDifferentElevator());
            }
            else
            {
                // Try again after a short delay - Thai style patience
                StartCoroutine(RetryEnterElevator());
            }
        }
    }
    
    // Try a different elevator if the current one is full
    private IEnumerator TryDifferentElevator()
    {
        yield return new WaitForSeconds(1.0f);
        
        Debug.Log($"Person {gameObject.name} is impatient and trying a different elevator");
        targetElevator = null;
        RequestElevator();
    }
    
    // Try again to enter the elevator after a short delay
    private IEnumerator RetryEnterElevator()
    {
        yield return new WaitForSeconds(1.0f);
        
        if (targetElevator != null && targetElevator.CurrentFloor == currentFloor && targetElevator.DoorsOpen)
        {
            EnterElevator();
        }
        else
        {
            // If elevator is no longer available, request a new one
            targetElevator = null;
            RequestElevator();
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
        transform.SetParent(null);

        // Move away from elevator - Bangkok style, more varied exit paths
        float exitAngle = Random.Range(0, 360f) * Mathf.Deg2Rad;
        Vector3 exitDir = new Vector3(Mathf.Cos(exitAngle) * Random.Range(1.5f, 3.0f), 0, Mathf.Sin(exitAngle) * Random.Range(1.5f, 3.0f));
        Vector3 exitPos = elevatorPos + exitDir;
        exitPos.y = targetFloor * floorHeight + 1f; // Make sure y position is correct
        
        // Use safe position change to avoid stretching
        transform.position = Vector3.ClampMagnitude(exitPos, positionClamp);
        
        currentFloor = targetFloor;
        targetElevator = null;
        isFollowingOrder = false; // Clear any following order
        
        Debug.Log($"Person {gameObject.name} exited at floor {currentFloor}");
        
        // Schedule next action after a brief delay
        nextActionTime = Time.time + Random.Range(2f, 5f);
    }
    
    // Move to a random position on the current floor with Bangkok-style positioning
    private void MoveToRandomSpot()
    {
        float halfWidth = buildingSize.x / 2 - 2f; // Keep away from walls
        float halfDepth = buildingSize.y / 2 - 2f;
        
        Vector3 randomPos;
        
        // Bangkok style - some person types have preferred areas
        switch (personType)
        {
            case PersonType.OfficeWorker:
                // Office workers tend to cluster in groups
                randomPos = new Vector3(
                    Random.Range(-halfWidth * 0.8f, halfWidth * 0.8f), // More central
                    currentFloor * floorHeight + 1f,
                    Random.Range(-halfDepth * 0.8f, halfDepth * 0.8f)
                );
                break;
                
            case PersonType.Tourist:
                // Tourists go to the edges and windows
                if (Random.value < 0.7f)
                {
                    // Near windows/edges
                    float edge = Random.value < 0.5f ? 1 : -1;
                    if (Random.value < 0.5f)
                    {
                        // Along X edge
                        randomPos = new Vector3(
                            edge * halfWidth * 0.8f,
                            currentFloor * floorHeight + 1f,
                            Random.Range(-halfDepth * 0.8f, halfDepth * 0.8f)
                        );
                    }
                    else
                    {
                        // Along Z edge
                        randomPos = new Vector3(
                            Random.Range(-halfWidth * 0.8f, halfWidth * 0.8f),
                            currentFloor * floorHeight + 1f,
                            edge * halfDepth * 0.8f
                        );
                    }
                }
                else
                {
                    // Random position
                    randomPos = new Vector3(
                        Random.Range(-halfWidth, halfWidth),
                        currentFloor * floorHeight + 1f,
                        Random.Range(-halfDepth, halfDepth)
                    );
                }
                break;
                
            case PersonType.Maintenance:
                // Maintenance people check corners and edges
                if (Random.value < 0.6f)
                {
                    // Near corners or edges
                    float edgeX = Random.value < 0.5f ? 0.8f : -0.8f;
                    float edgeZ = Random.value < 0.5f ? 0.8f : -0.8f;
                    randomPos = new Vector3(
                        edgeX * halfWidth,
                        currentFloor * floorHeight + 1f,
                        edgeZ * halfDepth
                    );
                }
                else
                {
                    // Random position
                    randomPos = new Vector3(
                        Random.Range(-halfWidth, halfWidth),
                        currentFloor * floorHeight + 1f,
                        Random.Range(-halfDepth, halfDepth)
                    );
                }
                break;
                
            default:
                // Generate a random position for others
                randomPos = new Vector3(
                    Random.Range(-halfWidth, halfWidth),
                    currentFloor * floorHeight + 1f,
                    Random.Range(-halfDepth, halfDepth)
                );
                break;
        }
        
        // Ensure position is within safe limits
        moveTarget = Vector3.ClampMagnitude(randomPos, positionClamp);
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
            // FIXED: Here's the fix for wall detection - use layer or name instead of tag
            // Don't use CompareTag - look at the name or layer instead
            if (hit.collider.name.Contains("Wall"))
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
        
        // Bangkok style - more varied movement patterns
        if (isInRush)
        {
            // Direct movement when in a rush
            moveDirection = Vector3.Lerp(moveDirection, (moveTarget - transform.position).normalized, 0.9f);
        }
        else if (personType == PersonType.Tourist)
        {
            // Tourists meander more
            if (Random.value < 0.05f)
            {
                moveDirection = Quaternion.Euler(0, Random.Range(-15f, 15f), 0) * moveDirection;
            }
        }
        
        // Calculate speed with caps
        float currentSpeed = Mathf.Min(moveSpeed, maxSpeed);
        if (isInRush) currentSpeed = Mathf.Min(currentSpeed * rushSpeedMultiplier, maxSpeed);
        
        // Calculate new position with safe movement
        Vector3 movement = moveDirection * currentSpeed * Time.deltaTime;
        
        // Apply position change with safety limits
        Vector3 newPosition = transform.position + movement;
        
        // Ensure y position is maintained (don't fall through floors)
        newPosition.y = currentFloor * floorHeight + 1f;
        
        // Apply safety clamping
        if (Vector3.Distance(newPosition, transform.position) > maxSpeed * Time.deltaTime * 2)
        {
            // If movement is too large, normalize it to prevent stretching
            Debug.LogWarning($"Person {gameObject.name} movement too large. Clamping.");
            newPosition = transform.position + Vector3.ClampMagnitude(movement, maxSpeed * Time.deltaTime);
        }
        
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
                Debug.Log($"Person {gameObject.name} reached destination on floor {currentFloor}");
                
                // If this was a following order, clear it
                if (isFollowingOrder && !waitingForElevator)
                {
                    isFollowingOrder = false;
                }
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
        // Bangkok style - more frequent actions during peak times
        float waitTime = Random.Range(minWaitTime, maxWaitTime);
        
        // Person type adjustments
        switch (personType)
        {
            case PersonType.Tourist:
                waitTime *= 0.8f; // Tourists move around more frequently
                break;
            case PersonType.Delivery:
                waitTime *= 0.7f; // Delivery people are very active
                break;
            case PersonType.Maintenance:
                waitTime *= 0.9f; // Maintenance moves around regularly
                break;
        }
        
        // Adjust wait time based on time of day if simulation manager exists
        if (simulationManager != null)
        {
            float timeOfDay = simulationManager.CurrentHour;
            
            // Bangkok specific patterns
            
            // Morning rush (7-9 AM)
            if (timeOfDay >= 7f && timeOfDay <= 9f)
            {
                waitTime *= 0.5f; // Very short wait times - busy morning
            }
            // Mid-morning (9-11:30 AM)
            else if (timeOfDay >= 9f && timeOfDay <= 11.5f)
            {
                waitTime *= 0.8f; // Still fairly active
            }
            // Lunch time (11:30-1:30 PM) - Thai lunch culture is important
            else if (timeOfDay >= 11.5f && timeOfDay <= 13.5f)
            {
                waitTime *= 0.6f; // Very active lunch period
            }
            // Afternoon (1:30-4 PM) - Thai afternoon work
            else if (timeOfDay >= 13.5f && timeOfDay <= 16f)
            {
                waitTime *= 0.9f; // Normal activity
            }
            // Evening rush (4-7 PM) - Extended in Bangkok
            else if (timeOfDay >= 16f && timeOfDay <= 19f)
            {
                waitTime *= 0.5f; // Very active evening rush
            }
            // Evening (7-10 PM) - Bangkok nightlife starts
            else if (timeOfDay >= 19f && timeOfDay <= 22f)
            {
                waitTime *= 0.7f; // Active evening
            }
            // Late night (10 PM-7 AM)
            else
            {
                waitTime *= 1.5f; // Much less activity late night
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
    
    // For debugging
    void OnDrawGizmos()
    {
        // Draw ray showing where person is trying to go
        if (hasTarget)
        {
            // Color based on person type
            switch (personType)
            {
                case PersonType.OfficeWorker:
                    Gizmos.color = new Color(0, 0, 1, 0.5f); // Blue
                    break;
                case PersonType.Tourist:
                    Gizmos.color = new Color(1, 0.5f, 0, 0.5f); // Orange
                    break;
                case PersonType.Maintenance:
                    Gizmos.color = new Color(1, 1, 0, 0.5f); // Yellow
                    break;
                case PersonType.Delivery:
                    Gizmos.color = new Color(0, 1, 0, 0.5f); // Green
                    break;
                case PersonType.Resident:
                    Gizmos.color = new Color(0.5f, 0, 0.5f, 0.5f); // Purple
                    break;
                default:
                    Gizmos.color = new Color(0, 0.5f, 1, 0.5f); // Default blue
                    break;
            }
            
            Gizmos.DrawLine(transform.position, moveTarget);
        }
        
        // Draw sphere showing wall detection distance
        if (waitingForElevator)
        {
            Gizmos.color = new Color(1, 1, 0, 0.3f); // Yellow, semi-transparent
            Gizmos.DrawWireSphere(transform.position, wallDetectionDistance);
            
            // Draw line to target elevator if we have one
            if (targetElevator != null)
            {
                Gizmos.color = new Color(0, 1, 0, 0.5f); // Green, semi-transparent
                Gizmos.DrawLine(transform.position, targetElevator.transform.position);
            }
        }
        
        // Show group connections
        if (isInGroup)
        {
            if (groupLeader != null)
            {
                // Show connection to leader
                Gizmos.color = new Color(1, 0.5f, 0.5f, 0.5f); // Pink
                Gizmos.DrawLine(transform.position, groupLeader.transform.position);
            }
            else if (groupFollowers.Count > 0)
            {
                // Show connections to followers
                Gizmos.color = new Color(0.5f, 1, 0.5f, 0.5f); // Light green
                foreach (var follower in groupFollowers)
                {
                    if (follower != null)
                    {
                        Gizmos.DrawLine(transform.position, follower.transform.position);
                    }
                }
            }
        }
    }
}