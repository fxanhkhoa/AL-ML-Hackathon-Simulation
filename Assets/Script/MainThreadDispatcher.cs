using System.Collections.Generic;
using UnityEngine;
using System; // For Action

/// <summary>
/// A simple dispatcher to queue actions to be executed on Unity's main thread.
/// </summary>
public class MainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();
    private static MainThreadDispatcher _instance;
    private static bool _initialized = false;

    /// <summary>
    /// Gets the singleton instance of the MainThreadDispatcher.
    /// Creates one if it doesn't exist in the scene.
    /// </summary>
    public static MainThreadDispatcher Instance()
    {
        if (_instance == null && !_initialized)
        {
            _instance = FindObjectOfType<MainThreadDispatcher>();
            if (_instance == null)
            {
                GameObject obj = new GameObject("MainThreadDispatcher");
                _instance = obj.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad(obj); // Persist across scene loads
            }
            _initialized = true;
        }
        return _instance;
    }

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _initialized = true;
        }
        else if (_instance != this)
        {
            Destroy(gameObject); // Ensure only one instance
        }
    }

    /// <summary>
    /// Enqueues an action to be executed on the main thread in the next Update cycle.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public void Enqueue(Action action)
    {
        if (action == null)
        {
            Debug.LogWarning("Attempted to enqueue a null action.");
            return;
        }

        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                Action action = _executionQueue.Dequeue();
                try
                {
                    action?.Invoke(); // Use null-conditional operator for safety
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error executing enqueued action: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }

    void OnApplicationQuit()
    {
        _initialized = false;
        _instance = null;
        lock (_executionQueue)
        {
            _executionQueue.Clear();
        }
    }
}