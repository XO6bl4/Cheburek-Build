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

    [ValidatePrototypeId<AlertPrototype>]
    private const string NanitesAlert = "NanitesAlert";

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

        var severity = ContentHelpers.RoundToLevels(MathF.Max(0f, component.NanitesLevel - component.PowerLevelMax), component.PowerLevelMax, 7);
        _alerts.ShowAlert(uid, component.NanitesAlert, (short) severity);
    }

    public bool TryTakeNanites(EntityUid uid, float takenNanites, NanitesComponent? component = null, EntityUid? source = null, EntityUid? with = null)
    {
        // Something that has no Stamina component automatically passes stamina checks
        if (!Resolve(uid, ref component, false))
            return true;

        if (uid == null || takenNanites == null || takenNanites == 0 || component == null)
            return false;

        var oldLevel = component.NanitesLevel;

        if (oldLevel - takenNanites <= component.PowerLevelMin)
            return false;

        TakeNanites(uid, takenNanites, component, oldLevel);
        return true;
    }

    public void TakeNanites(EntityUid uid, float takenNanites, NanitesComponent? component, float oldLevel)
    {
        var newLevel = oldLevel - takenNanites;
    }

    public bool TryAddPowerLevel(EntityUid uid, float amount)
    {
        // Check if the entity has a shadowkin component
        if (!TryComp<NanitesComponent>(uid, out _))
            return false;

        // Set the new power level
        AddPowerLevel(uid, amount);

        return true;
    }

    /// <summary>
    ///     Adds to the power level of a shadowkin.
    /// </summary>
    /// <param name="uid">The entity uid.</param>
    /// <param name="amount">The amount to add to the power level.</param>
    public void AddPowerLevel(EntityUid uid, float amount)
    {
        // Get shadowkin component
        if (!TryComp<NanitesComponent>(uid, out var component))
        {
            return;
        }

        // Get new power level
        var newPowerLevel = component.NanitesLevel + amount;

        // Clamp power level using clamp function
        newPowerLevel = Math.Clamp(newPowerLevel, component.PowerLevelMin, component.PowerLevelMax);

        // Set the new power level
        SetPowerLevel(uid, newPowerLevel);
    }


    /// <summary>
    ///     Sets the power level of a shadowkin.
    /// </summary>
    /// <param name="uid">The entity uid.</param>
    /// <param name="newPowerLevel">The new power level.</param>
    public void SetPowerLevel(EntityUid uid, float newPowerLevel)
    {
        // Get shadowkin component
        if (!TryComp<NanitesComponent>(uid, out var component))
        {
            return;
        }

        // Clamp power level using clamp function
        newPowerLevel = Math.Clamp(newPowerLevel, component.PowerLevelMin, component.PowerLevelMax);

        // Set the new power level
        component.NanitesLvl = newPowerLevel;
    }

    public void UpdateAlert(EntityUid uid, bool enabled, float? powerLevel = null)
    {
        if (!enabled || powerLevel == null)
        {
            _alerts.ClearAlert(uid, NanitesAlert);
            return;
        }

        // Get shadowkin component
        if (!TryComp<NanitesComponent>(uid, out var component))
        {
            return;
        }

        // 250 / 7 ~= 35
        // Pwr / 35 ~= (0-7)
        // Round to ensure (0-7)
        var power = Math.Clamp(Math.Round(component.NanitesLevel / 35), 0, 7);

        // Set the alert level
        _alerts.ShowAlert(uid, NanitesAlert, (short) power);
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
        component.NanitesDamage = component.PowerLevelMax;

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

    public bool TryUpdatePowerLevel(EntityUid uid, float frameTime)
    {
        // Check if the entity has a shadowkin component
        if (!TryComp<NanitesComponent>(uid, out var component))
            return false;

        // Check if power gain is enabled
        if (!component.PowerLevelGainEnabled)
            return false;

        // Set the new power level
        UpdatePowerLevel(uid, frameTime);

        return true;
    }

    public void UpdatePowerLevel(EntityUid uid, float frameTime)
    {
        // Get shadowkin component
        if (!TryComp<NanitesComponent>(uid, out var component))
        {
            return;
        }

        // Calculate new power level (P = P + t * G * M)
        var newPowerLevel = component.NanitesLvl + frameTime * component.PowerLevelGain * component.PowerLevelGainMultiplier;

        // Clamp power level using clamp function
        newPowerLevel = Math.Clamp(newPowerLevel, component.PowerLevelMin, component.PowerLevelMax);

        // Set the new power level
        SetPowerLevel(uid, newPowerLevel);
    }

}
