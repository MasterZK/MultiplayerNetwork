using UnityEngine;
using Mirror;

public class MovementPlayer : NetworkBehaviour
{
    [Header("Player Attributes")]
    [SerializeField] private float speed = 1f;
    [SerializeField] private bool isSprinting;
    [SerializeField] private float sprintSpeedModifier = 1.5f;
    [SerializeField] private bool canMove = true;
    [SerializeField] private bool canJump = false;
    [SerializeField] private bool isGrounded;
    [SerializeField] private float jumpForce = 1f;
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float lowJumpMultiplier = 2f;

    [Header("Unity Components")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private GameObject playerObject;
    [SerializeField] private Transform playerRotationPoint;
    private Rigidbody playerRb;
    private Vector3 direction;

    private void Start()
    {
        playerRb = gameObject.GetComponent<Rigidbody>();
        if (this.isLocalPlayer)
            playerObject.SetActive(false);

        if (!this.isLocalPlayer)
            playerCamera.gameObject.SetActive(false);
    }

    private void FixedUpdate()
    {
        if (!this.isLocalPlayer)
            return;

        SprintCheck();
        SprintCamera();
        Jump();
        InitMovement();
        LookDirection(new Vector3(0.0f, playerCamera.transform.eulerAngles.y, 0.0f));
    }

    private void LookDirection(Vector3 lookDirection)
    {
        var cameraDirection = playerCamera.transform.eulerAngles.y;
        var lookDirectionVector = Quaternion.Euler(0, cameraDirection, 0) * Vector3.forward;
        playerRotationPoint.rotation = Quaternion.LookRotation(lookDirectionVector);
    }

    #region Jump
    private void Jump()
    {
        if (!isGrounded || !canJump)
            return;

        if (Input.GetKey(KeyCode.Space))
        {
            playerRb.velocity += Vector3.up * jumpForce;
        }

        if (playerRb.velocity.y < 0)
        {
            playerRb.velocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.deltaTime;
        }
        else if (playerRb.velocity.y > 0 && !Input.GetButton("Jump"))
        {
            playerRb.velocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime;
        }
    }

    private void OnCollisionStay(Collision other)
    {
        if (other.gameObject.CompareTag("floor"))
            isGrounded = true;
    }

    private void OnCollisionExit(Collision other)
    {
        isGrounded = false;
    }
    #endregion
    
    #region Sprint
    private void SprintCheck()
    {
        isSprinting = false;
        if (Input.GetKey(KeyCode.LeftShift))
            isSprinting = true;
    }

    private void SprintCamera()
    {
        if (isSprinting)
            playerCamera.fieldOfView = 70;
        else
            playerCamera.fieldOfView = 60;
    }
    #endregion

    #region Movement
    private void InitMovement()
    {
        if (!canMove)
            return;

        MoveForward();
        MoveBackwards();
        MoveLeft();
        MoveRight();
        Move(direction);
    }

    private void Move(Vector3 direction)
    {
        if (!isGrounded)
            direction = direction / 2;
        playerRb.MovePosition(this.transform.position + (direction * Time.deltaTime * speed));
        this.direction = Vector3.zero;
    }

    private Vector3 CalDirection(Vector3 moveDirection)
    {
        var cameraDirection = playerCamera.transform.eulerAngles.y;
        var finalMoveDirection = Quaternion.Euler(0, cameraDirection, 0) * moveDirection;
        //LookDirection(finalMoveDirection);
        return finalMoveDirection;
    }

    private void MoveForward()
    {
        if (!Input.GetKey(KeyCode.W))
            return;

        var vector = Vector3.forward;
        if (isSprinting)
            vector *= sprintSpeedModifier;

        direction += CalDirection(vector);
    }

    private void MoveBackwards()
    {
        if (!Input.GetKey(KeyCode.S))
            return;

        direction += CalDirection(Vector3.back);
    }

    private void MoveRight()
    {
        if (!Input.GetKey(KeyCode.D))
            return;

        direction += CalDirection(Vector3.right);
    }

    private void MoveLeft()
    {
        if (!Input.GetKey(KeyCode.A))
            return;

        direction += CalDirection(Vector3.left);
    }
    #endregion
}
