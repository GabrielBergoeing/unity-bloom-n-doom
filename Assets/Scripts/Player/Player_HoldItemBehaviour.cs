using Mirror;
using UnityEngine;

public class Player_HoldItemBehaviour : NetworkBehaviour
{
    [Range(0f, 1f)][SerializeField] private float holdingDistance = 0.8f;
    private Transform onHand;
    private Player player;
    private int lastX = int.MinValue;
    private int lastY = int.MinValue;

    private void Awake()
    {
        player = GetComponent<Player>();

        var weapon = GetComponentInChildren<WeaponRotation>();
        if (weapon != null)
            onHand = weapon.transform;
    }
    private void Update()
    {
        if (!isClient || player == null) return;

        if (onHand == null)
        {
            var weapon = GetComponentInChildren<WeaponRotation>();
            if (weapon != null)
                onHand = weapon.transform;
            else
                return;
        }

        if (player.xFacingDir == lastX && player.yFacingDir == lastY)
            return;

        lastX = player.xFacingDir;
        lastY = player.yFacingDir;

        onHand.localPosition = new Vector3(
            lastX * holdingDistance,
            lastY * holdingDistance,
            0f
        );
    }
}
