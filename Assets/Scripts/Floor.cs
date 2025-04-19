using UnityEngine;

public sealed class Floor : MonoBehaviour
{
    [SerializeField]
    private Transform floorMeshTransform = null;
    [SerializeField]
    private Vector2 floorSize = new(10.0f, 10.0f);
    [SerializeField]
    private float floorThickness = 0.1f;

    public void SetFloorSize(Vector2 size)
    {
        floorSize = size;
        UpdateFloorSize();
    }

    private void UpdateFloorSize()
    {
        if (!floorMeshTransform) return;
        floorMeshTransform.localScale = new Vector3(floorSize.x, floorThickness, floorSize.y);

        // Add or adjust collider so agents can’t walk right through
        var col = floorMeshTransform.GetComponent<BoxCollider>() 
                ?? floorMeshTransform.gameObject.AddComponent<BoxCollider>();
        col.size   = new Vector3(floorSize.x, floorThickness, floorSize.y);
        col.center = Vector3.zero;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        floorMeshTransform = transform.Find("FloorMesh");
        if (floorMeshTransform == null)
        {
            Debug.LogError("FloorMesh not found in the Floor object.");
            return;
        }
        UpdateFloorSize();
    }
    private void OnValidate()
    {
        UpdateFloorSize();
    }
#endif
}
