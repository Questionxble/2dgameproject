using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;
using TMPro;
using System;


public class PauseMenuScript : MonoBehaviour
{
    public GameObject pauseMenuUI; // assign in Inspector
    public static bool isPaused = false;
    public GameObject mainPanel;
    public GameObject optionsPanel;
    public GameObject slotPanel;
    public GameObject overwritePopup;
    public TMPro.TextMeshProUGUI slotInfoText;


    private int selectedSlot = -1; // tracks which slot is currently selected

    //Pauses Game 
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }


    //Toggles 
    //Resume: Turns off the panel and returns to the game 
    public void Resume()
    {
        BackToMain();
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
    }
    //OpenOptions: Opens the same options menu used in main menu screen, Activates Options Panel and
    //turns off the main panel
    public void OpenOptions()
    {
        mainPanel.SetActive(false);
        optionsPanel.SetActive(true);
    }
    //CloseOptions: Closes the options panel and reactivates the main panel 
    public void CloseOptions()
    {
        optionsPanel.SetActive(false);
        mainPanel.SetActive(true);

    }
    //OpenSlots: Opens the Slots Menu by activating the slotPanel and turning off the main panel 
    public void OpenSlots()
    {
        mainPanel.SetActive(false);
        slotPanel.SetActive(true);
    }
    //BackToMain: Goes back to the main menu by deactivating the slot panel and activating the main
    //panel.
    public void BackToMain()
    {
        optionsPanel.SetActive(false);
        slotPanel.SetActive(false);
        overwritePopup.SetActive(false);
        mainPanel.SetActive(true);
    }
    //Pause: Pauses the game , pops up the menu and sets the in game timescale to 0 
    void Pause()
    {
        Debug.Log("PauseMenuUI reference: " + pauseMenuUI.name);
        pauseMenuUI.SetActive(true);
        mainPanel.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;
    }
    //LoadMainMenu: Loads back to the MainMenu Scene
    public void LoadMainMenu()
    {
        Time.timeScale = 1f; // reset time before switching scenes
        isPaused = false;
        SceneManager.LoadScene("MainMenu"); //
    }
    //Slot Selection 
    public void SelectSlot(int slot)
    {
        selectedSlot = slot;
        UpdateSlotInfo(slot);
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
    // Confirm save
    public void ConfirmSave()
    {
        if (selectedSlot == -1)
        {
            Debug.LogWarning("No slot selected!");
            return;
        }

        string path = Application.persistentDataPath + "/save" + selectedSlot + ".json";
        if (File.Exists(path))
        {
            slotPanel.SetActive(false);
            overwritePopup.SetActive(true);
        }
        else
        {
            SaveSystem.SaveGame(PlayerManager.Instance.GetSaveData(), selectedSlot);
            BackToMain();
        }
    }

    // Overwrite popup
    public void OverwriteYes()
    {
        SaveSystem.SaveGame(PlayerManager.Instance.GetSaveData(), selectedSlot);
        slotPanel.SetActive(true);
        overwritePopup.SetActive(false);
        BackToMain();
    }

    public void OverwriteNo()
    {
        slotPanel.SetActive(true);
        overwritePopup.SetActive(false);
    }

    // Delete save
    public void DeleteSave()
    {
        if (selectedSlot == -1) return;
        SaveSystem.DeleteGame(selectedSlot);
        UpdateSlotInfo(selectedSlot);
    }
    public void LoadGame(int slot)
    {
        SaveData data = SaveSystem.LoadGame(slot);
        if (data == null) return;

        SceneManager.LoadScene(data.sceneName);
        StartCoroutine(ApplyAfterSceneLoad(data));
    }

    private IEnumerator ApplyAfterSceneLoad(SaveData data)
    {
        yield return null; // wait one frame for scene load
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.ApplySaveData(data);
        }
    }

}
