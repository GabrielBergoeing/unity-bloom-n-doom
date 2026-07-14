using Unity.Netcode.Components;

/// <summary>
/// NetworkAnimator driven by the OWNER instead of the server.
/// The player's state machine writes animator parameters locally (owner side),
/// and this component replicates them to all other clients.
/// </summary>
public class OwnerNetworkAnimator : NetworkAnimator
{
    protected override bool OnIsServerAuthoritative() => false;
}
