namespace AvatarChamber.PhysBoneMotion
{
    public enum MotionPreset
    {
        Bob,
        Sway,
        Surge,
        Circle,
        Jitter,
        Wind,
        Pulse
    }

    public enum MotionAxisMode
    {
        Translation,
        Rotation
    }

    public sealed class MotionSettings
    {
        public MotionPreset Preset;
        public MotionAxisMode Mode;
        public float Amplitude;
        public float Frequency;
        public float Duration;
    }
}
