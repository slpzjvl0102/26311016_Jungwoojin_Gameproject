using EscapeMine;
using Vortice.Multimedia;
using Vortice.XAudio2;

// 외부 음원 없이 재생 가능한 임시 합성 사운드. Cue별 Voice로 BGM과 효과음을 동시 재생합니다.
sealed class GameAudio : IDisposable
{
    private sealed class Clip : IDisposable
    {
        public readonly AudioBuffer Buffer;
        public readonly IXAudio2SourceVoice Voice;
        public Clip(byte[] pcm)
        {
            Buffer = new AudioBuffer(pcm);
            Voice = G2AudioContext.Instance!.Audio!.CreateSourceVoice(new WaveFormat(22050, 16, 1));
        }
        public void Play(bool loop = false)
        {
            Voice.Stop(); Voice.FlushSourceBuffers();
            Buffer.LoopCount = loop ? (uint)XAudio2.LoopInfinite : 0;
            Voice.SubmitSourceBuffer(Buffer); Voice.Start();
        }
        public void Stop() { Voice.Stop(); Voice.FlushSourceBuffers(); }
        public void Dispose() { Voice.Dispose(); Buffer.Dispose(); }
    }
    private readonly Dictionary<SoundCue, Clip> effects = new();
    private readonly Clip? title, mine;
    private GamePhase? phase;
    private bool muted;
    public bool Available => title != null;
    public bool Muted
    {
        get => muted;
        set
        {
            muted = value; title?.Stop(); mine?.Stop();
            foreach (Clip clip in effects.Values) clip.Stop();
            phase = null;
        }
    }

    public GameAudio()
    {
        if (G2AudioContext.Instance?.Audio == null) return;
        foreach (SoundCue cue in Enum.GetValues<SoundCue>())
        {
            (double hz, double duration, double noise) = cue switch
            {
                SoundCue.Swing => (210, .13, .45), SoundCue.Break => (90, .19, .7),
                SoundCue.Bounce => (440, .06, 0), SoundCue.Oxygen => (880, .35, 0),
                SoundCue.Breath => (65, .6, .65), SoundCue.Win => (660, .8, .05),
                _ => (140, .8, .1)
            };
            effects[cue] = new Clip(Synthesize(duration, t =>
                Math.Sin(2 * Math.PI * hz * t * (cue == SoundCue.Oxygen ? 1 + t : 1)), noise, .14));
        }
        title = new Clip(Synthesize(8, t =>
        {
            double[] notes = { 220, 261.63, 329.63, 293.66, 220, 196, 261.63, 164.81 };
            double note = notes[(int)t % notes.Length];
            return Math.Sin(2 * Math.PI * note * t) * Math.Sin(Math.PI * (t % 1));
        }, 0, .035));
        mine = new Clip(Synthesize(8, t => Math.Sin(2 * Math.PI * 55 * t) * .7 +
            Math.Sin(2 * Math.PI * 82.5 * t) * .3, .1, .025));
    }

    public void Play(SoundCue cue) { if (!muted && effects.TryGetValue(cue, out Clip? clip)) clip.Play(); }
    public void Update(GamePhase next)
    {
        if (phase == next) return;
        title?.Stop(); mine?.Stop(); phase = next;
        if (muted) return;
        if (next is GamePhase.Title or GamePhase.Ready) title?.Play(true);
        else if (next == GamePhase.Play) mine?.Play(true);
    }
    public void Pause() { title?.Stop(); mine?.Stop(); phase = null; }

    private static byte[] Synthesize(double duration, Func<double, double> tone, double noise, double volume)
    {
        int samples = (int)(duration * 22050);
        byte[] data = new byte[samples * 2];
        Random random = new(42);
        for (int i = 0; i < samples; i++)
        {
            double t = i / 22050.0;
            double envelope = Math.Min(1, t / .02) * Math.Min(1, (duration - t) / .08);
            double value = tone(t) * (1 - noise) + (random.NextDouble() * 2 - 1) * noise;
            short sample = (short)(Math.Clamp(value * volume * envelope, -1, 1) * short.MaxValue);
            data[i * 2] = (byte)sample; data[i * 2 + 1] = (byte)(sample >> 8);
        }
        return data;
    }

    public void Dispose()
    {
        foreach (var effect in effects.Values) effect.Dispose();
        title?.Dispose(); mine?.Dispose();
    }
}
