using UnityEngine;

[CreateAssetMenu(fileName = "PhotonOnlineConfig", menuName = "Thereasm/Photon Fusion Config")]
public class PhotonOnlineConfig : ScriptableObject
{
    public string appIdFusion = "REMOVED";
    public string appVersion = "thereasm-online-4";
    [Tooltip("Both players must use the same region. asia is Singapore.")]
    public string region = "asia";
    public string boardScene = "MainBoard";
    [Range(.05f, 1)] public float snapshotInterval = .05f;
}
