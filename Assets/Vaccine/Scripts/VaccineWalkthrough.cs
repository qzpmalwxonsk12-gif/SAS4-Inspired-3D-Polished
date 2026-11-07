using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace Vaccine
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class VaccineWalkthrough : MonoBehaviour
    {
        public Camera eye, overview;
        public GameObject roofs, mapLabels, marker;
        public TMP_Text prompt, area;
        public float walkSpeed = 5, sensitivity = .09f;
        public bool missionMode;
        public bool Captured { get; private set; }
        public bool MapVisible { get; private set; }
        public InputActionMap Actions { get; private set; }
        CharacterController body;
        Vector3 spawn;
        Quaternion spawnRotation;
        float pitch, fall;
        void Awake()
        {
            body = GetComponent<CharacterController>(); spawn = transform.position; spawnRotation = transform.rotation;
            Actions = new InputActionMap("Vaccine walkthrough");
            Actions.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2").AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Actions.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            Actions.AddAction("Map", InputActionType.Button, "<Keyboard>/tab");
            Actions.AddAction("Reset", InputActionType.Button, "<Keyboard>/enter");
            Actions.AddAction("Release", InputActionType.Button, "<Keyboard>/escape");
            Actions.AddAction("Focus", InputActionType.Button, "<Mouse>/leftButton");
            Actions.AddAction("Interact", InputActionType.Button, missionMode?"<Keyboard>/f":"<Keyboard>/e");
        }
        void OnEnable() => Actions.Enable();
        void OnDisable() { Actions?.Disable(); Capture(false); }
        void OnDestroy() => Actions?.Dispose();
        void Start() { Application.targetFrameRate = 60; SetMap(false); }
        void OnApplicationFocus(bool focused) { if (!focused) Capture(false); }
        public void Capture(bool value)
        {
            Captured = value && !MapVisible;
            Cursor.lockState = Captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !Captured;
        }
        public void SetMap(bool value)
        {
            MapVisible = value; eye.enabled = !value; overview.enabled = value;
            roofs.SetActive(!value); mapLabels.SetActive(value); marker.SetActive(value); Capture(!value);
        }
        public void ResetPosition()
        {
            body.enabled = false; transform.SetPositionAndRotation(spawn, spawnRotation); body.enabled = true;
            pitch = 0; fall = 0; eye.transform.localRotation = Quaternion.identity; SetMap(false);
        }
        public void SetPose(Vector3 position, Quaternion facing, Quaternion view)
        {
            body.enabled=false;transform.SetPositionAndRotation(position,facing);body.enabled=true;
            pitch=view.eulerAngles.x;if(pitch>180)pitch-=360;pitch=Mathf.Clamp(pitch,-85,85);fall=0;
            eye.transform.localRotation=Quaternion.Euler(pitch,0,0);
        }
        void Update()
        {
            if(missionMode&&VaccineMission.Instance&&VaccineMission.Instance.Paused){Capture(false);return;}
            if (Actions["Map"].WasPressedThisFrame()) SetMap(!MapVisible);
            if (!missionMode&&Actions["Release"].WasPressedThisFrame()) Capture(false);
            if (!missionMode&&(Actions["Reset"].WasPressedThisFrame() || transform.position.y < -15)) ResetPosition();
            if (MapVisible) { area.text = "VACCINE / SURFACE PLAN"; prompt.text = "TAB  Return to first person"; return; }
            if (!Captured && Actions["Focus"].WasPressedThisFrame()) Capture(true);
            if (Captured)
            {
                Vector2 look = Actions["Look"].ReadValue<Vector2>() * sensitivity;
                transform.Rotate(0, look.x, 0); pitch = Mathf.Clamp(pitch - look.y, -85, 85);
                eye.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            var p = transform.position;
            area.text = "VACCINE / " + (p.y < -1.5f ? "PLATFORM ACCESS" : p.x < 0 && p.z < 0 ? "HELICOPTER PARKING LOT" :
                p.x < -8 && p.z > 27 ? "BACKUP POWER ROOM" : p.x >= 60.8f && p.x < 79.8f && p.z < 6 && p.z >= -40 ? "WAITING ROOM" :
                p.x >= 50 && p.z >= -29.2f ? "STATION CORRIDOR" : p.z < -29.2f && p.x >= 0 ? "STATION PASSAGE" :
                p.x >= 0 && p.x < 33 && p.z < 0 ? "TICKET HALL" : "CRASH COURTYARD");
            ReviewDoor door = null;
            if (Captured && Physics.Raycast(eye.transform.position, eye.transform.forward, out var hit, 4, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                door = hit.collider.GetComponentInParent<ReviewDoor>();
            if(door&&door.GetComponent<VaccineBreakableDoor>())door=null;
            prompt.text = !Captured ? "Click to look around" : door != null ? (missionMode?"F  ":"E  ") + (door.Open ? "Close " : "Open ") + door.roomName : "";
            if (door != null && Actions["Interact"].WasPressedThisFrame()) door.Toggle();
            Vector2 axis = Captured ? Actions["Move"].ReadValue<Vector2>() : Vector2.zero;
            if (body.isGrounded && fall < 0) fall = -2;
            fall = Mathf.Max(-30, fall - 20 * Time.deltaTime);
            Vector3 move = transform.TransformDirection(new Vector3(axis.x, 0, axis.y));
            body.Move((Vector3.ClampMagnitude(move, 1) * walkSpeed + Vector3.up * fall) * Time.deltaTime);
            // Prevent getting wedged in exterior inter-car notches on Platform 1 or Platform 2 trains
            if (transform.position.y < -8.5f)
            {
                Vector3 pos = transform.position;
                bool inNorthGangway = (pos.x > 116.75f && pos.x < 118.55f) || (pos.x > 135.05f && pos.x < 136.85f);
                if (inNorthGangway && pos.z > 17.5f && pos.z < 25.8f && (pos.z < 20.5f || pos.z > 22.7f))
                {
                    pos.z = Mathf.Clamp(pos.z, 20.55f, 22.65f);
                    pos.y = Mathf.Max(pos.y, -11.92f);
                    body.enabled = false; transform.position = pos; body.enabled = true;
                }
            }
        }
    }
}
