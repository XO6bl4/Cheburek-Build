using System.Diagnostics.CodeAnalysis;
using Content.Shared.Radium.Nanites;
using Content.Shared.Radium.Nanites.Components;
using Content.Shared.Radium.Nanites.Events;
using Content.Server.GameTicking;
using Content.Shared.Alert;

namespace Content.Server.Radium.Nanites.Systems;

public sealed class NanitesPowerSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;

    private readonly Dictionary<NanitesThreshold, string> _powerDictionary = new();

    public override void Initialize()
    {
        base.Initialize();

        _powerDictionary.Add(NanitesThreshold.Max, Loc.GetString("shadowkin-power-max"));
        _powerDictionary.Add(NanitesThreshold.Great, Loc.GetString("shadowkin-power-great"));
        _powerDictionary.Add(NanitesThreshold.Good, Loc.GetString("shadowkin-power-good"));
        _powerDictionary.Add(NanitesThreshold.Okay, Loc.GetString("shadowkin-power-okay"));
        _powerDictionary.Add(NanitesThreshold.Tired, Loc.GetString("shadowkin-power-tired"));
        _powerDictionary.Add(NanitesThreshold.Min, Loc.GetString("shadowkin-power-min"));
    }

    public string GetLevelName(float powerLevel)
    {
        // Placeholders
        var result = NanitesThreshold.Min;
        var value = NanitesComponent.PowerThresholds[NanitesThreshold.Max];

        // Find the highest threshold that is lower than the current power level
        foreach (var threshold in NanitesComponent.PowerThresholds)
        {
            if (threshold.Value <= value &&
                threshold.Value >= powerLevel)
            {
                result = threshold.Key;
                value = threshold.Value;
            }
        }
        // Return the name of the threshold
        _powerDictionary.TryGetValue(result, out var powerType);
        powerType ??= "norm";
        return powerType;
    }

    [ValidatePrototypeId<AlertPrototype>]
    private const string ShadowkinPower = "ShadowkinPower";

    /// <summary>
    ///    Sets the alert level of a shadowkin.
    /// </summary>
    /// <param name="uid">The entity uid.</param>
    /// <param name="enabled">Enable the alert or not</param>
    /// <param name="powerLevel">The current power level.</param>
    public void UpdateAlert(EntityUid uid, bool enabled, float? powerLevel = null)
    {
        if (!enabled || powerLevel == null)
        {
            _alerts.ClearAlert(uid, ShadowkinPower);
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
        _alerts.ShowAlert(uid, ShadowkinPower, (short) power);
    }


    /// <summary>
    ///     Tries to update the power level of a shadowkin based on an amount of seconds.
    /// </summary>
    /// <param name="uid">The entity uid.</param>
    /// <param name="frameTime">The time since the last update in seconds.</param>
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

    /// <summary>
    ///     Updates the power level of a shadowkin based on an amount of seconds.
    /// </summary>
    /// <param name="uid">The entity uid.</param>
    /// <param name="frameTime">The time since the last update in seconds.</param>
    public void UpdatePowerLevel(EntityUid uid, float frameTime)
    {
        // Get shadowkin component
        if (!TryComp<NanitesComponent>(uid, out var component))
        {
            return;
        }

        // Calculate new power level (P = P + t * G * M)
        var newPowerLevel = component.NanitesLevel + frameTime * component.PowerLevelGain * component.PowerLevelGainMultiplier;

        // Clamp power level using clamp function
        newPowerLevel = Math.Clamp(newPowerLevel, component.PowerLevelMin, component.PowerLevelMax);

        // Set the new power level
        SetPowerLevel(uid, newPowerLevel);
    }


    /// <summary>
    ///     Tries to add to the power level of a shadowkin.
    /// </summary>
    /// <param name="uid">The entity uid.</param>
    /// <param name="amount">The amount to add to the power level.</param>
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
}
