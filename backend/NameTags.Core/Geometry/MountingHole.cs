namespace NameTags.Core.Geometry;

/// <summary>A circular through-hole in the plate, offset from the plate's center (origin).</summary>
public sealed record MountingHole(float OffsetXMm, float OffsetYMm, float DiameterMm);
