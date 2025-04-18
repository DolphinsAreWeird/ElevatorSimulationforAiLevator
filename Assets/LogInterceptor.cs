using System;
using UnityEngine;

public class LogInterceptor : MonoBehaviour
{
    private void OnEnable()
    {
        // Register callback when enabled
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable()
    {
        // Unregister callback when disabled
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Check if this is an elevator movement log
        if (logString.Contains("Elevator") && logString.Contains("moving:"))
        {
            // Forward the log to the socket manager to be sent to the server
            SocketManager.Instance?.LogElevatorMovement(logString);
        }
    }
}