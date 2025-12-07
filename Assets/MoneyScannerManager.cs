using System;
using System.Collections.Generic;
using UnityEngine;
using Vuforia;
using UnityEngine.UI;   // for Button & Toggle

public class MoneyScannerManager : MonoBehaviour
{
    [Header("UI (drag & drop)")]
    public UnityEngine.UI.Text detectedText;
    public UnityEngine.UI.Text totalText;
    public UnityEngine.UI.Button addButton;
    public UnityEngine.UI.Button clearButton;
    public UnityEngine.UI.Toggle autoAddToggle;
    public AudioSource addSound;

    [Header("Settings")]
    [Tooltip("Cooldown in seconds to avoid double-add when a target stays tracked")]
    public float addCooldownSeconds = 0.6f;

    // Map Vuforia Image Target names -> numeric value
    private Dictionary<string, float> denominationMap = new Dictionary<string, float>()
    {
        // CHANGE THESE KEYS to match your Image Target names exactly
        { "Carte_5dt", 5f },
        { "Carte_10DT", 10f },
        { "Carte_10DT_V2", 10f },
        { "Carte_20dt", 20f },
        { "Carte_50dt", 50f },
        // add more as needed
    };

    // runtime state
    private string currentDetectedName = "";
    private float currentDetectedValue = 0f;
    private float runningTotal = 0f;

    // simple history (optional)
    private List<(string name, float value, DateTime when)> history = new List<(string, float, DateTime)>();

    // cooldown tracking: map targetName -> last added time
    private Dictionary<string, float> lastAddedAt = new Dictionary<string, float>();

    void Start()
    {
        // Initialize UI
        UpdateDetectedUI();
        UpdateTotalUI();

        // Wire buttons (also you can set these in the Inspector)
        if (addButton != null) addButton.onClick.AddListener(OnAddButton);
        if (clearButton != null) clearButton.onClick.AddListener(OnClearButton);

        // Subscribe to all Vuforia observers in the scene
        var observers = FindObjectsOfType<ObserverBehaviour>();
        foreach (var ob in observers)
        {
            ob.OnTargetStatusChanged += OnTargetStatusChanged;
        }

    }

    private void OnDestroy()
    {
        // Unsubscribe
        var observers = FindObjectsOfType<ObserverBehaviour>();
        foreach (var ob in observers)
        {
            ob.OnTargetStatusChanged -= OnTargetStatusChanged;
        }
    }

    // Called whenever a target's status changes
    private void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus targetStatus)
    {
        var status = targetStatus.Status;

        // consider tracked when TRACKED or EXTENDED_TRACKED
        if (status == Status.TRACKED || status == Status.EXTENDED_TRACKED)
        {
            string targetName = behaviour.TargetName;
            if (denominationMap.ContainsKey(targetName))
            {
                currentDetectedName = targetName;
                currentDetectedValue = denominationMap[targetName];
                UpdateDetectedUI();

                // Auto-add if toggle is on and cooldown allows
                if (autoAddToggle != null && autoAddToggle.isOn)
                {
                    TryAutoAdd(targetName, currentDetectedValue);
                }
            }
            else
            {
                // unknown target (maybe you forgot to add to the map)
                currentDetectedName = targetName + " (unknown)";
                currentDetectedValue = 0f;
                UpdateDetectedUI();
            }
        }
        else
        {
            // Lost tracking — clear current detection (but keep running total)
            currentDetectedName = "";
            currentDetectedValue = 0f;
            UpdateDetectedUI();
        }
    }

    private void TryAutoAdd(string targetName, float value)
    {
        float now = Time.time;
        if (!lastAddedAt.ContainsKey(targetName) || (now - lastAddedAt[targetName]) >= addCooldownSeconds)
        {
            AddCurrentDetected();
            lastAddedAt[targetName] = now;
        }
    }

    private void UpdateDetectedUI()
    {
        if (detectedText != null)
        {
            if (!string.IsNullOrEmpty(currentDetectedName))
                detectedText.text= $"Detected: {currentDetectedName} — {currentDetectedValue}";
            else
                detectedText.text = "Detected: —";
        }
    }

    private void UpdateTotalUI()
    {
        if (totalText != null)
        {
            // Format without too many decimals
            totalText.text = $"Total: {runningTotal:0.##}";
        }
    }

    // Called by Add button (or listeners)
    public void OnAddButton()
    {
        AddCurrentDetected();
    }

    // Called by Clear button
    public void OnClearButton()
    {
        runningTotal = 0f;
        history.Clear();
        UpdateTotalUI();
    }

    private void AddCurrentDetected()
    {
        if (currentDetectedValue > 0f)
        {
            runningTotal += currentDetectedValue;
            history.Add((currentDetectedName, currentDetectedValue, DateTime.Now));
            UpdateTotalUI();

            if (addSound != null) addSound.Play();

            // record cooldown for the current detected target so it won't be double-added immediately
            if (!string.IsNullOrEmpty(currentDetectedName))
            {
                lastAddedAt[currentDetectedName] = Time.time;
            }
        }
        else
        {
            // Optionally show a small feedback that nothing is detected
            Debug.Log("No valid detected value to add.");
        }
    }

    // Optional: public method to get history (useful for export)
    public List<(string name, float value, DateTime when)> GetHistory()
    {
        return history;
    }
}
