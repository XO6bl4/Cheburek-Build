using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.CombatMode;
using Content.Shared.Radium.Nanites.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Database;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Rejuvenate;
using Content.Shared.Rounding;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;


namespace Content.Shared.Radium.Nanites.Systems;

public abstract class SharedNanitesSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly SharedStunSystem _stunSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        //InitializeModifier();

        SubscribeLocalEvent<NanitesComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NanitesComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<NanitesComponent, AfterAutoHandleStateEvent>(OnNanitesHandleState);
    }

    private void OnNanitesHandleState(EntityUid uid, NanitesComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (component.Critical)
            EnterNanitesCooldown(uid, component);
        else
        {
            if (component.NanitesDamage > 0f)
                EnsureComp<ActiveNanitesComponent>(uid);

            ExitNanitesCooldown(uid, component);
        }
    }

    private void OnShutdown(EntityUid uid, NanitesComponent component, ComponentShutdown args)
    {
        if (MetaData(uid).EntityLifeStage < EntityLifeStage.Terminating)
        {
            RemCompDeferred<ActiveNanitesComponent>(uid);
        }
        _alerts.ClearAlert(uid, component.NanitesAlert);
    }

    private void OnStartup(EntityUid uid, NanitesComponent component, ComponentStartup args)
    {
        SetNanitesAlert(uid, component);
    }

    private void SetNanitesAlert(EntityUid uid, NanitesComponent? component = null)
    {
        if (!Resolve(uid, ref component, false) || component.Deleted)
            return;

        var severity = ContentHelpers.RoundToLevels(MathF.Max(0f, component.Nanites - component.CritThreshold), component.CritThreshold, 7);
        _alerts.ShowAlert(uid, component.NanitesAlert, (short) severity);
    }

    public bool TryTakeNanites(EntityUid uid, float value, NanitesComponent? component = null, EntityUid? source = null, EntityUid? with = null)
    {
        // Something that has no Stamina component automatically passes stamina checks
        if (!Resolve(uid, ref component, false))
            return true;

        var oldStam = component.NanitesDamage;

        if (oldStam + value > component.CritThreshold || component.Critical)
            return false;

        TakeNanitesDamage(uid, value, component, source, with, visual: false);
        return true;
    }

    public void TakeNanitesDamage(EntityUid uid, float value, NanitesComponent? component = null,
        EntityUid? source = null, EntityUid? with = null, bool visual = true, SoundSpecifier? sound = null, bool chaosDamage = false)
    {
        if (!Resolve(uid, ref component, false))
            return;

        // Have we already reached the point of max stamina damage?
        if (component.Critical)
            return;


        var oldDamage = component.NanitesDamage;
        component.NanitesDamage = MathF.Max(0f, component.NanitesDamage + value);

        SetNanitesAlert(uid, component);
        Dirty(uid, component);

        if (value <= 0)
            return;

        if (visual)
        {
            _color.RaiseEffect(Color.Aqua, new List<EntityUid>() { uid }, Filter.Pvs(uid, entityManager: EntityManager));
        }

        if (_net.IsServer)
        {
            _audio.PlayPvs(sound, uid);
        }
    }

    public float GetNanitesDamage(EntityUid uid, NanitesComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return 0f;

        var curTime = _timing.CurTime;
        var pauseTime = _metadata.GetPauseTime(uid);
        return MathF.Max(0f, component.NanitesDamage - MathF.Max(0f, (float) (curTime - (component.NextUpdate + pauseTime)).TotalSeconds));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var stamQuery = GetEntityQuery<NanitesComponent>();
        var query = EntityQueryEnumerator<ActiveNanitesComponent>();
        var curTime = _timing.CurTime;

        while (query.MoveNext(out var uid, out _))
        {
            // Just in case we have active but not stamina we'll check and account for it.
            if (!stamQuery.TryGetComponent(uid, out var comp) ||
                comp.NanitesDamage <= 0f && !comp.Critical)
            {
                RemComp<ActiveNanitesComponent>(uid);
                continue;
            }

            // Shouldn't need to consider paused time as we're only iterating non-paused stamina components.
            var nextUpdate = comp.NextUpdate;

            if (nextUpdate > curTime)
                continue;

            comp.NextUpdate += TimeSpan.FromSeconds(1f);
            Dirty(uid, comp);
        }
    }

    private void EnterNanitesCooldown(EntityUid uid, NanitesComponent? component = null)
    {
        if (!Resolve(uid, ref component) ||
            component.Critical)
        {
            return;
        }

        component.Critical = true;
        component.NanitesDamage = component.CritThreshold;

        // Give them buffer before being able to be re-stunned
        component.NextUpdate = _timing.CurTime + component.NanitesCooldown;
        EnsureComp<ActiveNanitesComponent>(uid);
        Dirty(uid, component);
        _adminLogger.Add(LogType.Stamina, LogImpact.Medium, $"{ToPrettyString(uid):user} entered stamina crit");
    }

    private void ExitNanitesCooldown(EntityUid uid, NanitesComponent? component = null)
    {
        if (!Resolve(uid, ref component) ||
            !component.Critical)
        {
            return;
        }

        component.Critical = false;
        component.NanitesDamage = 0f;
        component.NextUpdate = _timing.CurTime;
        SetNanitesAlert(uid, component);
        RemComp<ActiveNanitesComponent>(uid);
        Dirty(uid, component);
        _adminLogger.Add(LogType.Stamina, LogImpact.Low, $"{ToPrettyString(uid):user} recovered from stamina crit");
    }

}
