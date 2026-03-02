using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;
using TMPro;

public class MainMenu : MonoBehaviour
{
    public GameObject slotPanel;    // Panel that holds the save slot buttons
    public TextMeshProUGUI slotInfoText; // Text element that displays info about the selected slot
    private int selectedSlot = -1;    // Tracks which slot the player has selected (-1 = none
    public GameObject mainPanel;
    private SaveData pendingData;


    public void NewJourney()
    {
        SceneManager.LoadScene("StartingScene");
    }
    public void OpenSlots()
    {
        mainPanel.SetActive(false);
        slotPanel.SetActive(true);
    }
    public void CloseSlots()
    {
        mainPanel.SetActive(true);
        slotPanel.SetActive(false);
    }
    // SelectSlot: Called when the player clicks a slot button.
    // Stores the chosen slot index and updates the info text with metadata (timestamp or "Empty Slot").
    public void SelectSlot(int slot)
    {
        selectedSlot = slot;
        UpdateSlotInfo(slot);
        Debug.Log("Selected slot: " + selectedSlot);

    }

    // UpdateSlotInfo: Checks if a save file exists for the given slot.
    // If it does, display the last saved timestamp. If not, show "Empty Slot".

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
    // LoadSelectedSlot: Called when the player clicks the Play/Continue button.
    // Loads the save data from the selected slot, switches to the saved scene,
    // and applies the player’s saved position/stats.

    public void ContinueFromSlot(int slot)
    {
        SaveData data = SaveSystem.LoadGame(slot);
        if (data == null)
        {
            Debug.LogWarning("Slot " + slot + " is empty!");
            return;
        }
        Debug.Log("Confirming continue from slot: " + selectedSlot);


        GameManager.Instance.LoadGame(data);
    }


    public void ConfirmContinue()
    {
        if (selectedSlot == -1)
        {
            Debug.LogWarning("No slot selected!");
            return;
        }

        ContinueFromSlot(selectedSlot);
    }


    // ContinueGame: Convenience method for a "Continue" button.
    // Finds the most recent save slot and loads it automatically.

    public void ContinueGame()
    {
        int latestSlot = FindMostRecentSlot();
        if (latestSlot != -1)
        {
            selectedSlot = latestSlot;
            ContinueFromSlot(latestSlot);
        }
    }
    // FindMostRecentSlot: Loops through all slots and finds the one with the newest timestamp.
    // Returns -1 if no saves exist.

    private int FindMostRecentSlot()
    {
        int latestSlot = -1;
        System.DateTime latestTime = System.DateTime.MinValue;

        for (int i = 0; i < 3; i++) // adjust for number of slots
        {
            string path = Application.persistentDataPath + "/save" + i + ".json";
            if (File.Exists(path))
            {
                var time = File.GetLastWriteTime(path);
                if (time > latestTime)
                {
                    latestTime = time;
                    latestSlot = i;
                }
            }
        }
        return latestSlot;
    }
    public void QuitGame()
    {
        Debug.Log("Quit!");
        Application.Quit();
    }
}
