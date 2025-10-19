using UnityEngine;

public class Player_HoldItemBehaviour : MonoBehaviour
{
    [Range(0f, 1f)][SerializeField] private float holdingDistance = 0.8f;
    private Transform onHand;
    private Player player;

    private void Awake()
    {
        player = GetComponent<Player>();
        onHand = GetComponentInChildren<WeaponRotation>().transform;
    }

    private void Update()
    {
        onHand.localPosition = new Vector3(
            player.xFacingDir * holdingDistance,
            player.yFacingDir * holdingDistance,
            0f
        );
    }
}
