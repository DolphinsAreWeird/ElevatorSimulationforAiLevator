using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages socket communication between the elevator simulation and an external server.
/// Works alongside the existing SimplifiedSimulationManager.
/// </summary>
public class SocketManager : MonoBehaviour
{
    [Header("Socket Connection Settings")]
    [SerializeField] public string serverAddress = "localhost";
    [SerializeField] public int serverPort = 8000;
    [SerializeField] private int bufferSize = 16384;
    [SerializeField] private float reconnectInterval = 5f;
    [SerializeField] private bool autoConnect = true;
    [SerializeField] private float updateInterval = 1.0f; // How often to send state updates

    [Header("Fail-Safe Settings")]
    [SerializeField] private bool continueWithoutConnection = true;
    [SerializeField] private bool suppressConnectionErrors = true;
    [SerializeField] private int maxConnectionAttempts = 3;

    [Header("Debug Settings")]
    [SerializeField] private bool logMessages = true;
    [SerializeField] private bool logHeartbeats = false;
    [SerializeField] private bool captureElevatorMovements = true; // New setting to control elevator movement logging

    // Internal connection state
    private TcpClient client;
    private NetworkStream stream;
    private bool _isConnected = false;
    private bool isConnecting = false;
    private CancellationTokenSource cancellationToken;
    private int connectionAttempts = 0;

    // References to simulation components
    private SimplifiedSimulationManager simulationManager;
    private List<Elevator> elevators = new List<Elevator>();
    private List<EnhancedPerson> people = new List<EnhancedPerson>();
    
    // Queue for logging to avoid network bottlenecks
    private Queue<string> logQueue = new Queue<string>();
    private bool isProcessingLogs = false;

    // Singleton pattern
    private static SocketManager _instance;
    public static SocketManager Instance => _instance;

    // Public properties
    public bool IsConnected => _isConnected;

    // Events
    public event Action<bool> OnConnectionStatusChanged;
    public event Action<string> OnMessageReceived;

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

        // Initialize cancellation token
        cancellationToken = new CancellationTokenSource();
        
