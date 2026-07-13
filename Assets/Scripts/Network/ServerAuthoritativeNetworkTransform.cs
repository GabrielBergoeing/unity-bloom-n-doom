using Mirror;

/// <summary>
/// This game's player movement is strictly server-authoritative - Entity.SetVelocity() blocks
/// every non-server write, so the client never moves its own Rigidbody locally. Stock
/// NetworkTransformReliable.UpdateClient() skips applying incoming server snapshots whenever
/// IsClientWithAuthority (isClient && authority) is true, which is exactly the case for a
/// player's own object - Mirror grants connection ownership to player objects by default so
/// Commands work at all. That silently froze the local client's own character in place (while
/// SyncVars like xFacingDir/yFacingDir kept updating normally through a separate path, since
/// those aren't gated by NetworkTransform's ownership check) - there's no client-side prediction
/// here to protect, so always apply.
/// </summary>
public class ServerAuthoritativeNetworkTransform : NetworkTransformReliable
{
    protected override void UpdateClient()
    {
        if (useFixedUpdate)
        {
            base.UpdateClient();
            return;
        }

        if (clientSnapshots.Count > 0)
        {
            SnapshotInterpolation.StepInterpolation(
                clientSnapshots,
                NetworkTime.time,
                out TransformSnapshot from,
                out TransformSnapshot to,
                out double t);

            TransformSnapshot computed = TransformSnapshot.Interpolate(from, to, t);
            Apply(computed, to);
        }
    }
}
