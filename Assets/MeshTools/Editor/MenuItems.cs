using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MeshTools
{
    public class MeshToolsMenuItems
    {
        // Editor Prefs keys
        private static string sLastFolderPath = "MESH_COMBINE_KEY_LAST_FOLDER_PATH";
        private static string sLastFolderName = "MESH_COMBINE_KEY_LAST_FOLDER_NAME";

        [MenuItem("MeshTools/Combine")]
        protected static void MenuItemDoCombine()
        {
            List<GameObject> objects = new List<GameObject>();
            bool makeStatic = true;
            foreach (var o in Selection.objects)
            {
                var go = o as GameObject;
                if (go != null)
                {
                    makeStatic &= go.isStatic;
                    objects.Add(go);
                }
            }

            if (objects.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "No suitable objects found to combine.", "Ok");
                return;
            }

            // Pick where these new assets will be put
            string outputFolder = ChooseFolder(objects[0]);
            if (string.IsNullOrEmpty(outputFolder))
                return;

            var combined = SimmpleMeshCombine.CombineMeshes(objects);

            // Create a prefab and mesh asset out of what we've made
            var newObjectAsPrefab = SaveCombined(outputFolder, combined, combined.GetComponent<MeshFilter>());

            // If all objects that were selected are static, make the new one static
            if (makeStatic)
            {
                newObjectAsPrefab.isStatic = makeStatic;
            }

            if (newObjectAsPrefab != null)
            {
                // Select the new GO in the hierarchy
                Selection.SetActiveObjectWithContext(newObjectAsPrefab, newObjectAsPrefab);
            }
        }

        [MenuItem("MeshTools/Break")]
        protected static void MenuItemDoBreak()
        {
            if (Selection.objects.Length != 1)
            {
                EditorUtility.DisplayDialog("Error", "Exactly one combined mesh must be selected in the hierarchy.", "Ok");
                return;
            }

            var go = Selection.objects[0] as GameObject;

            // Pick where these new assets will be put
            string outputFolder = ChooseFolder(go);
            if (string.IsNullOrEmpty(outputFolder))
                return;

            var resultingObjects = SimmpleMeshCombine.BreakMesh(go);
            go.SetActive(false);

            foreach (var r in resultingObjects)
            {
                var newGO = SaveCombined(outputFolder, r.gameObject, r);
                if (go.isStatic)
                {
                    newGO.isStatic = true;
                }
            }
        }
        

        // Pick a folder to store our new assets
        private static string ChooseFolder(Object mesh)
        {
            string outputFolderPath = EditorPrefs.GetString(sLastFolderPath);
            string outputFolderName = EditorPrefs.GetString(sLastFolderName);
            string[] objectZeroPath = null;

            // If we haven't stored the last used folder path and name, use the mesh's current project path
            if (string.IsNullOrEmpty(outputFolderPath))
            {
                string path = AssetDatabase.GetAssetPath(mesh);
                if (string.IsNullOrEmpty(path))
                {
                    outputFolderPath = Application.dataPath;
                    outputFolderName = "CombinedMeshes";
                }
                else
                {
                    objectZeroPath = path.Split('/');
                    outputFolderPath = string.Join("/", objectZeroPath, 0, objectZeroPath.Length - 2);
                    outputFolderName = objectZeroPath[objectZeroPath.Length - 2];
                }
            }

            string outputFolder = EditorUtility.OpenFolderPanel("Select Destination Mesh Asset Location", outputFolderPath, outputFolderName);
            if (string.IsNullOrEmpty(outputFolder))
                return null;

            int assetsSubstrIndex = outputFolder.IndexOf("Assets");
            outputFolder = outputFolder.Substring(assetsSubstrIndex, outputFolder.Length - assetsSubstrIndex);

            // Update the editor prefs with the user's selection, so we can remember it for next time
            objectZeroPath = outputFolder.Split('/');
            outputFolderPath = string.Join("/", objectZeroPath, 0, objectZeroPath.Length - 1);
            outputFolderName = objectZeroPath[objectZeroPath.Length - 1];
            EditorPrefs.SetString(sLastFolderPath, outputFolderPath);
            EditorPrefs.SetString(sLastFolderName, outputFolderName);

            if (!System.IO.Directory.Exists(outputFolder))
            {
                System.IO.Directory.CreateDirectory(outputFolder);
            }

            return outputFolder;
        }

        // Sanitize the file name from the GO name
        private static string ProcessName(string name)
        {
            return name.Replace(":", "").Replace(" ", "_").Replace(",", "_");
        }

        // Store the new GO and mesh data to assets, link as prefab
        private static GameObject SaveCombined(string outputFolder, GameObject newObject, MeshFilter mesh)
        {
            var currentScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(currentScene);

            string outputFileName = ProcessName(newObject.name);

            // If the file exists, keep tacking a number onto the end until we find a filename that is not in use
            int fileTestCounter = 1;
            string tempName = outputFileName;
            while (System.IO.File.Exists(outputFolder + "/" + tempName + ".prefab"))
            {
                tempName = outputFileName + "-" + fileTestCounter++;
            }

            string prefabPath = outputFolder + "/" + tempName + ".prefab";
            string meshPath = outputFolder + "/" + tempName + ".asset";

            AssetDatabase.CreateAsset(mesh.sharedMesh, meshPath);
            PrefabUtility.CreatePrefab(prefabPath, newObject);
            AssetDatabase.SaveAssets();

            return PrefabUtility.ConnectGameObjectToPrefab(newObject, AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        }

    }
}
