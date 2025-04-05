using UnityEngine;

public sealed class Floor : MonoBehaviour
{
    [SerializeField]
    private Transform floorMeshTransform = null;
    [SerializeField]
    private Vector2 floorSize = new(10.0f, 10.0f);
    [SerializeField]
    private float floorThinkness = 0.1f;

    public void SetFloorSize(Vector2 size)
    {
        floorSize = size;
        UpdateFloorSize();
    }

    private void UpdateFloorSize()
    {
        if (!floorMeshTransform)
            return;
        floorMeshTransform.localScale = new Vector3(floorSize.x, floorThinkness, floorSize.y);
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