        // Add a LogInterceptor component if it doesn't exist and we want to capture elevator movements
        if (captureElevatorMovements && GetComponent<LogInterceptor>() == null)
        {
            gameObject.AddComponent<LogInterceptor>();
        }
    }

    private void Start()
    {
        // Find the simulation manager
        simulationManager = SimplifiedSimulationManager.Instance;
        if (simulationManager == null)
        {
            simulationManager = FindObjectOfType<SimplifiedSimulationManager>();
            if (simulationManager == null)
            {
                Debug.LogWarning("No SimplifiedSimulationManager found in the scene. Socket communication may not work properly.");
            }
        }

        // Subscribe to simulation events
        if (simulationManager != null)
        {
            simulationManager.OnHourChanged += OnSimulationHourChanged;
            simulationManager.OnDayChanged += OnSimulationDayChanged;
        }

        // Start connection if auto-connect is enabled
        if (autoConnect)
        {
            Connect();
        }

        // Start periodic update routines
        InvokeRepeating(nameof(SendSimulationState), updateInterval, updateInterval);
        InvokeRepeating(nameof(SendHeartbeat), 5f, 5f);
        
        // Start finding simulation components
        InvokeRepeating(nameof(FindSimulationComponents), 0.5f, 5f);
        
        // Start log processing
        if (captureElevatorMovements)
        {
            InvokeRepeating(nameof(ProcessLogQueue), 0.1f, 0.1f);
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (simulationManager != null)
        {
            simulationManager.OnHourChanged -= OnSimulationHourChanged;
            simulationManager.OnDayChanged -= OnSimulationDayChanged;
        }

        // Clean up
        Disconnect();
        cancellationToken.Cancel();
        CancelInvoke();
    }

    /// <summary>
    /// Find all relevant simulation components in the scene
    /// </summary>
    private void FindSimulationComponents()
    {
        // Find all elevators
        Elevator[] foundElevators = FindObjectsOfType<Elevator>();
        if (foundElevators.Length > 0)
        {
            elevators.Clear();
            elevators.AddRange(foundElevators);
            
            if (logMessages)
                Debug.Log($"SocketManager: Found {elevators.Count} elevators");
        }

        // Find all people
        EnhancedPerson[] foundPeople = FindObjectsOfType<EnhancedPerson>();
        if (foundPeople.Length > 0)
        {
            people.Clear();
            people.AddRange(foundPeople);
            
            if (logMessages)
                Debug.Log($"SocketManager: Found {people.Count} people");
        }
    }

    /// <summary>
    /// Connect to the socket server
    /// </summary>
    public async void Connect()
    {
        if (_isConnected || isConnecting)
            return;

        isConnecting = true;
        connectionAttempts++;

        try
        {
            Debug.Log($"Connecting to socket server at {serverAddress}:{serverPort}... (Attempt {connectionAttempts}/{maxConnectionAttempts})");
            client = new TcpClient();
            
            // Connect with timeout
            var connectTask = client.ConnectAsync(serverAddress, serverPort);
            await Task.WhenAny(connectTask, Task.Delay(5000)); // 5 second timeout

            if (!client.Connected)
            {
                HandleConnectionFailure("Connection timeout");
                return;
            }

            stream = client.GetStream();
            _isConnected = true;
            isConnecting = false;
            connectionAttempts = 0; // Reset counter on successful connection
            
            Debug.Log("Connected to socket server successfully");
            OnConnectionStatusChanged?.Invoke(true);
            
            // Start receiving messages
            _ = ReceiveMessages();
        }
        catch (Exception e)
        {
            HandleConnectionFailure($"Failed to connect to socket server: {e.Message}");
        }
    }

    /// <summary>
    /// Handle connection failure with appropriate logging and reconnection logic
    /// </summary>
    private void HandleConnectionFailure(string message)
    {
        if (!suppressConnectionErrors)
            Debug.LogWarning(message);
        else
            Debug.Log(message + " - Continuing in standalone mode");
            
        _isConnected = false;
        isConnecting = false;
        OnConnectionStatusChanged?.Invoke(false);
        
        // Decide if we should try to reconnect
        if (!continueWithoutConnection && connectionAttempts < maxConnectionAttempts)
        {
            Invoke(nameof(Connect), reconnectInterval);
        }
        else if (connectionAttempts >= maxConnectionAttempts)
        {
            Debug.Log("Maximum connection attempts reached. Running in standalone mode.");
            // Reset counter for future connection attempts
            connectionAttempts = 0;
        }
    }

    /// <summary>
    /// Disconnect from the socket server
    /// </summary>
    public void Disconnect()
    {
        if (!_isConnected)
            return;

        _isConnected = false;
        stream?.Close();
        client?.Close();
        OnConnectionStatusChanged?.Invoke(false);
        Debug.Log("Disconnected from socket server");
    }

    /// <summary>
    /// Send a message to the socket server
    /// </summary>
    public async Task<bool> SendMessage(string message)
    {
        if (!_isConnected)
        {
            if (logMessages && !message.StartsWith("HEARTBEAT") && !suppressConnectionErrors) // Don't log heartbeat failures
                Debug.LogWarning("Cannot send message: Not connected to server");
            return false;
        }

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(data, 0, data.Length, cancellationToken.Token);
            
            if (logMessages && (!message.StartsWith("HEARTBEAT") || logHeartbeats))
                Debug.Log($"Sent message: {message}");
                
            return true;
        }
        catch (Exception e)
        {
            if (logMessages && !message.StartsWith("HEARTBEAT") && !suppressConnectionErrors) // Don't log heartbeat failures
                Debug.LogError($"Error sending message: {e.Message}");
                
            _isConnected = false;
            OnConnectionStatusChanged?.Invoke(false);
            
            // Try to reconnect if needed
            if (!continueWithoutConnection && connectionAttempts < maxConnectionAttempts)
            {
                Invoke(nameof(Connect), reconnectInterval);
            }
            return false;
        }
    }

    /// <summary>
    /// Continuously receive messages from the server
    /// </summary>
    private async Task ReceiveMessages()
    {
        byte[] buffer = new byte[bufferSize];

        while (_isConnected && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken.Token);
                
                if (bytesRead > 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    
                    if (logMessages)
                        Debug.Log($"Received message: {message}");
                    
                    // Process the message on the main thread
                    ProcessMessageOnMainThread(message);
                }
                else
                {
                    // Server closed the connection
                    Debug.Log("Server closed the connection");
                    _isConnected = false;
                    OnConnectionStatusChanged?.Invoke(false);
                    
                    // Try to reconnect if needed
                    if (!continueWithoutConnection && connectionAttempts < maxConnectionAttempts)
                    {
                        Invoke(nameof(Connect), reconnectInterval);
                    }
                    break;
                }
            }
            catch (Exception e)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    if (!suppressConnectionErrors)
                        Debug.LogError($"Error receiving message: {e.Message}");
                    
                    _isConnected = false;
                    OnConnectionStatusChanged?.Invoke(false);
                    
                    // Try to reconnect if needed
                    if (!continueWithoutConnection && connectionAttempts < maxConnectionAttempts)
                    {
                        Invoke(nameof(Connect), reconnectInterval);
                    }
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Process a message on the main thread
    /// </summary>
    private void ProcessMessageOnMainThread(string message)
    {
        // Invoke on main thread
        Dispatcher.RunOnMainThread(() => {
            OnMessageReceived?.Invoke(message);
            ProcessMessage(message);
        });
    }

    /// <summary>
    /// Process the received message
    /// </summary>
    private void ProcessMessage(string message)
    {
        try
        {
            // Split message into command and parameters
            string[] parts = message.Split(':');
            if (parts.Length < 2)
                return;

            string command = parts[0].Trim().ToUpper();
            string parametersString = parts[1].Trim();
            string[] parameters = parametersString.Split(',');

            switch (command)
            {
                case "ELEVATOR_MOVE":
                    if (parameters.Length > 0 && int.TryParse(parameters[0], out int floor))
                    {
                        MoveElevator(floor);
                    }
                    break;
                
                case "SPAWN_PERSON":
                    if (parameters.Length > 1 && 
                        int.TryParse(parameters[0], out int startFloor) && 
                        int.TryParse(parameters[1], out int targetFloor))
                    {
                        SpawnPerson(startFloor, targetFloor);
                    }
                    break;
                
                case "SET_SIMULATION_SPEED":
                    if (parameters.Length > 0 && float.TryParse(parameters[0], out float speed))
                    {
                        SetSimulationSpeed(speed);
                    }
                    break;
                    
                case "PAUSE_SIMULATION":
                    if (parameters.Length > 0 && bool.TryParse(parameters[0], out bool pause))
                    {
                        PauseSimulation(pause);
                    }
                    break;
                    
                case "REGENERATE_BUILDING":
                    RegenerateBuilding();
                    break;
                
                case "QUERY":
                    HandleQuery(parametersString);
                    break;
                
                default:
                    Debug.Log($"Unknown command: {command}");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing message: {e.Message}");
        }
    }

    /// <summary>
    /// Handle query commands from the server
    /// </summary>
    private void HandleQuery(string queryType)
    {
        switch (queryType.ToUpper())
        {
            case "STATE":
                SendSimulationState();
                break;
                
            case "ELEVATORS":
                SendElevatorsState();
                break;
                
            case "PEOPLE":
                SendPeopleState();
                break;
                
            default:
                Debug.Log($"Unknown query type: {queryType}");
                break;
        }
    }

    /// <summary>
    /// Move an elevator to a specific floor
    /// </summary>
    private void MoveElevator(int floor)
    {
        if (elevators.Count == 0)
        {
            Debug.LogError("No elevators found to move");
            return;
        }

        // Find the elevator closest to the requested floor
        Elevator closestElevator = elevators[0];
        int minDistance = Mathf.Abs(closestElevator.CurrentFloor - floor);

        foreach (Elevator elevator in elevators)
        {
            int distance = Mathf.Abs(elevator.CurrentFloor - floor);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestElevator = elevator;
            }
        }

        // Request the elevator to move
        closestElevator.RequestFloor(floor);
        Debug.Log($"Requested elevator {closestElevator.name} to move to floor {floor}");
    }

    /// <summary>
    /// Spawn a person at a specific floor with a target floor
    /// </summary>
    private void SpawnPerson(int startFloor, int targetFloor)
    {
        // Check if buildingGenerator is available
        if (simulationManager == null || simulationManager.buildingGenerator == null)
        {
            Debug.LogError("Building generator not found");
            return;
        }

        // Validate floors
        int maxFloor = 0;
        if (elevators.Count > 0)
        {
            maxFloor = elevators[0].topFloor;
        }
        else if (people.Count > 0)
        {
            // Try to get max floor from the building generator
            maxFloor = simulationManager.buildingGenerator.numberOfFloors - 1;
        }

        if (startFloor < 0 || startFloor > maxFloor || targetFloor < 0 || targetFloor > maxFloor || startFloor == targetFloor)
        {
            Debug.LogError($"Invalid floor numbers: start={startFloor}, target={targetFloor}, max={maxFloor}");
            return;
        }

        // Try to find the person prefab
        if (simulationManager.buildingGenerator is EnhancedBuildingGenerator)
        {
            EnhancedBuildingGenerator generator = (EnhancedBuildingGenerator)simulationManager.buildingGenerator;
            
            // Check if we can create a person
            if (generator.personPrefab != null)
            {
                // Calculate spawn position (adjust based on your scene)
                float floorHeight = generator.floorHeight;
                Vector3 spawnPosition = new Vector3(
                    UnityEngine.Random.Range(-2f, 2f),
                    startFloor * floorHeight + 0.5f,
                    UnityEngine.Random.Range(-2f, 2f)
                );

                // Instantiate person
                GameObject personObj = Instantiate(generator.personPrefab, spawnPosition, Quaternion.identity);
                EnhancedPerson person = personObj.GetComponent<EnhancedPerson>();
                
                if (person != null)
                {
                    // Setup person
                    person.currentFloor = startFloor;
                    
                    // Add to list
                    people.Add(person);
                    
                    // Give the person all available elevators
                    person.availableElevators = new List<Elevator>(elevators);
                    
                    // Set target floor and make them move
                    person.GoToFloor(targetFloor);
                    
                    Debug.Log($"Spawned person at floor {startFloor} with target floor {targetFloor}");
                }
                else
                {
                    Debug.LogError("Spawned object doesn't have EnhancedPerson component");
                    Destroy(personObj);
                }
            }
            else
            {
                Debug.LogError("Person prefab not found in building generator");
            }
        }
        else
        {
            Debug.LogError("Building generator is not an EnhancedBuildingGenerator");
        }
    }

    /// <summary>
    /// Set the simulation speed
    /// </summary>
    private void SetSimulationSpeed(float speed)
    {
        if (simulationManager != null)
        {
            simulationManager.simulationSpeed = Mathf.Clamp(speed, 0.1f, 10f);
            Debug.Log($"Set simulation speed to {simulationManager.simulationSpeed}x");
        }
    }

    /// <summary>
    /// Pause or resume the simulation
    /// </summary>
    private void PauseSimulation(bool pause)
    {
        if (simulationManager != null)
        {
            simulationManager.pauseSimulation = pause;
            Debug.Log(pause ? "Simulation paused" : "Simulation resumed");
        }
    }

    /// <summary>
    /// Regenerate the building
    /// </summary>
    private void RegenerateBuilding()
    {
        if (simulationManager != null)
        {
            simulationManager.RegenerateBuilding();
            Debug.Log("Regenerated building");
            
            // Re-find all components
            FindSimulationComponents();
        }
    }

    /// <summary>
    /// Send the current simulation state to the server
    /// </summary>
    private void SendSimulationState()
    {
        if (!_isConnected) return;

        try
        {
            Dictionary<string, object> state = new Dictionary<string, object>();
            
            // Add simulation time info
            if (simulationManager != null)
            {
                state["day"] = simulationManager.CurrentDay;
                state["hour"] = simulationManager.CurrentHour;
                state["timeFormatted"] = simulationManager.FormatTime(simulationManager.CurrentHour, true);
                state["speed"] = simulationManager.simulationSpeed;
                state["paused"] = simulationManager.pauseSimulation;
                state["isMorningRush"] = simulationManager.IsMorningRush;
                state["isLunchTime"] = simulationManager.IsLunchTime;
                state["isEveningRush"] = simulationManager.IsEveningRush;
            }
            
            // Add counts
            state["elevatorCount"] = elevators.Count;
            state["peopleCount"] = people.Count;
            
            // Convert to JSON
            string json = JsonUtility.ToJson(new SystemState { state = state });
            
            // Send the state
            _ = SendMessage($"STATE:{json}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending simulation state: {e.Message}");
        }
    }

    /// <summary>
    /// Send the current state of all elevators
    /// </summary>
    private void SendElevatorsState()
    {
        if (!_isConnected) return;

        try
        {
            List<Dictionary<string, object>> elevatorsData = new List<Dictionary<string, object>>();
            
            foreach (Elevator elevator in elevators)
            {
                Dictionary<string, object> elevatorState = new Dictionary<string, object>();
                elevatorState["id"] = elevator.GetInstanceID();
                elevatorState["name"] = elevator.name;
                elevatorState["currentFloor"] = elevator.CurrentFloor;
                elevatorState["topFloor"] = elevator.topFloor;
                elevatorState["isMoving"] = elevator.IsMoving;
                elevatorState["doorsOpen"] = elevator.DoorsOpen;
                elevatorState["passengerCount"] = elevator.transform.childCount;
                elevatorState["status"] = elevator.Status;
                
                elevatorsData.Add(elevatorState);
            }
            
            // Convert to JSON
            string json = JsonUtility.ToJson(new ElevatorsState { elevators = elevatorsData });
            
            // Send the state
            _ = SendMessage($"ELEVATORS:{json}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending elevators state: {e.Message}");
        }
    }

    /// <summary>
    /// Send the current state of all people
    /// </summary>
    private void SendPeopleState()
    {
        if (!_isConnected) return;

        try
        {
            List<Dictionary<string, object>> peopleData = new List<Dictionary<string, object>>();
            
            foreach (EnhancedPerson person in people)
            {
                Dictionary<string, object> personState = new Dictionary<string, object>();
                personState["id"] = person.GetInstanceID();
                personState["name"] = person.name;
                personState["currentFloor"] = person.currentFloor;
                personState["targetFloor"] = person.targetFloor;
                personState["inElevator"] = person.transform.parent != null && person.transform.parent.GetComponent<Elevator>() != null;
                personState["position"] = new float[] { person.transform.position.x, person.transform.position.y, person.transform.position.z };
                
                peopleData.Add(personState);
            }
            
            // Convert to JSON
            string json = JsonUtility.ToJson(new PeopleState { people = peopleData });
            
            // Send the state
            _ = SendMessage($"PEOPLE:{json}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending people state: {e.Message}");
        }
    }

    /// <summary>
    /// Called when the simulation hour changes
    /// </summary>
    private void OnSimulationHourChanged(float hour)
    {
        if (!_isConnected) return;
        
        // Send a message with the new hour
        _ = SendMessage($"HOUR_CHANGED:{hour}");
    }

    /// <summary>
    /// Called when the simulation day changes
    /// </summary>
    private void OnSimulationDayChanged(int day)
    {
        if (!_isConnected) return;
        
        // Send a message with the new day
        _ = SendMessage($"DAY_CHANGED:{day}");
    }

    /// <summary>
    /// Send a heartbeat to keep the connection alive
    /// </summary>
    private void SendHeartbeat()
    {
        if (!_isConnected) return;
        
        _ = SendMessage("HEARTBEAT");
    }

    /// <summary>
    /// Log elevator movement to the server
    /// </summary>
    public void LogElevatorMovement(string logMessage)
    {
        if (!captureElevatorMovements) return;
        
        // Add to queue rather than sending immediately to avoid network congestion
        lock (logQueue)
        {
            logQueue.Enqueue(logMessage);
        }
    }
    
    /// <summary>
    /// Process queued log messages
    /// </summary>
    private async void ProcessLogQueue()
    {
        if (!_isConnected || isProcessingLogs) return;
        
        isProcessingLogs = true;
        
        try
        {
            // Process up to 10 logs at once to avoid flooding
            for (int i = 0; i < 10; i++)
            {
                string logMessage = null;
                
                lock (logQueue)
                {
                    if (logQueue.Count > 0)
                    {
                        logMessage = logQueue.Dequeue();
                    }
                }
                
                if (logMessage == null)
                    break;
                
                await SendMessage($"ELEVATOR_LOG:{logMessage}");
                
                // Small delay to prevent flooding the network
                await Task.Delay(10);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing log queue: {e.Message}");
        }
        finally
        {
            isProcessingLogs = false;
        }
    }

    // Helper classes for JSON serialization
    [Serializable]
    private class SystemState
    {
        public Dictionary<string, object> state;
    }

    [Serializable]
    private class ElevatorsState
    {
        public List<Dictionary<string, object>> elevators;
    }

    [Serializable]
    private class PeopleState
    {
        public List<Dictionary<string, object>> people;
    }
}