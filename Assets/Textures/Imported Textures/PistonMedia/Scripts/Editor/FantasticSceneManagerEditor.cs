using UnityEditor;
using UnityEngine;
namespace PistonMedia {
[CustomEditor(typeof(FantasticSceneManager))]
public class FantasticSceneManagerEditor : Editor
{
	// Shows the toggle button in editor and calls the method to change the environment in the scene.
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();
		var fantasticSceneManager = target as FantasticSceneManager;
		if (GUILayout.Button("Change Day/Night")) {
			fantasticSceneManager.ChangeDayNightScene();
		}
	}
}
}