#nullable enable
using System;
using System.Globalization;

namespace GCodeGenerator.Trajectory
{
    /// <summary>
    /// A 3D point in program coordinates (plan item 6.2). Pure data type —
    /// no WPF dependencies, so it can live in Core and be used by tests.
    /// </summary>
    public readonly struct Vec3
    {
        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public static Vec3 Zero => new Vec3(0, 0, 0);

        public bool Equals(Vec3 other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object? obj) => obj is Vec3 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        public static bool operator ==(Vec3 left, Vec3 right) => left.Equals(right);

        public static bool operator !=(Vec3 left, Vec3 right) => !left.Equals(right);

        public override string ToString() =>
            $"{X.ToString(CultureInfo.InvariantCulture)};{Y.ToString(CultureInfo.InvariantCulture)};{Z.ToString(CultureInfo.InvariantCulture)}";
    }
}
