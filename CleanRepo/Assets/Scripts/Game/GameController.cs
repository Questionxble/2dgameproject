using UnityEngine;
using UnityEngine.InputSystem;

public class GameController : MonoBehaviour
{
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
        // Handle pause menu, game state changes, etc. using new Input System
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // TogglePause();
        }
    }
    
    // Commented out to fix double pause bug

    // private void TogglePause()
    // {
    //     gameIsPaused = !gameIsPaused;
    //     Time.timeScale = gameIsPaused ? 0f : 1f;
    //     Debug.Log($"Game {(gameIsPaused ? "Paused" : "Resumed")}");
    // }
    
    // Public methods for other systems
    // public bool IsGamePaused() => gameIsPaused;
    
    public void RestartLevel()
    {
        // TODO: Implement level restart logic
    }
    
    public void GameOver()
    {
        // TODO: Implement game over logic
    }
}
