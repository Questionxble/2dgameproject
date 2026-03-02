using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using TMPro;

public class NewGame : MonoBehaviour
{
    public GameObject NewGamePanel;
    public GameObject overwritePopup;
    private int selectedSlot = -1; // integer slot index
    public TextMeshProUGUI slotInfoText;
   

    public void OpenNewGame()
    {
        NewGamePanel.SetActive(true);
    }

    public void CloseNewGame()
    {
        NewGamePanel.SetActive(false);
    }

    public void SelectSlot(int slot)
    {
        selectedSlot = slot;
        UpdateSlotInfo(slot);
        Debug.Log("Selected slot: " + selectedSlot);
    }

    private void UpdateSlotInfo(int slot)
    {
        string path = Application.persistentDataPath + "/save" + slot + ".json";
        if (File.Exists(path))
        {
            System.DateTime lastWrite = File.GetLastWriteTime(path);
            slotInfoText.text = "Last Saved: " + lastWrite.ToString();
        }
        else
        {
            slotInfoText.text = "Empty Slot";
        }
    }

    public void ConfirmNewGame()
    {
        if (selectedSlot == -1)
        {
            Debug.LogWarning("No slot selected!");
            return;
        }

        string path = Application.persistentDataPath + "/save" + selectedSlot + ".json";
        if (File.Exists(path))
        {
            // Show overwrite popup, keep NewGamePanel open
            overwritePopup.SetActive(true);
            NewGamePanel.SetActive(false); // hide panel so popup is visible

        }
        else
        {
            // Empty slot → start new game immediately
            StartNewGame();
        }
    }

    public void OverwriteYes()
    {
        if (selectedSlot == -1)
        {
            Debug.LogWarning("No slot selected!");
            return;
        }

        // Delete old save if it exists
        SaveSystem.DeleteGame(selectedSlot);

        // Create a fresh SaveData instead of using PlayerManager
        SaveData data = new SaveData();
        data.sceneName = "StartingScene";
        data.playerX = 0f;
        data.playerY = 0f;
        data.playerZ = 0f;
        data.health = 100;
        data.inventory = new string[0];

        SaveSystem.SaveGame(data, selectedSlot);

        overwritePopup.SetActive(false);
        NewGamePanel.SetActive(false);

        // Load the starting scene
        SceneManager.LoadScene("StartingScene");
    }

    public void OverwriteNo()
    {
        // Cancel overwrite, stay in NewGamePanel
        overwritePopup.SetActive(false);
        NewGamePanel.SetActive(true);
    }

    private void StartNewGame()
    {
        SaveData data = new SaveData();
        data.sceneName = "StartingScene";
        data.playerX = 0f;
        data.playerY = 0f;
        data.playerZ = 0f;
        data.health = 100;
        data.inventory = new string[0];

        SaveSystem.SaveGame(data, selectedSlot);

        SceneManager.LoadScene("StartingScene");
    }




}
