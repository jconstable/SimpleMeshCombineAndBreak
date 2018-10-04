This script is free to use.

Once copied into your Unity project, use the Mesh Tools menu item to combine or break meshes.

This script currently merges meshes that use the same material. Combine will add multiple meshes as submeshes to a new mesh object. Break will split all submeshes out into their own meshes.

Usage:
	Combine
		Select multiple GameObjects from your scene hierarchy and use the MeshTools->Combine menu item
		From script, call SimmpleMeshCombine.CombineMeshes()
	Break
		Select a GameObject with a single MeshFilter, and use the MeshTools->Break menu item 
		From script, call SimpleMeshCombine.BreakMesh()
