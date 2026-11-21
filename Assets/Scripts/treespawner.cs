using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshCollider))]
public class TreeSpawner : MonoBehaviour
{
    [SerializeField] private GameObject treePrefab;
    [SerializeField, Min(1)] private int treeCount = 300;
    [SerializeField, Range(0f, 90f)] private float maxSlope = 30f;
    [SerializeField] private float heightOffset = 0f;

#if UNITY_EDITOR
    [ContextMenu("Rozmiesc drzewa")]
    private void SpawnTrees()
    {
        MeshCollider terrain = GetComponent<MeshCollider>();
        if (treePrefab == null || terrain.sharedMesh == null)
        {
            Debug.LogWarning("Przypisz prefab drzewa i siatke Mesh Collidera.", this);
            return;
        }

        Bounds bounds = terrain.bounds;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        GameObject forest = new GameObject("Drzewa");
        Undo.RegisterCreatedObjectUndo(forest, "Rozmiesc drzewa");

        int placed = 0;
        for (int attempt = 0; attempt < treeCount * 10 && placed < treeCount; attempt++)
        {
            Vector3 origin = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.max.y + 10f,
                Random.Range(bounds.min.z, bounds.max.z)
            );

            Ray ray = new Ray(origin, Vector3.down);
            if (!terrain.Raycast(ray, out RaycastHit hit, bounds.size.y + 20f))
                continue;

            if (Vector3.Angle(hit.normal, Vector3.up) > maxSlope)
                continue;

            GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(
                treePrefab, forest.transform);
            tree.transform.SetPositionAndRotation(
                hit.point + Vector3.up * heightOffset,
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
            );

            Undo.RegisterCreatedObjectUndo(tree, "Rozmiesc drzewa");
            placed++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"Rozmieszczono {placed} z {treeCount} drzew.", this);
    }
#endif
}