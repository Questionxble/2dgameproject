using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace PistonMedia {
	/// <summary>	
	/// Simple Script to toggle the night and day environments on the sample scene.
	/// </summary>
public class FantasticSceneManager : MonoBehaviour
{
	[SerializeField]
	private GameObject dayEnvironment;
	[SerializeField]
	private GameObject nightEnvironment;
	[SerializeField]
	private Camera sceneCamera;
	[SerializeField]
	private Color dayLightSceneBackgroundColor;
	[SerializeField]
	private Color nightSceneBackgroundColor;
	public void ChangeDayNightScene() {
		// Night Scene
		if(dayEnvironment.activeInHierarchy) {
			dayEnvironment.SetActive(false);
			nightEnvironment.SetActive(true);
			ChangeCameraBackgroundColor(nightSceneBackgroundColor);
		} else { // DayScene
			nightEnvironment.SetActive(false);
			dayEnvironment.SetActive(true);
			ChangeCameraBackgroundColor(dayLightSceneBackgroundColor);
		}
	}

	public void ChangeCameraBackgroundColor(Color _color) {
		if(sceneCamera != null) {
			sceneCamera.backgroundColor = _color;
		} else if(sceneCamera == null) {
			Debug.LogError("Could not change background color: no scene camera found");
		}
	}
}
}