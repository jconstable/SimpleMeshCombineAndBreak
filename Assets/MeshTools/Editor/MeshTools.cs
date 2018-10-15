// 2018 - John Constable
// Use freely, but please send pull requests with enhancements!
// https://github.com/jconstable/SimpleMeshCombineAndBreak

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MeshTools
{
    public class SimmpleMeshCombine
    {
        // Container class for bucketing by material
        private class MaterialAndCombines
        {
            public Material mat;
            public List<CombineInstance> list = new List<CombineInstance>();
        }

        // Do the work of combining meshes
        public static GameObject CombineMeshes(List<GameObject> objects)
        {
            List<MeshFilter> meshes = new List<MeshFilter>();
            List<GameObject> originalObjects = new List<GameObject>();

            // Loop over all selected GameObjects
            foreach (var o in Selection.objects)
            {
                var go = o as GameObject;
                if (go == null)
                {
                    Debug.LogError("Selected object is not a GameObject!");
                    return null;
                }

                // Collect the MeshFilters in each GameObject
                var filters = go.GetComponentsInChildren<MeshFilter>();
                if (filters.Length > 0)
                {
                    meshes.AddRange(filters);
                }
                else
                {
                    Debug.LogError("Selected object " + go.name + " does not have a MeshFilter!");
                    return null;
                }

                originalObjects.Add(go);
            }

            // Only run if we found MeshFilters from selection
            if (meshes.Count > 0)
            {
                // Data structure to store source information as we go
                List<string> names = new List<string>();
                List<GameObject> toDestroy = new List<GameObject>();
                Dictionary<Material, MaterialAndCombines> combinesByMaterial = new Dictionary<Material, MaterialAndCombines>();

                // Loop over all found MeshFilters
                for (int i = 0; i < meshes.Count; i++)
                {
                    var filter = meshes[i];

                    // If there is only one submesh, no need to break apart first
                    if (filter.sharedMesh.subMeshCount == 1)
                    {
                        DoCombine(filter, combinesByMaterial);
                    }
                    else
                    {
                        // We need to isolate all submeshes into discreet Mesh objects.
                        // Use the BreakMeshIntoBits function to create temporary GameObjects with one mesh each
                        var newObjects = BreakMesh(filter.gameObject);
                        foreach (var o in newObjects)
                        {
                            DoCombine(o, combinesByMaterial);

                            // We need to clean these temp object up later
                            toDestroy.Add(o.gameObject);
                        }
                    }

                    // Add the parent GO name, so we can add to the new GO's name later
                    names.Add(filter.gameObject.name);
                }

                // For each bucket, combine meshes of the same material into a single mesh
                Dictionary<Material, Mesh> submeshes = new Dictionary<Material, Mesh>();
                foreach (var pair in combinesByMaterial)
                {
                    var mesh = new Mesh();
                    var list = pair.Value.list;
                    mesh.CombineMeshes(list.ToArray(), true, false, false); // 2nd arg means to actually "weld" meshes
                    submeshes[pair.Value.mat] = mesh;

                    foreach (var combine in list)
                    {
                        GameObject.DestroyImmediate(combine.mesh);
                    }
                }

                // Create the uber mesh that will hold our merged submeshes
                Mesh newParentMesh = new Mesh();
                List<Material> matList = new List<Material>();
                List<CombineInstance> combineForParent = new List<CombineInstance>();

                // For each of our newly combined submeshes, we need to create a CombineInstance to be added to the final parent
                foreach (var pair in submeshes)
                {
                    CombineInstance c = new CombineInstance();
                    c.mesh = pair.Value;
                    combineForParent.Add(c);
                    matList.Add(pair.Key);
                }

                // Final combine into uber mesh
                newParentMesh.CombineMeshes(combineForParent.ToArray(), false, false, false);

                // Create a new GO that with a MeshFilter that will reference our new uber mesh
                var nameParts = names.GetRange(0, Mathf.Min(4, names.Count)).ToArray();
                var newObject = new GameObject("Combined: " + string.Join(",", nameParts) + (nameParts.Length < names.Count ? "..." : ""));
                var mf = newObject.AddComponent<MeshFilter>();
                newParentMesh.name = newObject.name;
                mf.mesh = newParentMesh;

                // Create the uber mesh's MeshRenderer, and add the material for each new submesh
                var mr = newObject.AddComponent<MeshRenderer>();
                mr.materials = matList.ToArray();
                mf.sharedMesh.RecalculateBounds();

                // Make sure the new mesh is at the same location as the old mesh
                var newCenter = mr.bounds.center;
                var newVertices = mf.sharedMesh.vertices;
                for (int i = 0; i < newVertices.Length; i++)
                {
                    newVertices[i] -= newCenter; // Ofset the verts in the mesh to the original location
                }
                mf.sharedMesh.vertices = newVertices;
                mf.sharedMesh.RecalculateBounds();
                newObject.transform.position = newCenter;

                // If there was only one parent GameObject that we combined, parent the new GO to the same place in the hierarchy
                if (Selection.objects.Length == 1)
                {
                    GameObject singleObject = Selection.objects[0] as GameObject;
                    newObject.transform.parent = singleObject.transform.parent;
                }
                else
                {
                    // We don't know how to choose where to parent something that came from lots of meshes, so leave the new GO at the root
                }

                // Deactivate the old mesh objects
                foreach (var o in originalObjects)
                {
                    o.SetActive(false);
                }

                // Destroy the temp GOs we created when we broke complex meshes into bits
                foreach (var o in toDestroy)
                {
                    GameObject.DestroyImmediate(o);
                }

                return newObject;
            }

            return null;
        }

        private static void ScaleMesh(MeshRenderer rend, Mesh mesh)
        {
            var verts = mesh.vertices;
            GameObject o = new GameObject();
            o.transform.position = rend.transform.position;
            o.transform.localScale = rend.transform.lossyScale;
            o.transform.rotation = rend.transform.rotation;
            for (int i = 0; i < verts.Length; i++)
            {
                
                verts[i] = o.transform.TransformPoint(verts[i]);// - rend.transform.position;
                
            }
            mesh.vertices = verts;
            GameObject.DestroyImmediate(o);
        }

        
        // Bucket the submeshes by material, so we can do a merge combine later
        private static Material DoCombine(MeshFilter filter, Dictionary<Material, MaterialAndCombines> buckets)
        {
            MeshRenderer rend = filter.gameObject.GetComponent<MeshRenderer>();
            Material mat = rend.sharedMaterial;

            CombineInstance c = new CombineInstance();
            c.mesh = GameObject.Instantiate(filter.sharedMesh);
            c.transform = filter.gameObject.transform.localToWorldMatrix;
            ScaleMesh(rend, c.mesh);

            MaterialAndCombines combineSet;
            if (!buckets.TryGetValue(mat, out combineSet))
            {
                combineSet = new MaterialAndCombines();
                combineSet.mat = mat;
                buckets[mat] = combineSet;
            }
            
            combineSet.list.Add(c);

            return mat;
        }

        // For each submesh, create a new mesh and make sure they exist in the same position in world space
        public static List<MeshFilter> BreakMesh(GameObject o)
        {
            List<MeshFilter> newMeshBits = new List<MeshFilter>();

            MeshFilter mf = o.GetComponent<MeshFilter>();
            MeshRenderer mr = o.GetComponent<MeshRenderer>();
            Vector3 center = mr.bounds.center;

            Vector3[] verts = mf.sharedMesh.vertices;
            Vector2[] uv = mf.sharedMesh.uv;
            int[] tris = mf.sharedMesh.triangles;
            Vector2[] uv2 = mf.sharedMesh.uv2;
            Vector2[] uv3 = mf.sharedMesh.uv3;
            Vector2[] uv4 = mf.sharedMesh.uv4;
            Vector3[] normals = mf.sharedMesh.normals;

            for (int i = 0; i < mf.sharedMesh.subMeshCount; i++)
            {
                int[] indices = mf.sharedMesh.GetIndices(i);
                Dictionary<int, int> remap = new Dictionary<int, int>();

                List<int> newIndices = new List<int>();
                List<Vector3> newVerts = new List<Vector3>();
                List<Vector2> newUv = new List<Vector2>();
                List<Vector2> newUv2 = new List<Vector2>();
                List<Vector2> newUv3 = new List<Vector2>();
                List<Vector2> newUv4 = new List<Vector2>();
                List<Vector3> newNormals = new List<Vector3>();

                foreach (int index in indices)
                {
                    int ni = 0;
                    if (!remap.TryGetValue(index, out ni))
                    {
                        ni = newVerts.Count;
                        remap[index] = ni;

                        newVerts.Add(verts[index]);
                        newUv.Add(uv[index]);
                        if (uv2.Length > index) newUv2.Add(uv2[index]);
                        if (uv3.Length > index) newUv3.Add(uv3[index]);
                        if (uv4.Length > index) newUv4.Add(uv4[index]);

                        newNormals.Add(normals[index]);
                    }

                    newIndices.Add(ni);
                }

                Mesh m = new Mesh();

                m.vertices = newVerts.ToArray();
                //m.triangles = newTris.ToArray();
                m.uv = newUv.ToArray();
                m.uv2 = newUv2.ToArray();
                m.uv3 = newUv3.ToArray();
                m.uv4 = newUv4.ToArray();
                m.normals = newNormals.ToArray();
                m.triangles = newIndices.ToArray();

                GameObject n = new GameObject(o.name + " submesh " + i);
                n.transform.position = Vector3.zero;
                n.transform.rotation = mf.gameObject.transform.rotation;
                n.transform.localScale = mf.gameObject.transform.localScale;

                var newMF = n.AddComponent<MeshFilter>();
                newMF.mesh = m;

                var newMR = n.AddComponent<MeshRenderer>();
                newMR.material = mr.sharedMaterials[i];

                m.RecalculateBounds();

                var oldCenter = newMR.bounds.center;
                for (int j = 0; j < newVerts.Count; j++)
                {
                    newVerts[j] -= oldCenter;
                }
                m.vertices = newVerts.ToArray();
                m.RecalculateBounds();

                n.transform.position = oldCenter + o.transform.position;

                newMeshBits.Add(newMF);
            }

            return newMeshBits;
        }
    }
}