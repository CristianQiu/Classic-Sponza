using UnityEngine;

public class Rotate : MonoBehaviour
{
    public Vector3 speeds;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(speeds * Time.deltaTime, Space.Self);
    }
}
