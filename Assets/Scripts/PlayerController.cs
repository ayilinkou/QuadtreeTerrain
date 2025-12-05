using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;
    public float verticalSpeed = 5f;
    // move faster by holding LShift
    public float boostMultiplier = 2f;

    [Header("Mouse Look")]
    public float mouseSens = 0.2f;
    public bool lockCursor = true;

    private float pitch = 0f;
    private float yaw = 0f;

    private void OnEnable()
    {
        if (lockCursor)
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
    }

	private void Awake()
	{
		if (lockCursor)
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
	}

	void Start()
    {
        if (lockCursor)
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
        
        Vector3 euler = transform.eulerAngles;
        pitch = euler.x;
        yaw = euler.y;
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
    }

    private void HandleMouseLook()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        yaw += mouseDelta.x * mouseSens;
        pitch -= mouseDelta.y * mouseSens;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
	}

    private void HandleMovement()
    {
        float multiplier = 1f;
        if (Keyboard.current.leftShiftKey.isPressed)
            multiplier *= boostMultiplier;

        Vector3 horizontalMove = Vector3.zero;
        if (Keyboard.current.wKey.isPressed) horizontalMove += transform.forward;
        if (Keyboard.current.sKey.isPressed) horizontalMove -= transform.forward;
        if (Keyboard.current.dKey.isPressed) horizontalMove += transform.right;
        if (Keyboard.current.aKey.isPressed) horizontalMove -= transform.right;

        horizontalMove.Normalize();
        horizontalMove *= moveSpeed * multiplier;

        float verticalMove = 0f;
        if (Keyboard.current.eKey.isPressed) verticalMove += 1f;
        if (Keyboard.current.qKey.isPressed) verticalMove -= 1f;

        verticalMove *= verticalSpeed * multiplier;

        Vector3 totalMove = new Vector3(horizontalMove.x, horizontalMove.y + verticalMove, horizontalMove.z);
        transform.position += totalMove * Time.deltaTime;
	}
}
