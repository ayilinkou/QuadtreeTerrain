using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;
    public float verticalSpeed = 5f;
    public float boostMultiplier = 2f;

    [Header("Mouse Look")]
    public Transform camTransform;
    public float mouseSens = 0.2f;
    public bool bLockCursor = true;

    private float pitch = 0f;
    private float yaw = 0f;

	private void Awake()
	{
		if (bLockCursor)
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
	}

	void Start()
    {
        if (camTransform == null)
            camTransform = Camera.main.transform;

        Vector3 euler = camTransform.eulerAngles;
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

        camTransform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f); // rotate player horizontally
	}

    private void HandleMovement()
    {
        float speed = moveSpeed;
        if (Keyboard.current.leftShiftKey.isPressed)
            speed *= boostMultiplier;

        Vector2 horizontalMove = Vector2.zero;
		if (Keyboard.current.aKey.isPressed) horizontalMove.x -= 1f;
		if (Keyboard.current.dKey.isPressed) horizontalMove.x += 1f;
		if (Keyboard.current.sKey.isPressed) horizontalMove.y -= 1f;
		if (Keyboard.current.wKey.isPressed) horizontalMove.y += 1f;
		
        horizontalMove *= speed;

        float verticalMove = 0f;
        if (Keyboard.current.eKey.isPressed) verticalMove += 1f;
        if (Keyboard.current.qKey.isPressed) verticalMove -= 1f;
        verticalMove *= verticalSpeed;

        Vector3 totalMove = new Vector3(horizontalMove.x, verticalMove, horizontalMove.y);
        transform.position += totalMove * Time.deltaTime;
	}
}
