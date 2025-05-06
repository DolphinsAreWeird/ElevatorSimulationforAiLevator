using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Required for Linq operations like Any()

#if UNITY_EDITOR
using UnityEditor; // Required for Handles and Selection
#endif

[SelectionBase]
public class EnhancedPerson : MonoBehaviour
{
    // Static variable to control global label visibility
    public static bool DisplayAllLabels = true;

    public enum PersonState
    {
        Idle, 
        Wandering, 
        MovingToElevatorWaitingArea,
        WaitingForElevator,
        MovingToEnterElevator, 
        InElevator,
        ExitingElevator, 
        MovingToFinalDestinationOnFloor, 
        TakingStairs 
    }

    [Header("Person Settings")]
    public int currentFloor = 0;
    public int homeFloor = 0;
    public int targetFloor = 0; 
    public float weight = 70f;
    public float moveSpeed = 1.5f;
    public float floorHeight = 3f;

    [Header("Behavior Settings")]
    public float minIdleTime = 2f; 
    public float maxIdleTime = 5f; 
    public float minWanderTime = 3f;
    public float maxWanderTime = 8f;


    public PersonType personType = PersonType.OfficeWorker;
    public bool isInGroup = false; 
    public bool isInRush = false;
    [Range(1f, 1.5f)]
    public float rushSpeedMultiplier = 1.2f; 
    [Range(0f, 1f)]
    public float socialBehaviorChance = 0.3f; 

    [Header("Elevator Usage")]
    [Range(0f, 1f)]
    public float baseElevatorUsageProbability = 0.8f; 
    [Range(0f, 1f)]
    public float perFloorDistanceBonus = 0.1f;
    [Range(0f, 1f)]
    public float maxElevatorUsageProbability = 0.98f;
    public float elevatorWaitingSpotOffset = 2.0f; 
    public float elevatorBoardingDistance = 0.5f; 

    [Header("Personal Traits")]
    [Range(0f, 1f)]
    public float elevatorPreference = 0.7f;
    [Range(0f, 1f)]
    public float patienceLevel = 0.5f; 
    private float maxWaitTimeForElevatorActual;


    [Header("Navigation")]
    public Vector2 buildingSize = new Vector2(20f, 20f);
    public float wallDetectionDistance = 1.0f; 
    public float maxSpeed = 2.0f;
    public float positionClamp = 100f;
    private float targetReachedThreshold = 0.2f; 

    // State Machine
    [Header("Current State")]
    [SerializeField] 
    private PersonState currentState = PersonState.Idle;
    private float currentStateStartTime;
    private float currentActionDuration;


    // References
    [HideInInspector]
    public List<Elevator> availableElevators = new List<Elevator>();
    private Elevator targetElevator; 
    private ElevatorController elevatorController;
    private SimplifiedSimulationManager simulationManager;

    // Movement
    private Vector3 moveTargetPosition;
    private bool hasMoveTarget = false;

    // Component references
    private Renderer personRenderer;

    // Debug
    private Vector3 lastPosition;
    private float lastMoveTime;


