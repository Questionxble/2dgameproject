using UnityEngine;

public class OptionsMenu : MonoBehaviour
{
    public GameObject OptionsPanel;

    public void OpenOptions()
    {
        LogManager.instance.log("OpenOptions called!", LogManager.INFO);
        OptionsPanel.SetActive(true);
    }

    public void CloseOptions()
    {
        OptionsPanel.SetActive(false);
    }
}
