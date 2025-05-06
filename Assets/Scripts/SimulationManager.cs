using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq; // Required for .Any()

public class SimplifiedSimulationManager : MonoBehaviour
{
    [Header("Simulation Settings")]
    public float simulationSpeed = 1.0f;
    public bool pauseSimulation = false;
    public bool showDebugInfo = true;
    
    [Header("Time Settings")]
    public float dayLengthInMinutes = 10f; // A full day-night cycle in real minutes
    public float startTimeHour = 8f; // Start at 8 AM
    
    [Header("References")]
    public EnhancedBuildingGenerator buildingGenerator; // Assign in Inspector
    
    // Internal simulation time (in hours, 0-24)
    private float currentSimTime = 0f;
    private float simulationDay = 1f;
    
    // Event system
    public event Action<float> OnHourChanged;
    public event Action<int> OnDayChanged;
    
    // Singleton pattern for easy access
    private static SimplifiedSimulationManager _instance;
    public static SimplifiedSimulationManager Instance => _instance;
    
    // Public properties
    public float CurrentHour => currentSimTime;
    public float CurrentDay => simulationDay;
    public bool IsMorningRush => currentSimTime >= 7f && currentSimTime <= 9.5f; // Adjusted rush hour
    public bool IsLunchTime => currentSimTime >= 11.5f && currentSimTime <= 13.5f;
    public bool IsEveningRush => currentSimTime >= 16.5f && currentSimTime <= 19f; // Adjusted rush hour
    
    // Debug flag to track state
    private bool debugIssuesLogged = false; // To prevent spamming logs for certain checks
    
    private void Awake()
    {
        // Singleton setup
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("Another instance of SimplifiedSimulationManager found. Destroying this one.");
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        // DontDestroyOnLoad(gameObject); // Optional: if you want it to persist across scene loads

        // Initialize simulation time
        currentSimTime = startTimeHour;
        simulationDay = 1; // Start on day 1
    }
    
    private void Start()
    {
        // Setup building if not already assigned
        if (buildingGenerator == null)
        {
            buildingGenerator = FindObjectOfType<EnhancedBuildingGenerator>();
            
            if (buildingGenerator == null)
            {
                Debug.LogError("SimplifiedSimulationManager: No EnhancedBuildingGenerator found in the scene, and none assigned. Please assign or add one.");
                enabled = false; // Disable manager if no building generator
                return;
            }
        }
        
        // Begin simulation time advancement
        StartCoroutine(RunSimulationTime());
        
        // Verify simulation setup after a short delay to allow other objects to initialize
        StartCoroutine(VerifySimulationSetupAfterDelay());
    }
    
