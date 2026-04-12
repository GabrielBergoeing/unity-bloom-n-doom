using UnityEngine;
using Mirror;

public class TransportSelector : MonoBehaviour
{
    [Header("Assign both transports on the NetworkManager GameObject")]
    public Transport editorTransport;  // KCP
    public Transport buildTransport;   // FizzySteamworks

    void Awake()
    {
#if UNITY_EDITOR
        Transport.active = editorTransport;
        Debug.Log("Using editor transport (KCP)");
#else
        Transport.active = buildTransport;
        Debug.Log("Using build transport (FizzySteamworks)");
#endif
    }
}
