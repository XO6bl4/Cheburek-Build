using System.Diagnostics.CodeAnalysis;
using Content.Shared.Radium.Nanites;
using Content.Shared.Radium.Nanites.Systems;
using Content.Server.GameTicking;

namespace Content.Server.Radium.Nanites.Systems;

public sealed class NanitesSystem : SharedNanitesSystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
}
