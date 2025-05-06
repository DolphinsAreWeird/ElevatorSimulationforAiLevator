using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Elevator : MonoBehaviour
{
    [Header("Elevator Settings")]
    public float floorHeight = 3f;
    public float speed = 2f;
    public int capacity = 6;
    public float maxWeight = 500f;
    public float doorOpenTime = 2f; // Time doors stay open
    public int topFloor = 10;

    [Header("Visual Components")]
    public GameObject elevatorCabin;
    [Range(0f, 1f)]
    public float cabinTransparency = 0.3f;
    public Color cabinColor = new Color(0.7f, 0.7f, 0.8f);

    // Elevator state
    private int currentFloor = 0;
    private bool isMoving = false;
    private bool doorsOpen = false;
    private float currentWeight = 0f;
    private List<EnhancedPerson> personsInElevator = new List<EnhancedPerson>();
    private List<int> requestedFloors = new List<int>(); // Combined internal destinations and external calls
    private Coroutine moveCoroutine;

    private ElevatorStatus elevatorStatus = ElevatorStatus.Idle;
    private bool isInitialized = false;

    // Public properties
    public int CurrentFloor => currentFloor;
    public bool IsMoving => isMoving;
    public bool DoorsOpen => doorsOpen;
    public float CurrentWeight => currentWeight;
    public float RemainingCapacity => maxWeight - currentWeight;
    public int PassengerCount => personsInElevator.Count;
    public int RequestedFloorsCount => requestedFloors.Count;
    public ElevatorStatus Status => elevatorStatus;
    public Vector3 CabinPosition => elevatorCabin != null ? elevatorCabin.transform.position : transform.position;

    void Awake()
    {
        float cabinCenterOffsetY = (elevatorCabin != null && elevatorCabin.transform.localScale.y > 0) ? elevatorCabin.transform.localScale.y / 2f : 1.25f;
        float baseFloorY = currentFloor * floorHeight;
        transform.position = new Vector3(transform.position.x, baseFloorY + cabinCenterOffsetY, transform.position.z);
    }

    void Start()
    {
        InitializeElevator();
    }

    void InitializeElevator()
    {
        if (isInitialized) return;

        if (elevatorCabin == null)
        {
            Transform cabinTransform = transform.Find("ElevatorCabin");
            if (cabinTransform != null) elevatorCabin = cabinTransform.gameObject;
            else
            {
                elevatorCabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                elevatorCabin.name = "ElevatorCabin";
                elevatorCabin.transform.SetParent(transform, false);
                elevatorCabin.transform.localPosition = Vector3.zero;
                elevatorCabin.transform.localScale = new Vector3(1.8f, 2.5f, 1.8f);
            }
        }

        Renderer cabinRenderer = elevatorCabin.GetComponent<Renderer>();
        if (cabinRenderer != null)
        {
            Material cabinMat = cabinRenderer.material;
            Color newCabinColor = cabinColor;
            newCabinColor.a = cabinTransparency;
            cabinMat.color = newCabinColor;

            if (cabinTransparency < 1.0f) { // Transparent
                cabinMat.SetFloat("_Mode", 3); 
                cabinMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                cabinMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                cabinMat.SetInt("_ZWrite", 0);
                cabinMat.DisableKeyword("_ALPHATEST_ON");
                cabinMat.EnableKeyword("_ALPHABLEND_ON");
                cabinMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                cabinMat.renderQueue = 3000;
            } else { // Opaque
                cabinMat.SetFloat("_Mode", 0);
                cabinMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                cabinMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                cabinMat.SetInt("_ZWrite", 1);
                cabinMat.DisableKeyword("_ALPHATEST_ON");
                cabinMat.DisableKeyword("_ALPHABLEND_ON");
                cabinMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                cabinMat.renderQueue = -1; 
            }
        }
        
        float cabinCenterOffsetY = (elevatorCabin != null && elevatorCabin.transform.localScale.y > 0) ? elevatorCabin.transform.localScale.y / 2f : 1.25f;
        float baseFloorY = currentFloor * floorHeight; 
        transform.position = new Vector3(transform.position.x, baseFloorY + cabinCenterOffsetY, transform.position.z);

        Debug.Log($"ELEVATOR [{gameObject.name}]: Initialized. CurrentFloor: {currentFloor}, Position: {transform.position}, Cabin Transparency: {cabinTransparency}");
        isInitialized = true;
    }
    
    public void SetElevatorSize(Vector3 size)
    {
        if (elevatorCabin != null) elevatorCabin.transform.localScale = size;
    }

    void Update()
    {
        // Safety net: If idle with requests, try to process.
        if (Time.frameCount % 120 == 0) 
        {
            if (requestedFloors.Count > 0 && !isMoving && elevatorStatus == ElevatorStatus.Idle)
            {
                Debug.LogWarning($"ELEVATOR [{gameObject.name}]: SafetyNet - Idle with {requestedFloors.Count} requests. Processing. Queue: {string.Join(",", requestedFloors)}");
                ProcessNextRequest();
            }
        }
    }

    public bool CanEnter(EnhancedPerson person)
    {
        if (!doorsOpen) return false;
        if (personsInElevator.Count >= capacity) {
            Debug.LogWarning($"ELEVATOR [{gameObject.name}]: Denying entry - AT CAPACITY ({personsInElevator.Count}/{capacity}).");
            return false;
        }
        if (currentWeight + person.weight > maxWeight) {
            Debug.LogWarning($"ELEVATOR [{gameObject.name}]: Denying entry - OVERWEIGHT (Current: {currentWeight} + Person: {person.weight} > Max: {maxWeight}).");
            return false;
        }
        return true;
    }

    public void AddPassenger(EnhancedPerson person)
    {
        if (person == null) {
            Debug.LogError($"ELEVATOR [{gameObject.name}]: Attempted to add NULL passenger!");
            return;
        }
        if (!personsInElevator.Contains(person))
        {
            personsInElevator.Add(person);
            person.transform.SetParent(elevatorCabin.transform); 

            float cabinHalfWidth = elevatorCabin.transform.localScale.x / 2f - 0.4f; 
            float cabinHalfDepth = elevatorCabin.transform.localScale.z / 2f - 0.4f;
            person.transform.localPosition = new Vector3(Random.Range(-cabinHalfWidth, cabinHalfWidth), 0f, Random.Range(-cabinHalfDepth, cabinHalfDepth));

            currentWeight += person.weight;
            Debug.Log($"ELEVATOR [{gameObject.name}]: Passenger '{person.name}' ADDED at F{currentFloor}. Total: {personsInElevator.Count}, Weight: {currentWeight:F0}kg.");
        } else {
            Debug.LogWarning($"ELEVATOR [{gameObject.name}]: Passenger '{person.name}' already in elevator. Not adding again.");
        }
    }
    
    public void RemovePassenger(EnhancedPerson person)
    {
        if (person == null) {
             Debug.LogError($"ELEVATOR [{gameObject.name}]: Attempted to remove NULL passenger!");
            return;
        }
        if (personsInElevator.Contains(person))
        {
            currentWeight -= person.weight;
            personsInElevator.Remove(person);
            Debug.Log($"ELEVATOR [{gameObject.name}]: Passenger '{person.name}' REMOVED at F{currentFloor}. Total: {personsInElevator.Count}, Weight: {currentWeight:F0}kg.");
        } else {
            Debug.LogWarning($"ELEVATOR [{gameObject.name}]: Tried to remove passenger '{person.name}' not found in list.");
        }
    }

    public void RequestFloor(int floor, bool fromInsideElevator = false)
    {
        if (floor < 0 || floor > topFloor) {
            Debug.LogWarning($"ELEVATOR [{gameObject.name}]: Invalid floor request: {floor}. Max: {topFloor}.");
            return;
        }

        string requestType = fromInsideElevator ? "INTERNAL" : "EXTERNAL";
        Debug.Log($"ELEVATOR [{gameObject.name}]: Received {requestType} request for F{floor}. CurrentF: {currentFloor}, Status: {elevatorStatus}, Moving: {isMoving}");

        if (!requestedFloors.Contains(floor))
        {
            requestedFloors.Add(floor);
            Debug.Log($"ELEVATOR [{gameObject.name}]: F{floor} added to request queue. Queue: [{string.Join(", ", requestedFloors)}]");
        }
        
        if (!fromInsideElevator && floor == currentFloor && !isMoving && !doorsOpen && elevatorStatus != ElevatorStatus.Loading)
        {
            Debug.Log($"ELEVATOR [{gameObject.name}]: Called to current F{currentFloor} while not loading. Opening doors.");
            if (moveCoroutine != null) StopCoroutine(moveCoroutine); 
            StartCoroutine(OpenDoorsAndNotifyCoroutine());
            return; 
        }

        if (!isMoving && 
            (elevatorStatus == ElevatorStatus.Idle || elevatorStatus == ElevatorStatus.Loading) && 
            requestedFloors.Count > 0)
        {
            Debug.Log($"ELEVATOR [{gameObject.name}]: Request prompted re-evaluation. Status: {elevatorStatus}, HasRequests: {requestedFloors.Count > 0}. Processing next.");
            ProcessNextRequest();
        }
    }

    public void CallToFloor(int floor) => RequestFloor(floor, false);
    
    private void ProcessNextRequest()
    {
        if (isMoving) { 
            return;
        }
        if (moveCoroutine != null) {
            StopCoroutine(moveCoroutine);
        }
        moveCoroutine = StartCoroutine(MoveElevatorCoroutine());
    }

    private IEnumerator MoveElevatorCoroutine()
    {
        Debug.Log($"ELEVATOR [{gameObject.name}]: ===== MoveElevatorCoroutine STARTING =====");
        isMoving = true; 

        while (true) 
        {
            SortRequests(); 

            if (requestedFloors.Count == 0)
            {
                Debug.Log($"ELEVATOR [{gameObject.name}]: No more floors in sorted queue. Exiting MoveElevatorCoroutine.");
                break; 
            }
            
            int targetFloor = requestedFloors[0];
            Debug.Log($"ELEVATOR [{gameObject.name}]: Next target from sorted queue: F{targetFloor}. Current Queue: [{string.Join(", ", requestedFloors)}]");

            if (targetFloor == currentFloor) 
            {
                if (!doorsOpen) { 
                     Debug.Log($"ELEVATOR [{gameObject.name}]: Target F{targetFloor} is Current F{currentFloor}. Doors are closed. Opening.");
                     yield return StartCoroutine(OpenDoorsAndNotifyCoroutine());
                } else { 
                     Debug.Log($"ELEVATOR [{gameObject.name}]: Target F{targetFloor} is Current F{currentFloor}. Doors already open. Notifying passengers.");
                     NotifyPassengersToExit(); 
                }
                 yield return new WaitForSeconds(doorOpenTime); 
                 requestedFloors.Remove(targetFloor); 
                 continue; 
            }

            if (doorsOpen)
            {
                Debug.Log($"ELEVATOR [{gameObject.name}]: Closing doors at F{currentFloor} before moving to F{targetFloor}.");
                doorsOpen = false;
                yield return new WaitForSeconds(doorOpenTime / 2f); 
            }
            
            elevatorStatus = (targetFloor > currentFloor) ? ElevatorStatus.MovingUp : ElevatorStatus.MovingDown;
            Debug.Log($"ELEVATOR [{gameObject.name}]: Moving from F{currentFloor} to F{targetFloor}. Status: {elevatorStatus}");

            float cabinCenterOffsetY = (elevatorCabin != null && elevatorCabin.transform.localScale.y > 0) ? elevatorCabin.transform.localScale.y / 2f : 1.25f;
            float targetY = targetFloor * floorHeight + cabinCenterOffsetY;
            Vector3 targetPosition = new Vector3(transform.position.x, targetY, transform.position.z);

            while (Mathf.Abs(transform.position.y - targetY) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
                yield return null;
            }

            transform.position = targetPosition; 
            currentFloor = targetFloor; 
            Debug.Log($"ELEVATOR [{gameObject.name}]: ARRIVED at F{currentFloor}.");

            bool removed = requestedFloors.Remove(targetFloor); 
            if (!removed) Debug.LogWarning($"ELEVATOR [{gameObject.name}]: Target F{targetFloor} was not found in requestedFloors list for removal after arrival. Current Queue: [{string.Join(", ", requestedFloors)}]");

            yield return StartCoroutine(OpenDoorsAndNotifyCoroutine()); 

            Debug.Log($"ELEVATOR [{gameObject.name}]: Waiting at F{currentFloor} (Doors Open) for {doorOpenTime}s.");
            yield return new WaitForSeconds(doorOpenTime); 
        } 

        isMoving = false;
        elevatorStatus = doorsOpen ? ElevatorStatus.Loading : ElevatorStatus.Idle;
        Debug.Log($"ELEVATOR [{gameObject.name}]: ===== MoveElevatorCoroutine ENDED. Status: {elevatorStatus} at F{currentFloor}. DoorsOpen: {doorsOpen} =====");
    }
    
    private IEnumerator OpenDoorsAndNotifyCoroutine()
    {
        if (!doorsOpen) 
        {
            Debug.Log($"ELEVATOR [{gameObject.name}]: Opening doors at F{currentFloor}.");
            doorsOpen = true;
            elevatorStatus = ElevatorStatus.Loading; 
            yield return new WaitForSeconds(doorOpenTime / 2f); 
        } else {
            if(elevatorStatus != ElevatorStatus.Loading) elevatorStatus = ElevatorStatus.Loading; 
        }
        NotifyPassengersToExit(); 
    }

    private void SortRequests()
    {
        List<int> internalDestinations = personsInElevator
            .Select(p => p.targetFloor)
            .Distinct()
            .ToList();

        HashSet<int> allPotentialStops = new HashSet<int>(requestedFloors); 
        allPotentialStops.UnionWith(internalDestinations); 

        if (!allPotentialStops.Any()) {
            requestedFloors.Clear(); 
            return;
        }

        bool effectiveDirectionUp;

        // Section 1: Determine effective direction
        if (elevatorStatus == ElevatorStatus.MovingUp) {
            effectiveDirectionUp = true;
            // Debug.Log($"ELEVATOR [{gameObject.name}]: SortRequests (MovingUp) - Maintaining UP direction.");
        }
        else if (elevatorStatus == ElevatorStatus.MovingDown) {
            effectiveDirectionUp = false;
            // Debug.Log($"ELEVATOR [{gameObject.name}]: SortRequests (MovingDown) - Maintaining DOWN direction.");
        }
        else // Status is Idle or Loading. This is where prioritization is key.
        {
            List<int> internalTargetsForDifferentFloors = internalDestinations.Where(f => f != currentFloor).ToList();
            
            if (internalTargetsForDifferentFloors.Any()) // If there's at least one passenger wanting to go to a *different* floor
            {
                bool anyInternalGoingUp = internalTargetsForDifferentFloors.Any(f => f > currentFloor);
                bool anyInternalGoingDown = internalTargetsForDifferentFloors.Any(f => f < currentFloor);

                if (anyInternalGoingUp && !anyInternalGoingDown) { // All internal targets for different floors are UP
                    effectiveDirectionUp = true;
                    Debug.Log($"ELEVATOR [{gameObject.name}]: SortRequests (Idle/Loading) - Internal passengers exclusively want UP (to different floors). EffectiveDirUp: true");
                } else if (!anyInternalGoingUp && anyInternalGoingDown) { // All internal targets for different floors are DOWN
                    effectiveDirectionUp = false;
                    Debug.Log($"ELEVATOR [{gameObject.name}]: SortRequests (Idle/Loading) - Internal passengers exclusively want DOWN (to different floors). EffectiveDirUp: false");
                } else { // Conflicting internal targets (some up, some down) for different floors, or all targets are in one direction but some are up and some are down relative to current floor
                    // Pick direction towards the closest *internal* target for a different floor
                    int closestInternalTarget = internalTargetsForDifferentFloors
                                                .OrderBy(f => Mathf.Abs(f - currentFloor))
                                                // Tie-breaker: if equidistant, prefer continuing current implied direction or default to up
                                                .ThenBy(f => f > currentFloor ? 0 : 1) 
                                                .First();
                    effectiveDirectionUp = closestInternalTarget > currentFloor;
                    Debug.Log($"ELEVATOR [{gameObject.name}]: SortRequests (Idle/Loading) - CONFLICTING/MIXED internal targets for different floors. Closest internal target F{closestInternalTarget}. EffectiveDirUp: {effectiveDirectionUp}");
                }
            }
            else // No internal passengers going to a *different* floor (elevator empty, or all internal passengers want current floor)
            {
                // Decide based on *all* potential stops (which are mainly external calls now, or internal requests for current floor)
                var potentialOverallNextStopsQuery = allPotentialStops 
                    .Where(f => f != currentFloor) 
                    .OrderBy(f => Mathf.Abs(f - currentFloor)) 
                    .ThenBy(f => f > currentFloor ? 0 : 1); // Bias UP for equidistant

                if (potentialOverallNextStopsQuery.Any()) {
                    int closestOverallRequestToDifferentFloor = potentialOverallNextStopsQuery.First();
                    effectiveDirectionUp = closestOverallRequestToDifferentFloor > currentFloor;
                    Debug.Log($"ELEVATOR [{gameObject.name}]: SortRequests (Idle/Loading) - No internal targets for different floors. Closest OVERALL F{closestOverallRequestToDifferentFloor}. EffectiveDirUp: {effectiveDirectionUp}");
                } else {
                    // All requests are for the current floor, or no requests at all.
                    // Default direction if no other cues for a *different* floor. This is for planning the *next* potential move.
                    effectiveDirectionUp = true; 
                    Debug.Log($"ELEVATOR [{gameObject.name}]: SortRequests (Idle/Loading) - No requests for different floors. Defaulting EffectiveDirUp: {effectiveDirectionUp}");
                }
            }
        }
        // Debug.Log($"ELEVATOR [{gameObject.name}]: SortRequests - Midpoint: CurrentF: {currentFloor}, Status: {elevatorStatus}, Determined EffectiveDirectionUP: {effectiveDirectionUp}, AllStops: [{string.Join(",", allPotentialStops)}]");

        // Section 2: Separate requests based on the determined direction
        List<int> primaryDirectionStops = new List<int>();
        List<int> secondaryDirectionStops = new List<int>(); 

        if (allPotentialStops.Contains(currentFloor)) {
            primaryDirectionStops.Add(currentFloor);
        }

        foreach (int stop in allPotentialStops.Where(s => s != currentFloor)) 
        {
            if (effectiveDirectionUp) {
                if (stop > currentFloor) primaryDirectionStops.Add(stop);
                else secondaryDirectionStops.Add(stop);
            } else { 
                if (stop < currentFloor) primaryDirectionStops.Add(stop);
                else secondaryDirectionStops.Add(stop);
            }
        }
        
        // Section 3: Sort the stops within each list and make them distinct
        if (effectiveDirectionUp) {
            primaryDirectionStops = primaryDirectionStops.Distinct().OrderBy(f => f).ToList();
            secondaryDirectionStops = secondaryDirectionStops.Distinct().OrderByDescending(f => f).ToList();
        } else {
            primaryDirectionStops = primaryDirectionStops.Distinct().OrderByDescending(f => f).ToList();
            secondaryDirectionStops = secondaryDirectionStops.Distinct().OrderBy(f => f).ToList();
        }

        // Section 4: Rebuild the main requestedFloors list
        requestedFloors.Clear();
        requestedFloors.AddRange(primaryDirectionStops);
        requestedFloors.AddRange(secondaryDirectionStops);
        
        if (requestedFloors.Count > 0) {
            requestedFloors = requestedFloors.Distinct().ToList();
        }

        Debug.Log($"ELEVATOR [{gameObject.name}]: SortRequests FINAL - EffectiveDirUp: {effectiveDirectionUp}. Sorted Queue: [{string.Join(", ", requestedFloors)}]");
    }

    private void NotifyPassengersToExit()
    {
        for (int i = personsInElevator.Count - 1; i >= 0; i--) 
        {
            EnhancedPerson person = personsInElevator[i];
            if (person != null && person.targetFloor == currentFloor)
            {
                Debug.Log($"ELEVATOR [{gameObject.name}]: Passenger '{person.name}' (TargetF:{person.targetFloor}) matches CurrentF:{currentFloor}. Signaling exit.");
                person.SignalExitElevator(this); 
            }
        }
    }

    public enum ElevatorStatus { Idle, Moving, MovingUp, MovingDown, Loading, Maintenance, OutOfService }

    void OnDrawGizmos()
    {
        if (!isInitialized || elevatorCabin == null) return;

        float cabinCenterOffsetY = (elevatorCabin.transform.localScale.y > 0) ? elevatorCabin.transform.localScale.y / 2f : 1.25f;
        Gizmos.color = Color.yellow;
        Vector3 bottom = new Vector3(transform.position.x, 0 + cabinCenterOffsetY, transform.position.z); 
        Vector3 topPos = new Vector3(transform.position.x, topFloor * floorHeight + cabinCenterOffsetY, transform.position.z); 
        Gizmos.DrawLine(bottom, topPos);

        Gizmos.color = isMoving ? Color.red : (doorsOpen ? Color.blue : Color.green);
        Gizmos.DrawWireCube(elevatorCabin.transform.position, elevatorCabin.transform.localScale);

        for (int i = 0; i <= topFloor; i++) {
            float y = i * floorHeight + cabinCenterOffsetY; 
            Gizmos.color = (i == currentFloor) ? Color.green : Color.gray;
            Gizmos.DrawWireSphere(new Vector3(transform.position.x, y, transform.position.z), 0.2f);
        }

        Gizmos.color = Color.magenta;
        foreach (int reqFloor in requestedFloors) {
            float y = reqFloor * floorHeight + cabinCenterOffsetY; 
            Gizmos.DrawSphere(new Vector3(transform.position.x, y, transform.position.z), 0.3f);
        }
    }
}
