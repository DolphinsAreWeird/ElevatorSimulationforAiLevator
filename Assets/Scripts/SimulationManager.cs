using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

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
    public EnhancedBuildingGenerator buildingGenerator;
    
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
    public bool IsMorningRush => currentSimTime >= 7f && currentSimTime <= 10f;
    public bool IsLunchTime => currentSimTime >= 11.5f && currentSimTime <= 13.5f;
    public bool IsEveningRush => currentSimTime >= 16f && currentSimTime <= 19f;
    
    // Debug flag to track state
    private bool debugIssuesLogged = false;
    
    private void Awake()
    {
        // Singleton setup
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Initialize simulation time
        currentSimTime = startTimeHour;
        simulationDay = 1;
    }

    [Obsolete]
    private void Start()
    {
        // Setup building if not already assigned
        if (buildingGenerator == null)
        {
            buildingGenerator = FindObjectOfType<EnhancedBuildingGenerator>();
            
            if (buildingGenerator == null)
            {
                Debug.LogWarning("No BuildingGenerator found. Creating a default one.");
                GameObject buildingObj = new GameObject("BuildingGenerator");
                buildingGenerator = buildingObj.AddComponent<EnhancedBuildingGenerator>();
            }
        }
        
        // Begin simulation
        StartCoroutine(RunSimulation());
        
        // Verify simulation setup
        StartCoroutine(VerifySimulationSetupNew());
    }
    
    private void Update()
    {
        // Debug update execution less frequently to avoid log spam
        if (Time.frameCount % 300 == 0) {
            Debug.Log("Update executing for " + gameObject.name);
        }
        
        // FIXED: Test key now properly finds an elevator to test
        if (Input.GetKeyDown(KeyCode.T)) {
            Debug.Log("Test key pressed - forcing elevator move");
            Elevator[] elevators = FindObjectsByType<Elevator>(FindObjectsSortMode.None);
            if (elevators.Length > 0) {
                int randomFloor = elevators[0].CurrentFloor == 0 ? 1 : 0;
                Debug.Log($"Forcing elevator 0 to move to floor {randomFloor}");
                elevators[0].RequestFloor(randomFloor);
            } else {
                Debug.LogError("No elevators found to test!");
            }
        }

        // Simple controls for simulation
        if (Input.GetKeyDown(KeyCode.Space))
        {
            pauseSimulation = !pauseSimulation;
            Debug.Log(pauseSimulation ? "Simulation Paused" : "Simulation Resumed");
        }
        
        if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Plus))
        {
            simulationSpeed += 0.5f;
            Debug.Log($"Simulation Speed: {simulationSpeed}x");
        }
        
        if (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Minus))
        {
            simulationSpeed = Mathf.Max(0.1f, simulationSpeed - 0.5f);
            Debug.Log($"Simulation Speed: {simulationSpeed}x");
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            RegenerateBuilding();
        }
        
        // NEW: Add F key to force all people to use elevators
        if (Input.GetKeyDown(KeyCode.F))
        {
            // ForceElevatorUsage(); // Removed obsolete method call
        }
    }

    // NEW: Force all people to use elevators
    [Obsolete]
    private void ForceElevatorUsage()
    {
        EnhancedPerson[] people = FindObjectsByType<EnhancedPerson>(FindObjectsSortMode.None);
        Elevator[] elevators = FindObjectsOfType<Elevator>();
        
        if (people.Length > 0 && elevators.Length > 0)
        {
            Debug.Log($"Forcing {people.Length} people to use elevators");
            
            // Make a few random people use elevators
            int usageCount = Mathf.Min(5, people.Length);
            for (int i = 0; i < usageCount; i++)
            {
                EnhancedPerson person = people[i];
                int currentFloor = person.currentFloor;
                int targetFloor = (currentFloor + 1) % (elevators[0].topFloor + 1);
                
                Debug.Log($"Forcing person {person.name} to go from floor {currentFloor} to {targetFloor}");
                person.GoToFloor(targetFloor);
            }
        }
        else
        {
            Debug.LogError("No people or elevators found to force usage!");
        }
    }
    
    private IEnumerator RunSimulation()
    {
        float lastHour = currentSimTime;
        int lastDay = (int)simulationDay;
        
        while (true)
        {
            if (!pauseSimulation)
            {
                // Calculate how much simulation time has passed
                float realSecondsPerDay = dayLengthInMinutes * 60f;
                float hoursPerRealSecond = 24f / realSecondsPerDay;
                
                // Advance simulation time
                currentSimTime += Time.deltaTime * hoursPerRealSecond * simulationSpeed;
                
                // Check for day rollover
                if (currentSimTime >= 24f)
                {
                    currentSimTime -= 24f;
                    simulationDay += 1;
                }
                
                // Adjust global time scale if needed
                Time.timeScale = simulationSpeed;
                
                // Fire events if hour changed
                int currentHourInt = Mathf.FloorToInt(currentSimTime);
                int lastHourInt = Mathf.FloorToInt(lastHour);
                
                if (currentHourInt != lastHourInt)
                {
                    OnHourChanged?.Invoke(currentSimTime);
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"Simulation Time: Day {simulationDay}, {FormatTime(currentSimTime)}");
                    }
                }
                
                // Fire event if day changed
                if ((int)simulationDay != lastDay)
                {
                    OnDayChanged?.Invoke((int)simulationDay);
                    lastDay = (int)simulationDay;
                }
                
                lastHour = currentSimTime;
            }
            
            yield return null;
        }
    }
    
    // Helper method to format time (24-hour format with options for AM/PM)
    public string FormatTime(float timeInHours, bool use12HourFormat = false)
    {
        int hours = Mathf.FloorToInt(timeInHours);
        int minutes = Mathf.FloorToInt((timeInHours - hours) * 60);
        
        if (use12HourFormat)
        {
            string period = hours >= 12 ? "PM" : "AM";
            int displayHours = hours % 12;
            if (displayHours == 0) displayHours = 12;
            
            return $"{displayHours:00}:{minutes:00} {period}";
        }
        else
        {
            return $"{hours:00}:{minutes:00}";
        }
    }
    
    // Public method to regenerate the building
    public void RegenerateBuilding()
    {
        if (buildingGenerator != null)
        {
            // Generate new building
            buildingGenerator.GenerateBuilding();
            
            Debug.Log("Building regenerated");
            
            // Reset debug flags
            debugIssuesLogged = false;
            
            // Verify setup again
            // StartCoroutine(VerifySimulationSetupNew()); // Removed obsolete method call
        }
    }


    // IMPROVED: Better testing of elevator functionality
    [Obsolete]
    private IEnumerator VerifySimulationSetupNew()
    {
        // Wait a bit to let everything initialize
        yield return new WaitForSeconds(1.0f);
        
        if (debugIssuesLogged) yield break;
        
        // Check elevators
        Elevator[] elevators = FindObjectsOfType<Elevator>();
        if (elevators.Length == 0)
        {
            Debug.LogError("No elevators found in the scene. The building generator may have failed to create them.");
        }
        else
        {
            Debug.Log($"Found {elevators.Length} elevators in the scene.");
            
            // Check if elevators are working
            foreach (Elevator elevator in elevators)
            {
                if (elevator.gameObject.activeSelf == false)
                {
                    Debug.LogWarning($"Elevator {elevator.name} is inactive.");
                }
                
                // Test elevator by requesting it to move
                StartCoroutine(TestElevator(elevator));
            }
        }
        
        // Check people
        EnhancedPerson[] people = FindObjectsOfType<EnhancedPerson>();
        if (people.Length == 0)
        {
            Debug.LogError("No people found in the scene. The building generator may have failed to create them.");
        }
        else
        {
            Debug.Log($"Found {people.Length} people in the scene.");
            
            // Verify people setup
            foreach (EnhancedPerson person in people)
            {
                if (person.availableElevators == null || person.availableElevators.Count == 0)
                {
                    Debug.LogWarning($"Person {person.name} has no available elevators assigned.");
                    
                    // Auto-fix: Assign all elevators
                    person.availableElevators = new List<Elevator>(elevators);
                }
            }
            
            // Force a few people to use elevators as a test
            yield return new WaitForSeconds(2.0f);
            // Removed obsolete ForceElevatorUsage() call to fix compile error
        }
        
        debugIssuesLogged = true;
    }
    
    // IMPROVED: More thorough elevator testing
    private IEnumerator TestElevator(Elevator elevator)
    {
        // Remember initial floor
        int initialFloor = elevator.CurrentFloor;
        
        // Find a different floor to go to
        int targetFloor = initialFloor == 0 ? 1 : 0;
        if (targetFloor > elevator.topFloor) targetFloor = elevator.topFloor;
        
        Debug.Log($"Testing elevator {elevator.name}: Requesting move from floor {initialFloor} to {targetFloor}");
        
        // Request the elevator to move
        elevator.RequestFloor(targetFloor);
        
        // Wait a bit and check if it started moving
        yield return new WaitForSeconds(3.0f);
        
        if (!elevator.IsMoving && elevator.CurrentFloor == initialFloor)
        {
            Debug.LogError($"Elevator {elevator.name} is not responding to move requests. Trying to diagnose...");
            
            // More detailed diagnostics
            Debug.Log($"Elevator position: {elevator.transform.position}");
            Debug.Log($"Elevator status: {elevator.Status}");
            Debug.Log($"Elevator doors open: {elevator.DoorsOpen}");
            
            // Attempt forceful movement
            Debug.Log("Attempting forceful movement...");
            StartCoroutine(ForcefulElevatorMovement(elevator, targetFloor));
        }
        else
        {
            Debug.Log($"Elevator {elevator.name} is responding to requests correctly.");
        }
    }
    
    // NEW: Try more forcefully to make an elevator move
    private IEnumerator ForcefulElevatorMovement(Elevator elevator, int targetFloor)
    {
        // Try requesting the floor multiple times
        for (int i = 0; i < 3; i++)
        {
            Debug.Log($"Forceful attempt {i+1} to move elevator to floor {targetFloor}");
            elevator.RequestFloor(targetFloor);
            yield return new WaitForSeconds(1.0f);
        }
        
        // Wait longer to see if it moves
        yield return new WaitForSeconds(5.0f);
        
        if (!elevator.IsMoving)
        {
            Debug.LogError("Elevator still not moving after forceful attempts. Possible code issue in Elevator.cs");
        }
    }
}