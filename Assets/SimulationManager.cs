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
        StartCoroutine(VerifySimulationSetup());
    }
    
    private void Update()
    {
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
            StartCoroutine(VerifySimulationSetup());
        }
    }
    
    // Coroutine to verify simulation setup and debug issues
    private IEnumerator VerifySimulationSetup()
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
        }
        
        debugIssuesLogged = true;
    }
    
    // Test if elevator is responding to requests
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
            Debug.LogError($"Elevator {elevator.name} is not responding to move requests. Check implementation.");
            
            // Attempt to diagnose the issue
            if (elevator.transform.childCount <= 3) // If only cabin and doors
            {
                Debug.Log("No passengers in elevator.");
            }
            
            // Check if the elevator has pending requests
            if (elevator.RequestedFloorsCount == 0)
            {
                Debug.LogError("Elevator has no pending requests. RequestFloor method may have failed.");
            }
            else
            {
                Debug.Log($"Elevator has {elevator.RequestedFloorsCount} pending requests but isn't moving.");
            }
        }
        else
        {
            Debug.Log($"Elevator {elevator.name} is responding to requests correctly.");
        }
    }
}