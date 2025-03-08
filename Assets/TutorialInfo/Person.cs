using UnityEngine;
using System.Collections;

public class Person : MonoBehaviour
{
    public int currentFloor = 0;   // Starting floor
    public int targetFloor = 0;    // Desired floor
    public float moveSpeed = 2f;   // Speed for optional movement
    private Elevator elevator;
    private bool waitingForElevator = false;

    void Start()
    {
        elevator = FindObjectOfType<Elevator>(); // Find the elevator
        StartCoroutine(DecideNextAction());
    }

    // Decide to request the elevator periodically
    IEnumerator DecideNextAction()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(5f, 15f));
            if (!waitingForElevator && transform.parent == null) // Not in elevator
            {
                targetFloor = Random.Range(0, 5); // Adjust 5 to your max floors
                if (targetFloor != currentFloor)
                {
                    waitingForElevator = true;
                    elevator.RequestFloor(currentFloor); // Call elevator
                    StartCoroutine(WaitForElevator());
                    Debug.Log("Person on floor " + currentFloor + " wants to go to floor " + targetFloor);
                }
            }
        }
    }

    // Wait for the elevator and enter it
    IEnumerator WaitForElevator()
    {
        while (waitingForElevator)
        {
            if (elevator.CurrentFloor == currentFloor && elevator.CanEnter(70f))
            {
                elevator.AddPassenger(gameObject);
                elevator.RequestFloor(targetFloor);
                waitingForElevator = false;
            }
            yield return null;
        }
    }

    // Exit the elevator at the target floor
    public void ExitElevator()
    {
        transform.parent = null;             // Unparent from elevator
        transform.position += new Vector3(2, 0, 0); // Move to the side
        currentFloor = targetFloor;
        Debug.Log("Person exited at floor " + currentFloor);
        // Optional: Uncomment to move to a random spot
        // StartCoroutine(MoveToRandomSpot());
    }

    // Optional: Move to a random spot on the floor
    IEnumerator MoveToRandomSpot()
    {
        Vector3 randomSpot = new Vector3(Random.Range(-4f, 4f), currentFloor * elevator.floorHeight, Random.Range(-4f, 4f));
        while (Vector3.Distance(transform.position, randomSpot) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, randomSpot, moveSpeed * Time.deltaTime);
            yield return null;
        }
    }
}