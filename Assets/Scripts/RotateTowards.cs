using UnityEngine;
using UnityEngine.EventSystems;

// Turns this object (the main menu model's gun holder) to point at the mouse cursor: at the point
// under it on the given plane. With a controller it points at the menu button that's selected
// instead, turning smoothly from one to the next as the selection moves.
public class RotateTowards : MonoBehaviour
{
    public float moveSpeed = 1f;
    public GameObject plane;

    // How quickly it turns to a newly selected button (the mouse is followed at once)
    private const float GamepadTurnSpeed = 10f;

    void Update()
    {
        if (Camera.main == null || plane == null) return;

        Vector2 screenPoint;
        bool smooth = false;
        if (MenuNavigation.UsingGamepad && SelectedOnScreen(out screenPoint))
        {
            smooth = true;
        }
        else
        {
            if (UnityEngine.InputSystem.Mouse.current == null) return;
            screenPoint = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        }

        Ray cameraRay = Camera.main.ScreenPointToRay(screenPoint);

        var filter = GetComponent<MeshFilter>();
        Vector3 normal = plane.transform.position;

        if (filter && filter.mesh.normals.Length > 0)
            normal = filter.transform.TransformDirection(filter.mesh.normals[0]);

        Plane groundPlane = new Plane(normal, plane.transform.position);
        float rayLength;

        if (groundPlane.Raycast(cameraRay, out rayLength))
        {
            Vector3 pointToLook = cameraRay.GetPoint(rayLength);
            Debug.DrawLine(cameraRay.origin, pointToLook, Color.cyan);

            Vector3 look = pointToLook - transform.position;
            if (look.sqrMagnitude < 1e-6f) return;
            Quaternion target = Quaternion.LookRotation(look);
            transform.rotation = smooth
                ? Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * GamepadTurnSpeed))
                : target;
        }
    }

    // Where on the screen the selected button's middle is, if a button is selected and showing
    private static bool SelectedOnScreen(out Vector2 point)
    {
        point = Vector2.zero;
        EventSystem events = EventSystem.current;
        GameObject selected = events != null ? events.currentSelectedGameObject : null;
        if (selected == null || !selected.activeInHierarchy) return false;
        RectTransform rect = selected.transform as RectTransform;
        if (rect == null) return false;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        point = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(rect.rect.center));
        return true;
    }
}