    void Start()
    {
        personRenderer = GetComponent<Renderer>();
        if (personRenderer == null) personRenderer = gameObject.AddComponent<MeshRenderer>(); 

        InitializePersonType(); 
        elevatorPreference = Random.value;
        patienceLevel = Random.value;
        maxWaitTimeForElevatorActual = 10f + (patienceLevel * 20f); 

        simulationManager = FindObjectOfType<SimplifiedSimulationManager>();
        elevatorController = FindObjectOfType<ElevatorController>();

        if (availableElevators == null || availableElevators.Count == 0)
        {
            availableElevators = FindObjectsOfType<Elevator>().ToList();
        }
        if (availableElevators.Count == 0 && currentState != PersonState.TakingStairs) 
        {
            Debug.LogError($"Person {gameObject.name} couldn't find any elevators!");
        }
        
        lastPosition = transform.position;
        lastMoveTime = Time.time;

        Vector3 startPos = transform.position;
        startPos.y = currentFloor * floorHeight + (transform.localScale.y / 2f); 
        transform.position = startPos;

        ChangeState(PersonState.Idle);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        UpdateRushStatus(); 

        if (Mathf.Abs(transform.position.x) > positionClamp || Mathf.Abs(transform.position.z) > positionClamp) {
            Debug.LogWarning($"Person {gameObject.name} out of bounds ({transform.position}), resetting to floor center.");
            transform.position = new Vector3(0, currentFloor * floorHeight + (transform.localScale.y / 2f), 0);
            ChangeState(PersonState.Idle); 
        }


        switch (currentState)
        {
            case PersonState.Idle:
                if (Time.time - currentStateStartTime >= currentActionDuration) DecideNextAction();
                break;
            case PersonState.Wandering:
                if (hasMoveTarget) MoveTowardsTarget(dt);
                else ChangeState(PersonState.Idle); 
                if (Time.time - currentStateStartTime >= currentActionDuration && !hasMoveTarget) ChangeState(PersonState.Idle); 
                break;
            case PersonState.MovingToElevatorWaitingArea:
                if (hasMoveTarget) MoveTowardsTarget(dt);
                else ChangeState(PersonState.WaitingForElevator); 
                break;
            case PersonState.WaitingForElevator:
                HandleWaitingForElevator();
                break;
            case PersonState.MovingToEnterElevator:
                 if (hasMoveTarget) MoveTowardsTarget(dt);
                 else HandleReachedElevatorToEnter(); 
                break;
            case PersonState.InElevator:
                break;
            case PersonState.ExitingElevator:
                 if (hasMoveTarget) MoveTowardsTarget(dt);
                 else ChangeState(PersonState.MovingToFinalDestinationOnFloor); 
                break;
            case PersonState.MovingToFinalDestinationOnFloor:
                 if (hasMoveTarget) MoveTowardsTarget(dt);
                 else ChangeState(PersonState.Idle); 
                break;
            case PersonState.TakingStairs:
                break;
        }

        if (Time.time - lastMoveTime > 0.2f) {
            if (Vector3.Distance(transform.position, lastPosition) / (Time.time - lastMoveTime) < maxSpeed * 2f) { 
                lastPosition = transform.position;
            }
            lastMoveTime = Time.time;
        }
    }

    void ChangeState(PersonState newState)
    {
        currentState = newState;
        currentStateStartTime = Time.time;
        hasMoveTarget = false; 

        switch (newState)
        {
            case PersonState.Idle:
                currentActionDuration = Random.Range(minIdleTime, maxIdleTime);
                targetElevator = null; 
                break;
            case PersonState.Wandering:
                currentActionDuration = Random.Range(minWanderTime, maxWanderTime);
                SetRandomWanderTargetOnCurrentFloor();
                break;
            case PersonState.InElevator:
                transform.localPosition = GetRandomPositionInsideElevator(); 
                break;
            case PersonState.MovingToFinalDestinationOnFloor:
                SetRandomWanderTargetOnCurrentFloor(); 
                break;
        }
    }
    
    private Vector3 GetRandomPositionInsideElevator()
    {
        if (targetElevator == null || targetElevator.elevatorCabin == null) return Vector3.zero;
        float cabinHalfWidth = targetElevator.elevatorCabin.transform.localScale.x / 2f - 0.3f;
        float cabinHalfDepth = targetElevator.elevatorCabin.transform.localScale.z / 2f - 0.3f;
        return new Vector3(Random.Range(-cabinHalfWidth, cabinHalfWidth), 0f, Random.Range(-cabinHalfDepth, cabinHalfDepth));
    }


    void InitializePersonType()
    {
        if (personRenderer == null) personRenderer = GetComponent<Renderer>() ?? gameObject.AddComponent<MeshRenderer>();
        switch (personType)
        {
            case PersonType.OfficeWorker: personRenderer.material.color = Color.blue; break;
            case PersonType.Tourist: personRenderer.material.color = Color.yellow; break;
            case PersonType.Maintenance: personRenderer.material.color = Color.gray; break;
            case PersonType.Delivery: personRenderer.material.color = Color.green; break;
            case PersonType.Resident: personRenderer.material.color = Color.magenta; break;
            default: personRenderer.material.color = Color.white; break;
        }
    }

    void UpdateRushStatus()
    {
        if (simulationManager != null && Time.frameCount % 120 == 0) 
        {
            float timeOfDay = simulationManager.CurrentHour;
            isInRush = (timeOfDay >= 7f && timeOfDay <= 9.5f) || (timeOfDay >= 17f && timeOfDay <= 19f);
        }
    }

