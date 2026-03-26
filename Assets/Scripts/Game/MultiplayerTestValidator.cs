using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Simple test script to validate multiplayer setup
/// Add this to a GameObject in your scene for testing
/// </summary>
public class MultiplayerTestValidator : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private bool runTestsOnStart = true;
    [SerializeField] private bool verboseLogging = true;
    
    private void Start()
    {
        if (runTestsOnStart)
        {
            RunValidationTests();
        }
    }
    
    private void RunValidationTests()
    {
        Debug.Log("=== Multiplayer Setup Validation Tests ===");
        
        // Test 1: Check NetworkManager existence
        TestNetworkManagerExists();
        
        // Test 2: Check PlayerMovement collision prevention
        TestPlayerCollisionPrevention();
        
        // Test 3: Check MultiplayerGameManager setup
        TestMultiplayerGameManagerSetup();
        
        // Test 4: Check auto-startup components
        TestAutoStartupComponents();
        
        Debug.Log("=== Validation Tests Complete ===");
    }
    
    private void TestNetworkManagerExists()
    {
        if (verboseLogging) Debug.Log("[Test] Checking NetworkManager...");
        
        if (NetworkManager.Singleton != null)
        {
            Debug.Log("✅ NetworkManager found and accessible");
        }
        else
        {
            Debug.LogError("❌ NetworkManager not found! Make sure NetworkManager exists in scene");
        }
    }
    
    private void TestPlayerCollisionPrevention()
    {
        if (verboseLogging) Debug.Log("[Test] Checking PlayerMovement collision prevention...");
        
        PlayerMovement[] playerMovements = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        bool hasCollisionMethod = false;
        
        foreach (var pm in playerMovements)
        {
            // Check if the collision prevention method exists (we can't directly test private methods)
            var methods = pm.GetType().GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var method in methods)
            {
                if (method.Name == "SetupPlayerCollisionIgnoring")
                {
                    hasCollisionMethod = true;
                    break;
                }
            }
        }
        
        if (hasCollisionMethod)
        {
            Debug.Log("✅ PlayerMovement collision prevention method found");
        }
        else
        {
            Debug.LogWarning("⚠️ PlayerMovement collision prevention method not found");
        }
    }
    
    private void TestMultiplayerGameManagerSetup()
    {
        if (verboseLogging) Debug.Log("[Test] Checking MultiplayerGameManager setup...");
        
        MultiplayerGameManager mgm = FindFirstObjectByType<MultiplayerGameManager>();
        if (mgm != null)
        {
            Debug.Log("✅ MultiplayerGameManager found in scene");
        }
        else
        {
            Debug.LogWarning("⚠️ MultiplayerGameManager not found in scene");
        }
    }
    
    private void TestAutoStartupComponents()
    {
        if (verboseLogging) Debug.Log("[Test] Checking server configuration components...");
        
        DedicatedServerConfig[] serverConfigs = FindObjectsByType<DedicatedServerConfig>(FindObjectsSortMode.None);
        
        if (serverConfigs.Length > 0)
        {
            Debug.Log($"✅ Found {serverConfigs.Length} DedicatedServerConfig component(s)");
            
            foreach (var config in serverConfigs)
            {
                if (verboseLogging)
                {
                    Debug.Log($"  - DedicatedServerConfig on GameObject: {config.gameObject.name}");
                }
            }
        }
        else
        {
            Debug.LogWarning("⚠️ No DedicatedServerConfig found. Add DedicatedServerConfig to the NetworkManager GameObject for proper server startup");
        }
    }
    
    // Public method to run tests manually
    [ContextMenu("Run Validation Tests")]
    public void RunTestsManually()
    {
        RunValidationTests();
    }
}