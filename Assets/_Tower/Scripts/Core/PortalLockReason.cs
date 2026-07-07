namespace Tower.Core
{
    // Why a portal is locked, if at all. None means the door is freely
    // enterable. This is the nullable "lockReason" from the T25 spec, modelled
    // as an explicit enum so Tower.Core stays allocation-free and deterministic.
    public enum PortalLockReason
    {
        // Door is open and enterable.
        None,

        // A key / resource is required before this door opens.
        RequiresKey,

        // The reward here is a boss-gated payoff.
        BossGated,

        // The door leads deeper than the party may currently travel.
        DepthGated
    }
}