    public void GoToFloor(int destinationFloor)
    {
        // Debug.Log($"{gameObject.name} received external request to go to floor {destinationFloor}. Current floor: {currentFloor}");
        if (destinationFloor == currentFloor)
        {
            // Debug.Log($"{gameObject.name} is already on target floor {destinationFloor}. Will wander.");
            if(currentState != PersonState.Wandering && currentState != PersonState.Idle) ChangeState(PersonState.Idle);
            return;
        }

        this.targetFloor = destinationFloor; 

        if (ShouldUseElevator(destinationFloor))
        {
            GoToFloorUsingElevatorInternal(destinationFloor);
        }
        else
        {
            SimulateTakingStairs(destinationFloor);
        }
    }


    void DecideNextAction()
    {
        if (ShouldChangeFloor())
        {
            int newTargetFloor;
            int highestFloor = GetHighestFloorAvailable();
            if (highestFloor <= 0 && currentFloor == 0) { 
                 ChangeState(PersonState.Wandering);
                 return;
            }
            do {
                newTargetFloor = Random.Range(0, highestFloor + 1);
            } while (newTargetFloor == currentFloor && highestFloor > 0);

            GoToFloor(newTargetFloor);
        }
        else
        {
            ChangeState(PersonState.Wandering);
        }
    }

    bool ShouldChangeFloor()
    {
        return Random.value < 0.5f;
    }

    bool ShouldUseElevator(int destinationFloor)
    {
        int floorDistance = Mathf.Abs(destinationFloor - currentFloor);
        if (floorDistance == 0) return false;

        float probability = baseElevatorUsageProbability + (floorDistance * perFloorDistanceBonus);
        if (isInRush) probability *= 1.2f; 
        probability *= elevatorPreference; 
        if (floorDistance > 1) probability = Mathf.Max(probability, 0.9f); 

        return Random.value < Mathf.Clamp01(probability);
    }
    
    int GetHighestFloorAvailable()
    {
        if (availableElevators != null && availableElevators.Any(e => e != null)) 
        {
            return availableElevators.Where(e => e != null).Max(e => e.topFloor);
        }
        var allElevatorsInScene = FindObjectsOfType<Elevator>();
        if (allElevatorsInScene.Any()) {
            return allElevatorsInScene.Max(e => e.topFloor);
        }
        // Debug.LogWarning($"{gameObject.name}: Could not determine highest floor available from elevators. Defaulting to 0.");
        return 0; 
    }

    void GoToFloorUsingElevatorInternal(int destinationFloor) 
    {
        if (elevatorController == null) elevatorController = FindObjectOfType<ElevatorController>();
        if (elevatorController == null) {
            Debug.LogError($"{gameObject.name}: ElevatorController not found! Cannot use elevator.");
            SimulateTakingStairs(destinationFloor); 
            return;
        }
        if (!availableElevators.Any(e => e != null)) { 
            availableElevators = FindObjectsOfType<Elevator>().Where(e => e != null).ToList();
            if (!availableElevators.Any()) {
                Debug.LogError($"{gameObject.name}: No elevators available! Cannot use elevator.");
                SimulateTakingStairs(destinationFloor); 
                return;
            }
        }

        targetElevator = elevatorController.FindBestElevatorForRequest(currentFloor, destinationFloor);

        if (targetElevator == null) { 
            targetElevator = availableElevators.FirstOrDefault(e => e != null && e.Status != Elevator.ElevatorStatus.OutOfService && e.Status != Elevator.ElevatorStatus.Maintenance);
        }

        if (targetElevator != null)
        {
            Vector3 elevatorBasePosition = new Vector3(targetElevator.transform.position.x, 
                                                       currentFloor * floorHeight + (transform.localScale.y / 2f), 
                                                       targetElevator.transform.position.z);
            Vector3 buildingCenterOnFloor = new Vector3(0, elevatorBasePosition.y, 0);
            Vector3 directionFromElevatorToCenter = (buildingCenterOnFloor - elevatorBasePosition).normalized;
            if (directionFromElevatorToCenter == Vector3.zero) directionFromElevatorToCenter = targetElevator.transform.forward * -1; 
             if (directionFromElevatorToCenter == Vector3.zero) directionFromElevatorToCenter = Vector3.forward; 

            moveTargetPosition = elevatorBasePosition + directionFromElevatorToCenter * elevatorWaitingSpotOffset;
            moveTargetPosition.y = currentFloor * floorHeight + (transform.localScale.y / 2f); 

            // Debug.Log($"{gameObject.name} (Floor {currentFloor}) moving to wait for E:{targetElevator.name} (at E-Floor {targetElevator.CurrentFloor}) for target floor {this.targetFloor}. Waiting at {moveTargetPosition}");

            hasMoveTarget = true;
            targetElevator.CallToFloor(currentFloor); 
            ChangeState(PersonState.MovingToElevatorWaitingArea);
        }
        else
        {
            // Debug.LogWarning($"{gameObject.name} couldn't find a suitable elevator for floor {destinationFloor}. Trying stairs.");
            SimulateTakingStairs(destinationFloor); 
        }
    }

