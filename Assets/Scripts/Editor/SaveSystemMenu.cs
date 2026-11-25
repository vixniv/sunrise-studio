using UnityEditor;
using UnityEngine;

// Editor only: Tools > Save menu for testing the save file on this computer.
public static class SaveSystemMenu
{
    [MenuItem("Tools/Save/Delete Save")]
    private static void DeleteSave()
    {
        if (!SaveSystem.HasSave)
        {
            Debug.Log($"SaveSystem: there is no save at {SaveSystem.SavePath}.");
            return;
        }
        if (!EditorUtility.DisplayDialog("Delete Save", $"Delete the save file?\n\n{SaveSystem.SavePath}", "Delete", "Cancel"))
            return;
        SaveSystem.DeleteSave();
        Debug.Log($"SaveSystem: deleted the save at {SaveSystem.SavePath}.");
    }

    [MenuItem("Tools/Save/Show Save File")]
    private static void ShowSaveFile()
    {
        if (SaveSystem.HasSave)
            EditorUtility.RevealInFinder(SaveSystem.SavePath);
        else
            EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
}
