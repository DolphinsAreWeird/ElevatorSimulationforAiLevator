using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Helper class for executing code on the Unity main thread
/// </summary>
public class Dispatcher : MonoBehaviour
{
    private static Dispatcher _instance;
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();
    private static readonly object _lock = new object();

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        lock (_lock)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }

    /// <summary>
    /// Run an action on the main thread
    /// </summary>
    public static void RunOnMainThread(Action action)
    {
        if (action == null)
        {
            Debug.LogError("Action cannot be null");
            return;
        }

        // If we're already on the main thread, just execute it
        if (Application.isPlaying && _instance != null && System.Threading.Thread.CurrentThread.ManagedThreadId == 1)
        {
            action();
            return;
        }

        // Otherwise queue it
        lock (_lock)
        {
            _executionQueue.Enqueue(action);
        }
    }

    /// <summary>
    /// Run an action on the main thread and await its completion
    /// </summary>
    public static Task RunOnMainThreadAsync(Action action)
    {
        var tcs = new TaskCompletionSource<bool>();
        
        if (action == null)
        {
            Debug.LogError("Action cannot be null");
            tcs.SetResult(false);
            return tcs.Task;
        }

        RunOnMainThread(() =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    /// <summary>
    /// Ensures the dispatcher instance exists
    /// </summary>
    public static void EnsureInstance()
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("Dispatcher");
            _instance = go.AddComponent<Dispatcher>();
            DontDestroyOnLoad(go);
        }
    }
    
    /// <summary>
    /// Check if the dispatcher instance exists
    /// </summary>
    public static bool HasInstance()
    {
        return _instance != null;
    }
}