    void HandleWaitingForElevator()
    {
        if (targetElevator == null) {
            ChangeState(PersonState.Idle); return;
        }
        if (Time.time - currentStateStartTime > maxWaitTimeForElevatorActual)
        {
            // Debug.Log($"{gameObject.name} got tired of waiting for E:{targetElevator.name}. Trying stairs.");
            targetElevator = null; 
            SimulateTakingStairs(targetFloor); 
            return;
        }

        if (targetElevator.CurrentFloor == currentFloor && targetElevator.DoorsOpen)
        {
            // Debug.Log($"{gameObject.name}: E:{targetElevator.name} arrived at floor {currentFloor} and doors open. Moving to enter.");
            moveTargetPosition = targetElevator.CabinPosition + targetElevator.elevatorCabin.transform.forward * 0.1f; 
            moveTargetPosition.y = currentFloor * floorHeight + (transform.localScale.y / 2f);
            hasMoveTarget = true;
            ChangeState(PersonState.MovingToEnterElevator);
        } else {
             Vector3 elevatorExpectedPos = new Vector3(targetElevator.transform.position.x, transform.position.y, targetElevator.transform.position.z);
            if (Vector3.Distance(transform.position, elevatorExpectedPos) > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(elevatorExpectedPos - transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
            }
        }
    }
    
    void HandleReachedElevatorToEnter()
    {
        if (targetElevator == null || !targetElevator.DoorsOpen || targetElevator.CurrentFloor != currentFloor)
        {
            // Debug.LogWarning($"{gameObject.name} reached E spot, but E:{targetElevator?.name} not ready. Waiting again.");
            ChangeState(PersonState.WaitingForElevator); 
            return;
        }
        if (targetElevator.CanEnter(this))
        {
            // Debug.Log($"{gameObject.name} entering E:{targetElevator.name}. My Target Floor: {this.targetFloor}");
            targetElevator.AddPassenger(this); 
            targetElevator.RequestFloor(this.targetFloor, true); 
            ChangeState(PersonState.InElevator);
        }
        else
        {
            // Debug.LogWarning($"{gameObject.name} cannot enter E:{targetElevator.name} (full/issue). Waiting again.");
            ChangeState(PersonState.WaitingForElevator); 
        }
    }

    public void SignalExitElevator(Elevator elevatorExiting)
    {
        if (currentState != PersonState.InElevator || transform.parent == null) {
             if (currentState == PersonState.InElevator && transform.parent == null) {
                 currentFloor = elevatorExiting.CurrentFloor;
                 // Debug.LogWarning($"{gameObject.name} was InElevator but unparented. Set currentFloor to {currentFloor}. Exiting.");
            } else {
                // Debug.LogWarning($"{gameObject.name} received SignalExitElevator but not InElevator or not parented. State: {currentState}");
                return; 
            }
        }
        
        // Debug.Log($"{gameObject.name} signaled to exit E:{elevatorExiting.name} at floor {elevatorExiting.CurrentFloor}. My target was {this.targetFloor}.");
        transform.SetParent(null); 
        currentFloor = elevatorExiting.CurrentFloor; 
        elevatorExiting.RemovePassenger(this);


        Vector3 exitDirection = (transform.position - elevatorExiting.CabinPosition).normalized;
        if (exitDirection == Vector3.zero) exitDirection = elevatorExiting.elevatorCabin.transform.forward * -1; 
        if (exitDirection == Vector3.zero) exitDirection = transform.forward; 
        exitDirection.y = 0; 

        moveTargetPosition = elevatorExiting.CabinPosition + exitDirection.normalized * elevatorWaitingSpotOffset; 
        moveTargetPosition.y = currentFloor * floorHeight + (transform.localScale.y / 2f); 
        
        hasMoveTarget = true;
        targetElevator = null; 
        ChangeState(PersonState.ExitingElevator);
    }

    void SimulateTakingStairs(int destinationFloor)
    {
        // Debug.Log($"{gameObject.name} taking stairs from {currentFloor} to {destinationFloor}");
        ChangeState(PersonState.TakingStairs); 
        StartCoroutine(StairsTransitionCoroutine(destinationFloor));
    }

    IEnumerator StairsTransitionCoroutine(int destinationFloor)
    {
        if(personRenderer) personRenderer.enabled = false; 
        float travelTimePerFloor = 3.0f; 
        float totalTravelTime = Mathf.Abs(destinationFloor - currentFloor) * travelTimePerFloor;
        yield return new WaitForSeconds(totalTravelTime);

        currentFloor = destinationFloor;
        Vector3 newPosOnFloor = new Vector3(Random.Range(-buildingSize.x/2f + 1f, buildingSize.x/2f -1f), 
                                            currentFloor * floorHeight + (transform.localScale.y / 2f), 
                                            Random.Range(-buildingSize.y/2f + 1f, buildingSize.y/2f - 1f));
        transform.position = newPosOnFloor;
        lastPosition = newPosOnFloor; 

        if(personRenderer) personRenderer.enabled = true; 
        // Debug.Log($"{gameObject.name} arrived at floor {currentFloor} via stairs.");
        ChangeState(PersonState.Idle); 
    }

    void SetRandomWanderTargetOnCurrentFloor()
    {
        float halfWidth = buildingSize.x / 2f - 1f; 
        float halfDepth = buildingSize.y / 2f - 1f;
        moveTargetPosition = new Vector3(
            Random.Range(-halfWidth, halfWidth),
            currentFloor * floorHeight + (transform.localScale.y / 2f), 
            Random.Range(-halfDepth, halfDepth)
        );
        hasMoveTarget = true;
    }

    void MoveTowardsTarget(float dt)
    {
        if (!hasMoveTarget) return;
        Vector3 direction = (moveTargetPosition - transform.position);
        direction.y = 0; 

        if (direction.magnitude < targetReachedThreshold)
        {
            hasMoveTarget = false;
            transform.position = new Vector3(moveTargetPosition.x, transform.position.y, moveTargetPosition.z); 
            return;
        }

        float actualSpeed = moveSpeed * (isInRush ? rushSpeedMultiplier : 1f);
        actualSpeed = Mathf.Min(actualSpeed, maxSpeed);
        
        transform.position = Vector3.MoveTowards(transform.position, 
                                                 new Vector3(moveTargetPosition.x, transform.position.y, moveTargetPosition.z), 
                                                 actualSpeed * dt);

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, dt * 10f);
        }
    }


    void OnDrawGizmos()
    {
        if (hasMoveTarget)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, moveTargetPosition);
            Gizmos.DrawSphere(moveTargetPosition, 0.2f);
        }

        if (currentState == PersonState.WaitingForElevator && targetElevator != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, targetElevator.transform.position);
        }
        
        // --- MODIFIED SECTION FOR LABEL TOGGLE ---
        #if UNITY_EDITOR
        // Display label if global toggle is on OR if this specific GameObject is selected
        if (DisplayAllLabels || Selection.activeGameObject == gameObject)
        {
            Handles.Label(transform.position + Vector3.up * 1.5f, $"S: {currentState}\nCF:{currentFloor} TF:{targetFloor}\nE: {targetElevator?.name}");
        }
        #endif
        // --- END OF MODIFIED SECTION ---
    }

    public enum PersonType { OfficeWorker, Tourist, Maintenance, Delivery, Resident }
}
