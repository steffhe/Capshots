using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateTowards : MonoBehaviour
{
    public float moveSpeed = 1f;
    public GameObject plane;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (UnityEngine.InputSystem.Mouse.current == null || Camera.main == null) return;
        Vector2 mousePosition = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

        Ray cameraRay = Camera.main.ScreenPointToRay(mousePosition);


        var filter = GetComponent<MeshFilter>();
        Vector3 normal = plane.transform.position;

        if (filter && filter.mesh.normals.Length > 0)
            normal = filter.transform.TransformDirection(filter.mesh.normals[0]);

        var planecomp = new Plane(normal, plane.transform.position);
        Plane groundPlane = planecomp;
        float rayLength;

        if (groundPlane.Raycast(cameraRay, out rayLength))
        {
            Vector3 pointToLook = cameraRay.GetPoint(rayLength);
            Debug.DrawLine(cameraRay.origin, pointToLook, Color.cyan);

            transform.LookAt(new Vector3(pointToLook.x, pointToLook.y, pointToLook.z));
        }
    }
}
