using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    public Transform playerTransform;
    public int health = 100;
    public string[] inventory = new string[0];

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public SaveData GetSaveData()
    {
        SaveData data = new SaveData();
        data.sceneName = SceneManager.GetActiveScene().name;

        // Save position
        data.playerX = playerTransform.position.x;
        data.playerY = playerTransform.position.y;
        data.playerZ = playerTransform.position.z;

        Debug.Log("Saving position: " + playerTransform.position);
        return data;
    }

    public void ApplySaveData(SaveData data)
    {
        Debug.Log("Applying save data: " + data.playerX + "," + data.playerY + "," + data.playerZ);

        if (playerTransform == null)
        {
            Debug.LogError("Player Transform not assigned!");
            return;
        }

        playerTransform.position = new Vector3(data.playerX, data.playerY, data.playerZ);
        Debug.Log("Player moved to: " + playerTransform.position);
    }
}
