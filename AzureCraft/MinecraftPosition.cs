namespace AzureCraft
{
    public record MinecraftPosition
    {
        public MinecraftPosition() { }
        public MinecraftPosition(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; init; }
        public double Y { get; init; }
        public double Z { get; init; }

        public bool RelativeX { get; init; }
        public bool RelativeY { get; init; }
        public bool RelativeZ { get; init; }

        public override string ToString()
        {
            var x = RelativeX ? $"~{X}" : X.ToString();
            var y = RelativeY ? $"~{Y}" : Y.ToString();
            var z = RelativeZ ? $"~{Z}" : Z.ToString();
            return $"{x} {y} {z}";
        }

        public static MinecraftPosition Zero { get; } = new MinecraftPosition();

        public MinecraftPosition WithX(double X) => this with { X = X, RelativeX = false };
        public MinecraftPosition WithY(double Y) => this with { Y = Y, RelativeY = false };
        public MinecraftPosition WithZ(double Z) => this with { Z = Z, RelativeZ = false };

        public MinecraftPosition WithRelativeX(double X) => this with { X = X, RelativeX = true };
        public MinecraftPosition WithRelativeY(double Y) => this with { Y = Y, RelativeY = true };
        public MinecraftPosition WithRelativeZ(double Z) => this with { Z = Z, RelativeZ = true };
    }
}