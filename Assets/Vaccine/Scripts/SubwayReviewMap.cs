using UnityEngine;

namespace Vaccine
{
    // Keep the existing surface plan; frame the separate lower level when the explorer is there.
    [DefaultExecutionOrder(100)]
    public sealed class SubwayReviewMap : MonoBehaviour
    {
        public VaccineWalkthrough player;
        public GameObject undergroundLabels;
        Vector3 surfacePosition;
        float surfaceSize;
        public bool Underground => player.transform.position.x >= 60 && player.transform.position.y < -1.5f;
        void Awake() { surfacePosition = player.overview.transform.position; surfaceSize = player.overview.orthographicSize; }
        void LateUpdate()
        {
            bool below = Underground;
            player.overview.transform.position = below ? new Vector3(140, 100, -6.5f) : surfacePosition;
            player.overview.orthographicSize = below ? Mathf.Max(43, 84 / player.overview.aspect) : surfaceSize;
            player.mapLabels.SetActive(player.MapVisible && !below);
            undergroundLabels.SetActive(player.MapVisible && below);
            if (player.MapVisible && below) player.area.text = "VACCINE / UNDERGROUND PLAN";
            else if (below) player.area.text = "VACCINE / " + (player.transform.position.x < 88.8f ? "SUBWAY STAIRS" : player.transform.position.z > 17 ? "PLATFORM 1 TRAIN" : player.transform.position.z < -30 ? "DAMAGED TRAIN" : player.transform.position.z > 2 ? "PLATFORM 1" : player.transform.position.z < -16 ? "PLATFORM 2" : "PLATFORM CONNECTION");
        }
    }
}