    private void Update()
    {
        // --- Key press for toggling Person AI labels ---
        if (Input.GetKeyDown(KeyCode.L)) 
        {
            EnhancedPerson.DisplayAllLabels = !EnhancedPerson.DisplayAllLabels;
            Debug.Log("Person AI Labels Toggled: " + (EnhancedPerson.DisplayAllLabels ? "ON" : "OFF"));
        }
        // --- End of label toggle ---

        // Test key for forcing an elevator move
        if (Input.GetKeyDown(KeyCode.T)) {
            Debug.Log("Test key 'T' pressed - forcing an elevator move.");
            Elevator[] elevators = FindObjectsOfType<Elevator>();
            if (elevators.Length > 0 && elevators[0] != null) { // Check if any elevator exists and the first one is not null
                int targetFloor = elevators[0].CurrentFloor == 0 ? 1 : 0; // Simple toggle
                if (targetFloor > elevators[0].topFloor) targetFloor = elevators[0].topFloor; // Ensure target is valid

                Debug.Log($"Forcing elevator {elevators[0].name} to move from F{elevators[0].CurrentFloor} to F{targetFloor}");
                elevators[0].RequestFloor(targetFloor);
            } else {
                Debug.LogError("No elevators found to test with 'T' key, or first elevator is null.");
            }
        }

        // Simulation pause/speed controls
        if (Input.GetKeyDown(KeyCode.Space))
        {
            pauseSimulation = !pauseSimulation;
            Debug.Log(pauseSimulation ? "Simulation Paused" : "Simulation Resumed");
        }
        
        if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals)) // Equals is often '+' without shift
        {
            simulationSpeed = Mathf.Min(10f, simulationSpeed + 0.5f); // Cap max speed
            Debug.Log($"Simulation Speed: {simulationSpeed:F1}x");
        }
        
        if (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Minus))
        {
            simulationSpeed = Mathf.Max(0.1f, simulationSpeed - 0.5f); // Cap min speed
            Debug.Log($"Simulation Speed: {simulationSpeed:F1}x");
        }
        
        // Regenerate building
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("Regenerating Building via 'R' key...");
            RegenerateBuilding();
        }
        
        // Force some people to use elevators
        if (Input.GetKeyDown(KeyCode.F))
        {
            Debug.Log("Forcing some people to use elevators via 'F' key...");
            ForceSomePeopleToUseElevators();
        }
    }
    
    // Forces a few random people to decide to use elevators
    private void ForceSomePeopleToUseElevators()
    {
        EnhancedPerson[] people = FindObjectsOfType<EnhancedPerson>();
        Elevator[] elevators = FindObjectsOfType<Elevator>(); // To get top floor
        
        if (people.Length > 0 && elevators.Length > 0 && elevators[0] != null)
        {
            int topFloor = elevators[0].topFloor;
            if (topFloor <= 0) {
                Debug.LogWarning("Cannot force elevator usage, only one floor or less defined.");
                return;
            }

            Debug.Log($"Forcing up to 3 people among {people.Length} to use elevators.");
            
            int usageCount = Mathf.Min(3, people.Length); // Force up to 3 people
            for (int i = 0; i < usageCount; i++)
            {
                // CORRECTED: Explicitly use UnityEngine.Random
                EnhancedPerson person = people[UnityEngine.Random.Range(0, people.Length)]; // Pick a random person
                if (person != null)
                {
                    int currentPFloor = person.currentFloor;
                    int targetPFloor;
                    do
                    {
                        // CORRECTED: Explicitly use UnityEngine.Random
                        targetPFloor = UnityEngine.Random.Range(0, topFloor + 1); 
                    } while (targetPFloor == currentPFloor); // Ensure different target floor

                    Debug.Log($"[SimManager] Forcing person {person.name} on F{currentPFloor} to go to F{targetPFloor}");
                    person.GoToFloor(targetPFloor); // Use the public method in EnhancedPerson
                }
            }
        }
        else
        {
            Debug.LogError("No people or no valid elevators found to force elevator usage.");
        }
    }
    
    // Coroutine to manage simulation time progression
    private IEnumerator RunSimulationTime()
    {
        float lastHourFired = Mathf.Floor(currentSimTime);
        int lastDayFired = (int)simulationDay;
        
        while (true)
        {
            if (!pauseSimulation)
            {
                // Calculate how much simulation time has passed this frame
                float realSecondsPerSimulationDay = dayLengthInMinutes * 60f;
                float simulationHoursPerRealSecond = 24f / realSecondsPerSimulationDay;
                
                // Advance simulation time
                currentSimTime += Time.deltaTime * simulationHoursPerRealSecond * simulationSpeed;
                
                // Check for day rollover
                if (currentSimTime >= 24f)
                {
                    currentSimTime -= 24f; // Reset to 00:00 of next day
                    simulationDay++;
                }
                
                // Fire events if hour changed
                if (Mathf.Floor(currentSimTime) != lastHourFired)
                {
                    lastHourFired = Mathf.Floor(currentSimTime);
                    OnHourChanged?.Invoke(currentSimTime); // Pass the exact current time
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"Simulation Time: Day {simulationDay}, Hour: {FormatTime(currentSimTime)}");
                    }
                }
                
                // Fire event if day changed
                if ((int)simulationDay != lastDayFired)
                {
                    lastDayFired = (int)simulationDay;
                    OnDayChanged?.Invoke(lastDayFired);
                    if (showDebugInfo) Debug.Log($"New Day Started: Day {lastDayFired}");
                }
            }
            
            yield return null; // Wait for the next frame
        }
    }
    
    // Helper method to format time (e.g., 8.5 hours -> "08:30")
    public string FormatTime(float timeInHours, bool use12HourFormat = false)
    {
        int hours = Mathf.FloorToInt(timeInHours);
        int minutes = Mathf.FloorToInt((timeInHours - hours) * 60);
        
        if (use12HourFormat)
        {
            string period = hours >= 12 ? "PM" : "AM";
            if (hours == 0) hours = 12; // Midnight case for 12-hour
            else if (hours > 12) hours -= 12; // Convert 13-23 to 1-11 PM
            
            return $"{hours:00}:{minutes:00} {period}";
        }
        else
        {
            return $"{hours:00}:{minutes:00}"; // 24-hour format
        }
    }
    
    // Public method to regenerate the building
    public void RegenerateBuilding()
    {
        if (buildingGenerator != null)
        {
            buildingGenerator.GenerateBuilding(); // Call the generator's method
            Debug.Log("Building regeneration initiated by SimulationManager.");
            debugIssuesLogged = false; // Reset debug flag for new building
            StartCoroutine(VerifySimulationSetupAfterDelay()); // Verify new setup
        }
        else
        {
            Debug.LogError("Cannot regenerate building: BuildingGenerator reference is missing.");
        }
    }
    
    
    // Coroutine to verify simulation setup after a delay
    private IEnumerator VerifySimulationSetupAfterDelay()
    {
        yield return new WaitForSeconds(1.5f); // Wait for objects to initialize
        
        if (debugIssuesLogged && !Application.isEditor) yield break; 
        
        Debug.Log("Verifying simulation setup...");

        // Check elevators
        Elevator[] elevators = FindObjectsOfType<Elevator>();
        if (elevators.Length == 0)
        {
            Debug.LogError("Verification Failed: No elevators found in the scene.");
        }
        else
        {
            Debug.Log($"Verification: Found {elevators.Length} elevators.");
            foreach (Elevator elevator in elevators)
            {
                if (elevator == null) {
                    Debug.LogWarning("Verification: A null elevator reference was found in the scene array.");
                    continue;
                }
                if (!elevator.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning($"Verification: Elevator {elevator.name} is inactive.");
                }
            }
        }
        
        // Check people
        EnhancedPerson[] people = FindObjectsOfType<EnhancedPerson>();
        if (people.Length == 0)
        {
            Debug.LogError("Verification Failed: No people (EnhancedPerson) found in the scene.");
        }
        else
        {
            Debug.Log($"Verification: Found {people.Length} people.");
            foreach (EnhancedPerson person in people)
            {
                 if (person == null) {
                    Debug.LogWarning("Verification: A null person reference was found in the scene array.");
                    continue;
                }
                if (person.availableElevators == null || !person.availableElevators.Any(e => e != null))
                {
                    Debug.LogWarning($"Verification: Person {person.name} has no valid available elevators assigned. Attempting to re-assign.");
                    person.availableElevators = elevators.Where(e => e != null).ToList(); 
                }
            }
        }
        
        if (Application.isEditor) debugIssuesLogged = true; 
    }
    
    // Optional: Test a specific elevator
    private IEnumerator TestSpecificElevator(Elevator elevator)
    {
        if (elevator == null || elevator.topFloor <= 0) yield break;

        int initialFloor = elevator.CurrentFloor;
        int targetFloor = (initialFloor == 0) ? 1 : 0;
        if (targetFloor > elevator.topFloor) targetFloor = elevator.topFloor;
        
        Debug.Log($"[SimManager Test] Testing elevator {elevator.name}: Requesting move from F{initialFloor} to F{targetFloor}");
        elevator.RequestFloor(targetFloor);
        
        yield return new WaitForSeconds(3.0f); 
        
        if (!elevator.IsMoving && elevator.CurrentFloor == initialFloor)
        {
            Debug.LogError($"[SimManager Test] Elevator {elevator.name} did not respond to move request. Status: {elevator.Status}, Doors: {elevator.DoorsOpen}");
        }
        else
        {
            Debug.Log($"[SimManager Test] Elevator {elevator.name} appears to be responding.");
        }
    }
    
    // Optional: Try more forcefully to make an elevator move if it seems stuck
    private IEnumerator AttemptForcefulElevatorMovement(Elevator elevator, int targetFloor)
    {
        if (elevator == null) yield break;
        Debug.LogWarning($"[SimManager Force] Attempting forceful movement for {elevator.name} to F{targetFloor}.");
        for (int i = 0; i < 2; i++) 
        {
            elevator.RequestFloor(targetFloor, false); 
            yield return new WaitForSeconds(1.5f);
            if (elevator.IsMoving || elevator.CurrentFloor == targetFloor) break;
        }
        
        yield return new WaitForSeconds(3.0f);
        if (!elevator.IsMoving && elevator.CurrentFloor != targetFloor)
        {
            Debug.LogError($"[SimManager Force] Elevator {elevator.name} still not moving to F{targetFloor} after forceful attempts.");
        }
    }
}
