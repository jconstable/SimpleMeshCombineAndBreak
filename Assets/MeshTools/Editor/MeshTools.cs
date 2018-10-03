using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class MeshCombine {
    [MenuItem("MeshTools/Combine")]
    public static void DoCombine()
    {
        List<MeshFilter> meshes = new List<MeshFilter>();
        foreach( var o in Selection.objects )
        {
            var go = o as GameObject;
            if( go == null )
            {
                Debug.LogError("Selected object is not a GameObject!");
                return;
            }
            var m = go.GetComponent<MeshFilter>();
            if( m != null )
            {
                meshes.Add(m);
            } else
            {
                Debug.LogError("Selected object " + go.name + " does not have a MeshFilter!");
                return;
            }
        }

        if( meshes.Count  >0)
        {
            List<string> names = new List<string>();
            List<Material> matList = new List<Material>();
            List<CombineInstance> combine = new List<CombineInstance>();
            List<GameObject> toDestroy = new List<GameObject>();

            for( int i = 0; i < meshes.Count; i++ )
            {
                var filter = meshes[i];

                if( filter.sharedMesh.subMeshCount == 1 )
                {
                    DoCombine(filter, matList, combine);
                } else
                {
                    var newObjects = BreakMeshIntoBits(filter.gameObject);
                    foreach( var o in newObjects )
                    {
                        DoCombine(o, matList, combine);
                        toDestroy.Add(o.gameObject);
                    }
                }
                names.Add(filter.gameObject.name);
                filter.gameObject.SetActive(false);
            }

            var newObject = new GameObject( string.Join(",", names.ToArray()) + " combined");
            var mf = newObject.AddComponent<MeshFilter>();
            mf.mesh = new Mesh();
            mf.sharedMesh.CombineMeshes(combine.ToArray(), false, true, false);
            var mr = newObject.AddComponent<MeshRenderer>();
            mr.materials = matList.ToArray();
            mf.sharedMesh.RecalculateBounds();

            var newCenter = mr.bounds.center;

            var newVertices = mf.sharedMesh.vertices;
            for( int i = 0; i < newVertices.Length; i++)
            {
                newVertices[i] -= newCenter;
            }
            mf.sharedMesh.vertices = newVertices;
            mf.sharedMesh.RecalculateBounds();

            newObject.transform.position = newCenter;

            foreach (var o in toDestroy)
            {
                GameObject.DestroyImmediate(o);
            }
        }
    }
    
    private static void DoCombine(MeshFilter filter, List<Material> matList, List<CombineInstance> combineList)
    {
        matList.AddRange(filter.gameObject.GetComponent<MeshRenderer>().sharedMaterials);

        CombineInstance c = new CombineInstance();
        c.mesh = filter.sharedMesh;
        c.transform = filter.gameObject.transform.localToWorldMatrix;
        combineList.Add(c);
    }

    [MenuItem("MeshTools/Break")]
    public static void BreakMesh()
    {
        if (Selection.objects.Length != 1)
        {
            Debug.LogError("Exactly one combined mesh must be selected in the hierarchy");
            return;
        }

        var go = Selection.objects[0] as GameObject;
        BreakMeshIntoBits(go);
        go.SetActive(false);
    }

    public static List<MeshFilter> BreakMeshIntoBits( GameObject o )
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

        for( int i = 0; i < mf.sharedMesh.subMeshCount; i++ )
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

            foreach ( int index in indices )
            {
                int ni = 0;
                if(!remap.TryGetValue(index, out ni ))
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
