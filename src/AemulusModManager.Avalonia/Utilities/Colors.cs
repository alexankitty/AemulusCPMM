using System;
using Avalonia.Media;

namespace AemulusModManager.Avalonia.Utilities;

public static class Colors {
    public static Color Darken(Color color, double amount) {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            color.A,
            (byte)(color.R * (1 - amount)),
            (byte)(color.G * (1 - amount)),
            (byte)(color.B * (1 - amount))
        );
    }
}
