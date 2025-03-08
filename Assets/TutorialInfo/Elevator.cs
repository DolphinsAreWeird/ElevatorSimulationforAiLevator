using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Elevator : MonoBehaviour
{
    private int currentFloor = 0;           // Current floor the elevator is on
    private List<int> requestedFloors = new List<int>(); // List of floors to visit
    public float floorHeight = 3f;         // Vertical distance between floors
    public float speed = 2f;               // Speed of elevator movement

    // Public property to get the current floor
    public int CurrentFloor => currentFloor;

    // Check if the elevator can accept more passengers (placeholder)
    public bool CanEnter(float weight)
    {
        return true; // For now, always allow entry
    }

    // Add a passenger to the elevator
    public void AddPassenger(GameObject passenger)
    {
        passenger.transform.SetParent(transform);
        passenger.transform.localPosition = Vector3.zero; // Center inside elevator
        Debug.Log("Passenger added: " + passenger.name);
    }

    // Request a floor for the elevator to visit
    public void RequestFloor(int floor)
    {
        if (!requestedFloors.Contains(floor) && floor != currentFloor)
        {
            requestedFloors.Add(floor);
            if (requestedFloors.Count == 1)
            {
                StartCoroutine(MoveElevator());
            }
            Debug.Log("Floor requested: " + floor);
        }
    }

    // Coroutine to move the elevator to requested floors
    private IEnumerator MoveElevator()
    {
        Debug.Log("Starting elevator movement");
        while (requestedFloors.Count > 0)
        {
            int targetFloor = requestedFloors[0];
            float targetY = targetFloor * floorHeight;
            Vector3 targetPosition = new Vector3(transform.position.x, targetY, transform.position.z);

            while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
                yield return null;
            }

            transform.position = targetPosition;
            currentFloor = targetFloor;
            requestedFloors.RemoveAt(0);
            Debug.Log("Elevator arrived at floor " + currentFloor);

            // Notify passengers to exit
            NotifyPassengers();
        }
    }

    // Notify passengers when arriving at a floor
    private void NotifyPassengers()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            Person person = child.GetComponent<Person>();
            if (person != null && person.targetFloor == currentFloor)
            {
                person.ExitElevator();
            }
        }
    }
}