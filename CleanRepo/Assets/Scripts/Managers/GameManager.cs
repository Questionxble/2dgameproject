using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public SaveData pendingData; // holds save data until scene is loaded

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // survives scene changes
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ApplyPendingData());
    }

    private IEnumerator ApplyPendingData()
    {
        yield return null; // wait one frame so PlayerManager initializes

        if (pendingData != null && PlayerManager.Instance != null)
        {
            PlayerManager.Instance.ApplySaveData(pendingData);
            Debug.Log("Applied save data: " + pendingData.playerX + "," + pendingData.playerY + "," + pendingData.playerZ);
            pendingData = null;
        }
        else
        {
            Debug.LogWarning("No pendingData or PlayerManager not ready.");
        }
    }

    public void LoadGame(SaveData data)
    {
        if (data == null)
        {
            Debug.LogError("LoadGame called with null SaveData!");
            return;
        }

        pendingData = data;
        Debug.Log("Pending data set: " + data.playerX + ", " + data.playerY + ", " + data.playerZ);

        SceneManager.LoadScene(data.sceneName);
    }
}
