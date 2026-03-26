using UnityEngine;
using UnityEngine.InputSystem;

public class GameController : MonoBehaviour
{
    [Header("Core Game Settings")]
    [SerializeField] private bool gameIsPaused = false;
    
    private PlayerMovement playerMovement;
    
    void Start()
    {
        // Find the player in the scene
        playerMovement = FindFirstObjectByType<PlayerMovement>();
        
        if (playerMovement == null)
        {
            Debug.LogError("GameController: No PlayerMovement found in scene!");
        }
        
        // Initialize core game systems
        InitializeGame();
    }

    void Update()
    {
        // Handle core game logic (pause, menu, etc.)
        HandleGameInput();
    }
    
    private void InitializeGame()
    {
        // TODO: Initialize game state, UI, score, etc.
    }
    
    private void HandleGameInput()
    {
        // ESC key behavior has been moved to ClientDebugger for multiplayer compatibility
        // In multiplayer, ESC should not pause the entire game for all players
        // Instead, it should only stop local player movement/actions
        
        // Check if we're in multiplayer mode
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            // In multiplayer, ESC handling is managed by ClientDebugger
            // This prevents pausing the game for all connected players
            return;
        }
        
        // In single-player mode, ESC can still pause normally
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ToggleSinglePlayerPause();
        }
    }
    
    private void ToggleSinglePlayerPause()
    {
        // Only pause in single-player mode
        gameIsPaused = !gameIsPaused;
        Time.timeScale = gameIsPaused ? 0f : 1f;
        Debug.Log($"Game {(gameIsPaused ? "paused" : "resumed")} (single-player mode)");
    }
    
    // Legacy pause method for backwards compatibility
    private void TogglePause()
    {
        ToggleSinglePlayerPause();
    }
    
    // Public methods for other systems
    public bool IsGamePaused() => gameIsPaused;
    
    public void RestartLevel()
    {
        // TODO: Implement level restart logic
    }
    
    public void GameOver()
    {
        // TODO: Implement game over logic
    }
}
