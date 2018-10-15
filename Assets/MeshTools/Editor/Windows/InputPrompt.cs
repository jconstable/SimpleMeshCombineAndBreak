using UnityEngine;
using UnityEditor;

public class InputPromptPopup : EditorWindow
{
    public float value = 1f;

    private string promptMessage;
    private System.Action<InputPromptPopup> onValueSelected;

    static public InputPromptPopup Init(string promptMessage, System.Action<InputPromptPopup> onValueSelected)
    {
        InputPromptPopup window = ScriptableObject.CreateInstance<InputPromptPopup>();
        window.promptMessage = promptMessage;
        window.onValueSelected = onValueSelected;
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 350, 150);
        window.ShowPopup();
        return window;
    }

    void OnGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(promptMessage, EditorStyles.wordWrappedLabel);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        value = EditorGUILayout.FloatField(value);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Ok"))
        {
            //try
            //{
                if (onValueSelected != null)
                    onValueSelected(this);
            //}catch(System.Exception e)
            //{
            //    Debug.LogError(e.Message);
            //}

            this.Close();
        }else if(GUILayout.Button("Cancel"))
        {
            this.Close();
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }
}