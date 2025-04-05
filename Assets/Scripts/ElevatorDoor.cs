using System.Collections;
using UnityEngine;

public class ElevatorDoor : MonoBehaviour
{
    [SerializeField]
    private Transform doorLeftTransform;
    [SerializeField]
    private Transform doorRightTransform;

    [SerializeField]
    private Vector2 doorSize = new(10.0f, 10.0f);
    [SerializeField]
    private float doorThinkness = 0.1f;

    public void SetElevatorDoorSize(Vector2 size)
    {
        doorSize = size;
        UpdateDoorSize();
    }

    private void UpdateDoorSize()
    {
        if (!doorLeftTransform || !doorRightTransform)
            return;
        doorLeftTransform.localScale = new Vector3(doorThinkness, doorSize.x, doorSize.y);
        doorRightTransform.localScale = new Vector3(doorThinkness, doorSize.x, doorSize.y);
    }

    public Coroutine DoorOpen()
    {
        return StartCoroutine(PlayDoorAnimation(true));
    }

    public Coroutine DoorClose()
    {
        return StartCoroutine(PlayDoorAnimation(false));
    }

    private IEnumerator PlayDoorAnimation(bool isOpen)
    {
        while (true)
        {
            yield return null;
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        doorLeftTransform = transform.Find("DoorLeft");
        doorRightTransform = transform.Find("DoorRight");
        if (doorLeftTransform == null || doorRightTransform == null)
        {
            Debug.LogError("DoorLeft or DoorRight not found. Please assign them in the inspector.");
            return;
        }
        UpdateDoorSize();
    }
    private void OnValidate()
    {
        UpdateDoorSize();
    }
#endif
}
