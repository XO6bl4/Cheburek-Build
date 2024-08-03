using Robust.Shared.Serialization;

namespace Content.Shared.Radium.Nanites.Events;

[Serializable, NetSerializable]
public sealed class NanitesEvents : EntityEventArgs
{
    public NetEntity Performer { get; }
    public NanitesEvents(NetEntity performer)
    {
        Performer = performer;
    }
}
