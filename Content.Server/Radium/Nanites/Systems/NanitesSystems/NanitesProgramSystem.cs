using Content.Server.Radium.Medical.Surgery.Systems;
using Content.Shared.Radium.Nanites.Systems;
using Content.Shared.Radium.Nanites.Events;
using Robust.Shared.Timing;
using Content.Shared.Alert;
using Content.Shared.Radium.Nanites.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed.Commands.Math;
using Robust.Shared.Utility;
using Content.Shared.Damage;
using SQLitePCL;
using Content.Server.Medical;
using Serilog.Debugging;

namespace Content.Server.Radium.Nanites.Programs;

public abstract class NanitesServerPrograms : SharedNanitesSystem
{
    [Dependency] private SharedNanitesSystem _nanitesSystem = default!;
    [Dependency] private IGameTiming _gameTicker = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private DamageableSystem _damage = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    private void RegenerateHealthByNanites(EntityUid uid, EntityUid user, NanitesComponent component, EventArgs? args)
    {
        var damagePerSecond = 1;
        var damageable = EnsureComp<DamageableComponent>(uid);
        var damage = new DamageSpecifier();
        _nanitesSystem.TryTakeNanites(uid, damagePerSecond, component);
        _damage.SetDamage(uid, damageable, -damagePerSecond); // бля да че использовать то ебаный ты в рот нахуй?
    }


}
