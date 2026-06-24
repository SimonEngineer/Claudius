namespace NameTags.Core.Pipeline;

/// <summary>Configurable dimensions for the wine-glass charm and clothes-clip pack variants, so a
/// project can tune the hook radius (to fit a particular glass stem) or clip gap (to fit a
/// particular fabric thickness) instead of being stuck with one fixed size for every project.</summary>
public sealed record PackSettings(
    float CharmPlateWidthMm,
    float CharmPlateHeightMm,
    float CharmHookOuterRadiusMm,
    float CharmHookBandThicknessMm,
    float ClipPlateWidthMm,
    float ClipPlateHeightMm,
    float ClipArmLengthMm,
    float ClipGapMm,
    float ClipArmThicknessMm)
{
    public static readonly PackSettings Default = new(
        WineGlassCharmBuilder.PlateWidthMm,
        WineGlassCharmBuilder.PlateHeightMm,
        WineGlassCharmBuilder.HookOuterRadiusMm,
        WineGlassCharmBuilder.HookBandThicknessMm,
        ClothesClipBuilder.PlateWidthMm,
        ClothesClipBuilder.PlateHeightMm,
        ClothesClipBuilder.ClipArmLengthMm,
        ClothesClipBuilder.ClipGapMm,
        ClothesClipBuilder.ClipArmThicknessMm);
}
