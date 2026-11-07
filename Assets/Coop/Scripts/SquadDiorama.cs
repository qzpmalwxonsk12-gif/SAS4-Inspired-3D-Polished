using UnityEngine;
namespace SASCoop
{
    // A live 3D dropship and warzone diorama: no video or pre-rendered image is used by the menu.
    public sealed class SquadDiorama : MonoBehaviour
    {
        public Transform[] operators = new Transform[4];
        public Transform portraitCamera;
        public Transform menuCamera;
        public Light selectionLight;
        public Transform holoDisplay;
        public static int HoveredRole = -1;

        Vector3 cameraOrigin;
        Vector3 menuCamOrigin;
        Quaternion menuCamRotOrigin;

        void Start()
        {
            if (portraitCamera) cameraOrigin = portraitCamera.localPosition;
            if (menuCamera)
            {
                menuCamOrigin = menuCamera.position;
                menuCamRotOrigin = menuCamera.rotation;
            }
        }

        void Update()
        {
            float t = Time.unscaledTime;
            int activeRole = HoveredRole >= 0 ? HoveredRole : (CoopSession.Instance ? CoopSession.Instance.LocalRole : RoleLoadout.Selected);

            for (int i = 0; i < operators.Length; i++)
            {
                if (!operators[i]) continue;
                bool active = i == activeRole;
                float targetPitch = active ? -4.5f : 0f;
                float targetZ = active ? -0.14f : 0f;
                Quaternion targetRot = Quaternion.Euler(
                    targetPitch + Mathf.Sin(t * 1.1f + i * 1.3f) * 0.65f,
                    Mathf.Sin(t * 0.45f + i * 1.7f) * (active ? 2.2f : 0.9f),
                    0f
                );
                operators[i].localRotation = Quaternion.Slerp(operators[i].localRotation, targetRot, Time.unscaledDeltaTime * 7f);
                Vector3 pos = operators[i].localPosition;
                pos.z = Mathf.Lerp(pos.z, targetZ, Time.unscaledDeltaTime * 7f);
                operators[i].localPosition = pos;
            }

            if (selectionLight && activeRole >= 0 && activeRole < operators.Length && operators[activeRole])
            {
                Vector3 targetLightPos = new Vector3(operators[activeRole].localPosition.x, 2.75f, -0.85f);
                selectionLight.transform.localPosition = Vector3.Lerp(selectionLight.transform.localPosition, targetLightPos, Time.unscaledDeltaTime * 9f);
            }

            if (holoDisplay)
            {
                holoDisplay.localRotation = Quaternion.Euler(0f, t * 24f, 0f);
            }

            if (portraitCamera)
            {
                portraitCamera.localPosition = cameraOrigin + new Vector3(Mathf.Sin(t * 0.28f) * 0.025f, Mathf.Sin(t * 0.72f) * 0.015f, 0f);
            }

            if (menuCamera)
            {
                menuCamera.position = menuCamOrigin + new Vector3(Mathf.Sin(t * 0.22f) * 0.18f, Mathf.Sin(t * 0.48f) * 0.06f, Mathf.Cos(t * 0.19f) * 0.12f);
                menuCamera.rotation = menuCamRotOrigin * Quaternion.Euler(Mathf.Sin(t * 0.31f) * 0.4f, Mathf.Sin(t * 0.18f) * 0.8f, 0f);
            }
        }
    }
}
