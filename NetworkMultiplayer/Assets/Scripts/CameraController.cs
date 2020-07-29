using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    private enum CameraMode
    {
        FixedFirstPerson,       //Kamera ist an die Rotation des Objektes gebunden
        FirstPerson,            //Kamera steuert die Rotation des Objektes
        FreeFirstPerson,        //Kamera kann sich unabhänhig vom Objekt rotieren beinflusst es dabei aber auch nicht
        FixedThirdPerson,       //Third Person mit einer Standard Position zu der sich die Kamera selbst ausrichtet
        ThirdPerson,            //Third Person kann mit rechtsklick Kamera frei bewegen. Sie dreht sich danach wieder langsam zur Ausgangsposition
        FreeThirdPerson,        //Third Person frei beweglich um die Person herum
        TopDown,                //TopDown Kamera
        Free                    //Freie Kamera
    }

    #region Serialized
    [Header("Allgemein")]

    /// <summary>
    /// Das Objekt dem die Kamera folgt
    /// </summary>
    [SerializeField] private GameObject objectToFollow;
    [SerializeField] private CameraMode cameramode;

    [Space(5)]
    [SerializeField] private bool cursorVisible = false;
    [SerializeField] private bool drawDebugRay = true;
    [SerializeField] private bool avoidCameraCollision = false;
    [SerializeField] private bool mouseInvert = false;

    [Space(5)]
    [Range(0, 0.1f)]
    //Die Mausempfindlichkeit im Bezug auf die Bewegung der Kamera
    [SerializeField] private float mouseSensivity = 0.045f;

    [Space(5)]
    [SerializeField] private bool mouseWheelZoom = true;
    [Range(100, 2000)]
    //Der Zoom Speed, mit dem man mit dem Mausrad zoomen kann
    [SerializeField] private float zoomSpeed = 1500;

    [Space(5)]
    [SerializeField] private bool smoothRevert = false;
    //Das XYOffset die FreeThirdPerson Kamera als Standardposition zu der immer wieder zurückgekehrt wird mit SmoothRevert()
    [Range((float)-Math.PI, (float)Math.PI)]
    [SerializeField]private float freeRevertOffsetY = -1;
    [Range((float)-Math.PI, (float)Math.PI)]
    [SerializeField]private float freeRevertOffsetX = 0;

    [Space(10)]
    [Header("Third Person Only")]
    
    //Der tatsächliche aktuelle Radius mit dem die Kamera um das Objekt kreist
    [SerializeField] private float radius = 3;

    //Der Radius welcher nicht unterschritten werden darf
    [SerializeField] private float minRadius = 1.2f;

    [SerializeField] private float maxRadius = 10;


    [Space(5)]
    [Range(-Mathf.PI ,0)]
    //Die Grenze bis an welche die Kamera über dem Objekt rotiert werden kann
    [SerializeField] private float thresholdRotationTop = -0.45f;

    [Range(-Mathf.PI, 0)]
    //Die Grenze bis an welche die Kamera unter dem Objekt rotiert werden kann
    [SerializeField] private float thresholdRotationBottom = -2f;

    [Space(5)]
    //Die Rotation umd die y Achse des Objektes die die Kamera hat.
    [SerializeField] private float rotationOffsetX = 0f;

    [Space(5)]
    //Das Offset um die Kamera Positionierung zu verschieben
    [SerializeField] private Vector3 posOffset;
    [SerializeField] private Vector3 posOffsetMin;
    [SerializeField] private Vector3 posOffsetMax;

    [Header("First Person Only")]
    [SerializeField] private float fovZoomSpeed = 2000;

    [Header("Free & Top Down Mode")]
    [SerializeField] private float movementSpeed = 5;
    [SerializeField] private List<KeyCode> movementBoost = new List<KeyCode>() {KeyCode.LeftShift,KeyCode.RightShift};

    #endregion

    #region Not Serialized
    /// <summary>
    /// Der Radius den die Kamera zur Zeit maximal vom Objekt weg sein darf aufgrund der Beschränkung eines Objketes welches im Weg ist.
    /// </summary>
    private float currentMaxRadius;

    private string inputX = "Mouse X";
    private string inputY = "Mouse Y";
    private string inputZoom = "Mouse ScrollWheel";

    private float startFOV;
    private Camera thisCamera;
    private Vector3 offset;


    /// <summary>
    /// Der Radius den die Kugel des Raycasts hat, welcher Hindernisse aufspüren soll.
    /// </summary>
    private float sphereRayRadius = 0.25f;
    private float rayLengthOffset = 0.2f;

    //Daten die einmal zu beginn festgelegt werden und unveränderlich sind!
    //Wird für die Fixed Third Person Kamera benötigt
    private float rotationOffsetY = (float)-Math.PI / 2;

    private float thirdPersonRadius;

    /// <summary>
    /// Stellt einen Grenzwert dar, der für die smothe Bewegung der Kamera benötigt wird.
    /// </summary>
    private const float LIMIT = 0.01f;

    /// <summary>
    /// Stellt die Position des zu folgenden Objektes im letzten Frame dar
    /// Wird zur Überprüfung einer Positionsänderung benötigt.
    /// </summary>
    private Vector3 objectToFollowPosition;

    /// <summary>
    /// Ist die Kamera in diesem Frame mit etwas kollidiert?
    /// </summary>
    private bool collisionInThisFrame = false;

    #endregion

    #region Unity Funktionen
    /// <summary>
    /// Legt zu Beginn die Standardwerte einmalig fest
    /// </summary>
    private void Start()
    {
        thisCamera = GetComponent<Camera>();
        startFOV = thisCamera.fieldOfView;
        objectToFollowPosition = objectToFollow.transform.position;

        currentMaxRadius = radius;
        thirdPersonRadius = radius;
    }

    /// <summary>
    /// Cameramovement in LateUpdate because objectToFollow may move in Update()
    /// </summary>
    private void LateUpdate()
    {
        ToggleCursorVisibility();
        MouseZoom();
        CalculateOffset();
        SwitchOnMode();
    }

    /// <summary>
    /// Draw Sphere for Debugging
    /// </summary>
    private void OnDrawGizmos()
    {
        if (drawDebugRay)
        {
            Vector3 direction = (transform.position - objectToFollowPosition).normalized * (rayLengthOffset - sphereRayRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + direction, sphereRayRadius);

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(objectToFollow.transform.position + offset, 0.1f);

            Gizmos.color = Color.red;
            //Gizmos.DrawSphere(objectToFollow.transform.position + lookAtOffset, 0.1f);
            Gizmos.DrawLine(transform.position, objectToFollow.transform.position + objectToFollow.transform.forward * 10000);
            Gizmos.DrawLine(objectToFollow.transform.position, objectToFollow.transform.forward * 10000);
        }
    }

    /// <summary>
    /// Überprüft, dass keine unsinnigen Maximal oder minimal Werte für die Rotation angegeben werden.
    /// </summary>
    private void OnValidate()
    {
        if (thresholdRotationTop < thresholdRotationBottom)
        {
            thresholdRotationTop = thresholdRotationBottom;
        }
    }

    #endregion

    #region GoMode
    /// <summary>
    /// Prüft in jedem Frame welche Kamera Einstellung verwendet wird
    /// </summary>
    private void SwitchOnMode()
    {
        switch (cameramode)
        {
            case CameraMode.FixedFirstPerson:
            case CameraMode.FirstPerson:
            case CameraMode.FreeFirstPerson:
                GoFirstPerson();
                break;
            case CameraMode.FixedThirdPerson:
            case CameraMode.ThirdPerson:
            case CameraMode.FreeThirdPerson:
                GoThirdPerson();
                break;
            case CameraMode.TopDown:
                GoTopDownMode();
                break;
            case CameraMode.Free:
                GoFreeMode();
                break;
        }
    }

    /// <summary>
    /// Stellt die Sichtbarkeit des Cursors ein
    /// </summary>
    private void ToggleCursorVisibility()
    {
        if (cursorVisible)
        {
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
        }
        else 
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>
    /// Ermöglicht es die Kamera Frei vom Objekt zu bewegen
    /// </summary>
    private void GoFreeMode()
    {
        MouseMovement();
        FreeCameraPositioning();
        LookAt();
    }

    /// <summary>
    /// Lässt die Bewegung der Kamera nur in der Horizontalen Ebene zu dem Objekt zu.
    /// </summary>
    private void GoTopDownMode()
    {
        MouseZoom();
        TopDownCameraPositioning();
        LookAt();
    }

    /// <summary>
    /// Schaltet die First Person Kamera ein
    /// </summary>
    private void GoFirstPerson()
    {
        //Schalte First Person Kamera an
        radius = 0;
        currentMaxRadius = 0;

        switch (cameramode)
        {
            case CameraMode.FixedFirstPerson:
                break;
            case CameraMode.FirstPerson:
            case CameraMode.FreeFirstPerson:
                    MouseMovement();
                break;
        }

        SphereCameraPositioning();
        MouseFovZoom();
    }

    /// <summary>
    /// Schaltet die Third Person Kamera ein
    /// </summary>
    private void GoThirdPerson()
    {
        if (avoidCameraCollision)
        {
            BlindDetection();
        }

        switch (cameramode)
        {
            case CameraMode.FixedThirdPerson:
                break;
            case CameraMode.ThirdPerson:
                if (Input.GetButton("Fire2"))
                    MouseMovement();
                break;
            case CameraMode.FreeThirdPerson:
                //if (Input.GetButton("Fire2"))
                    MouseMovement();
                    //SmoothRevert();
                //else 
                break;
        }

        SphereCameraPositioning();
        ResetCameraController();
    }

    #endregion

    #region Positioning
    /// <summary>
    /// Positioniert die Kamera anhand der Variablen OffsetX und OffsetY um das zu verfolgende Objekt
    /// </summary>
    private void SphereCameraPositioning()
    {
        switch (cameramode)
        {
            case CameraMode.FixedFirstPerson:
            case CameraMode.FirstPerson:
                transform.position = objectToFollow.transform.position;
                LookAt();
                break;
            case CameraMode.FixedThirdPerson:
            case CameraMode.ThirdPerson:
                LookAt();
                transform.position = objectToFollow.transform.position + objectToFollow.transform.TransformDirection(posOffset) ;
                break;
            case CameraMode.FreeFirstPerson:
            case CameraMode.FreeThirdPerson:
                //Aufgrund der Verwendung von offset muss LookAt() nach der Positionierung aufgerufen werden
                transform.position = objectToFollow.transform.position + objectToFollow.transform.TransformDirection(offset * radius);
                LookAt();
                break;
            default:
                break;
        }

    }

    /// <summary>
    /// Berechnet das Offset für die Kamera Positionierung
    /// </summary>
    private void CalculateOffset()
    {
        rotationOffsetX = rotationOffsetX < -Math.PI ? rotationOffsetX + 2 * (float)Math.PI : rotationOffsetX;
        rotationOffsetX = rotationOffsetX > Math.PI ? rotationOffsetX - 2 * (float)Math.PI : rotationOffsetX;

        //Calculate sphere
        offset = new Vector3(
            Mathf.Sin(rotationOffsetX) * Mathf.Sin(rotationOffsetY),
            Mathf.Cos(rotationOffsetY),
            Mathf.Cos(rotationOffsetX) * Mathf.Sin(rotationOffsetY));
    }

    /// <summary>
    /// Ermöglicht es der kamera sich im Free mode selbst zu bewegen
    /// </summary>
    private void FreeCameraPositioning()
    {
        transform.position += transform.right * Time.deltaTime * movementSpeed * Input.GetAxis("Horizontal") * (movementBoost.TrueForAll(k=>!Input.GetKey(k))?1:2);
        transform.position += transform.forward * Time.deltaTime * movementSpeed * Input.GetAxis("Vertical") * (movementBoost.TrueForAll(k => !Input.GetKey(k)) ? 1 : 2);
    }

    /// <summary>
    /// Ermöglicht es der Kamera sich im TopDown Modus über dem Objekt hinweg zu bewegen
    /// </summary>
    private void TopDownCameraPositioning()
    {
        transform.position = new Vector3(transform.position.x, objectToFollow.transform.position.y + currentMaxRadius , transform.position.z);
        transform.position += transform.right * Time.deltaTime * movementSpeed * Input.GetAxis("Horizontal") * (movementBoost.TrueForAll(k => !Input.GetKey(k)) ? 1 : 2);
        transform.position += transform.up * Time.deltaTime * movementSpeed * Input.GetAxis("Vertical") * (movementBoost.TrueForAll(k => !Input.GetKey(k)) ? 1 : 2);
    }

    /// <summary>
    /// Entscheidet anhand des Kameramodus wohin die Kamera schaut
    /// </summary>
    private void LookAt()
    {
        switch (cameramode)
        {
            case CameraMode.FixedFirstPerson:
                transform.LookAt(objectToFollow.transform.forward + objectToFollow.transform.position);
                break;
            case CameraMode.FirstPerson:
                objectToFollow.transform.LookAt(objectToFollow.transform.position + new Vector3(offset.x, offset.y * -1, offset.z));
                transform.LookAt(objectToFollow.transform.forward + objectToFollow.transform.position); 
                break;
            case CameraMode.FreeFirstPerson:
                transform.LookAt(objectToFollow.transform.position + new Vector3(offset.x,offset.y * -1,offset.z));
                break;
            case CameraMode.FixedThirdPerson:
                transform.LookAt(objectToFollow.transform.position + objectToFollow.transform.forward * 10000);
                break;
            case CameraMode.ThirdPerson:
                objectToFollow.transform.LookAt(objectToFollow.transform.position - offset);
                transform.LookAt(objectToFollow.transform.position + objectToFollow.transform.forward * 10000);
                break;
            case CameraMode.FreeThirdPerson:
                transform.LookAt(objectToFollow.transform.position );
                break;
            case CameraMode.TopDown:
                transform.LookAt(objectToFollow.transform.up * -1 + transform.position);
                break;
            case CameraMode.Free:
                transform.LookAt(transform.position + new Vector3(offset.x, offset.y * -1, offset.z));
                break;
        }

    }

    /// <summary>
    /// Bewegt die Kamera zu ihrer Ausgangsposition zurück
    /// </summary>
    private void SmoothRevert()
    {
        if (smoothRevert)
        {
            float tempX = ((freeRevertOffsetX - rotationOffsetX) * Time.deltaTime);
            float tempY = ((freeRevertOffsetY - rotationOffsetY) * Time.deltaTime);

            rotationOffsetX += tempX > 0 ? Math.Max(LIMIT, tempX) : tempX < 0 ? Math.Min(-LIMIT, tempX) : 0;
            rotationOffsetY += tempY > 0 ? Math.Max(LIMIT, tempY) : tempY < 0 ? Math.Min(-LIMIT, tempY) : 0;

            rotationOffsetX = Math.Abs(freeRevertOffsetX - rotationOffsetX) < LIMIT ? freeRevertOffsetX : rotationOffsetX;
            rotationOffsetY = Math.Abs(freeRevertOffsetY - rotationOffsetY) < LIMIT ? freeRevertOffsetY : rotationOffsetY;
            radius = Mathf.Clamp(radius + currentMaxRadius * Time.deltaTime, minRadius, currentMaxRadius);
        }
    }

    /// <summary>
    /// Ändert die Variablen OffsetX und OffsetY anhand der Mausbewegung
    /// </summary>
    private void MouseMovement()
    {
        float xAxis = Input.GetAxis(inputX);
        float yAxis = Input.GetAxis(inputY);

        //Set offset for mouse x axis
        rotationOffsetX += xAxis * mouseSensivity;

        //Set offset for mouse y axis
        //Check if offset goes over maximumHead or under minimumBotton
        rotationOffsetY += rotationOffsetY > thresholdRotationTop &&
            -yAxis > 0 ||
            rotationOffsetY < thresholdRotationBottom &&
            -yAxis < 0 ?
            0 : Mathf.Clamp(-yAxis, -1, 1) * mouseSensivity * (mouseInvert? -1:1);

        //Check offset is between minimum and maximum
        rotationOffsetY = Mathf.Clamp(rotationOffsetY, thresholdRotationBottom, thresholdRotationTop);
    }

    /// <summary>
    /// Ändert currentMaxRadius wenn mit dem Mausrad gescrollt wird
    /// currentMaxRadius wird zwischen minRadius und maxRadius geclampt
    /// </summary>
    private void MouseZoom()
    {
        //Wenn gescrollt wird ändere den currentMaxRadius
        if (mouseWheelZoom)
        {
            currentMaxRadius = Mathf.Clamp(currentMaxRadius - Input.GetAxis(inputZoom) * Time.deltaTime * zoomSpeed,
                minRadius,
                maxRadius);            
        }
    }

    /// <summary>
    /// Ändert das FOV um damit rein Zoomen zu können
    /// </summary>
    private void MouseFovZoom()
    {
        if (mouseWheelZoom)
        {
            thisCamera.fieldOfView = Mathf.Clamp(thisCamera.fieldOfView - Input.GetAxis(inputZoom) * Time.deltaTime * fovZoomSpeed,
                20,
                startFOV);
        }
    }

    /// <summary>
    /// Reset Values for next Frame
    /// </summary>
    private void ResetCameraController()
    {
        float temp = Mathf.Max(Mathf.Abs(radius-currentMaxRadius),LIMIT) * Time.deltaTime;

        if (radius < currentMaxRadius && !collisionInThisFrame)
        {
            //radius = Mathf.Clamp(radius + currentMaxRadius * Time.deltaTime, minRadius, currentMaxRadius);
            radius = Mathf.Min(radius + temp, currentMaxRadius);
        }
        else if (radius > currentMaxRadius && !collisionInThisFrame)
        {
            radius = Mathf.Max(radius - temp, currentMaxRadius);
        }

        if (Input.GetAxis(inputZoom) != 0 || (Input.GetButton("Fire2") && (Input.GetAxis(inputX) != 0 || Input.GetAxis(inputY) != 0)) || objectToFollowPosition != objectToFollow.transform.position)
        {
            collisionInThisFrame = false;
            objectToFollowPosition = objectToFollow.transform.position;
        }

            posOffset = posOffsetMin + (posOffsetMax - posOffsetMin) * (radius - minRadius) / (maxRadius - minRadius);

    }

    #endregion

    #region Raycast Functions

    /// <summary>
    /// Detect obstacle under the camera
    /// </summary>
    private void BlindDetection()
    {
        RaycastHit raycastHit;
        Vector3 direction;

        if (Physics.SphereCast(objectToFollowPosition, sphereRayRadius, direction = transform.position - objectToFollowPosition, out raycastHit, radius + rayLengthOffset))
        {
            if (raycastHit.transform.gameObject != objectToFollow)
            {
                OnRayCastHit(raycastHit, direction, Color.red);
            }
        }
    }

    /// <summary>
    /// What to do when a Raycast hits an object
    /// </summary>
    /// <param name="raycastHit">RaycastHit object</param>
    private void OnRayCastHit(RaycastHit raycastHit, Vector3 drawDirection, Color drawColor)
    {
        if (raycastHit.collider.gameObject != gameObject)
        {
            //Set camera in front of obstacle
            collisionInThisFrame = true;
            radius = (raycastHit.point - objectToFollowPosition).magnitude - rayLengthOffset;
            radius = Mathf.Max(minRadius, radius);

            if (drawDebugRay)
            {
                RayDraw(drawDirection, drawColor);
            }
        }
    }

    /// <summary>
    /// Draw the active Ray in Editor
    /// </summary>
    private void RayDraw(Vector3 direction, Color color)
    {
        Debug.DrawRay(objectToFollowPosition, (direction).normalized * ((transform.position - objectToFollowPosition).magnitude + rayLengthOffset), color);
    }

    #endregion

}

