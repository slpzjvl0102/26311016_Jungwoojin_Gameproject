using System.Numerics;

// Rendering/입력 장치와 독립적인 게임 규칙. 좌표는 1200 x 900 기준입니다.
namespace EscapeMine;

public enum GamePhase { Title, Ready, Play, Result }
public enum SoundCue { Swing, StrongSwing, Mode, Break, Bounce, Oxygen, Breath, Win, Lose }
public readonly record struct GameInput(float MouseX, bool Start = false, bool Swing = false,
    bool TogglePower = false, bool Restart = false);

public sealed class GameSession
{
    public GamePhase Phase { get; private set; } = GamePhase.Title;
    public Ball Ball { get; } = new();
    public Pickaxe Pickaxe { get; } = new();
    public List<Rock> Rocks { get; } = new();
    public float Oxygen { get; private set; } = Rules.OxygenMax;
    public float Elapsed { get; private set; }
    public int Score { get; private set; }
    public int Best { get; private set; }
    public bool Won { get; private set; }
    public string Feedback { get; private set; } = "";
    public float FeedbackTime { get; private set; }
    public event Action<SoundCue>? Sound;
    private float accumulator, breathTimer;
    private readonly Random random;

    public GameSession(int? seed = null) => random = seed.HasValue ? new Random(seed.Value) : new Random();

    public void Reset()
    {
        Oxygen = Rules.OxygenMax;
        Score = 0; Elapsed = 0; Won = false;
        accumulator = 0; breathTimer = 0; FeedbackTime = 0;
        Pickaxe.X = Rules.Width / 2; Pickaxe.Strong = false; Pickaxe.Flash = 0;
        Ball.Power = 0; Ball.Velocity = Vector2.Zero;
        Rocks.Clear();
        Rocks.AddRange(RockField.Create(random));
        Phase = GamePhase.Ready;
        AttachBall();
    }

    public void Tick(float delta, GameInput input)
    {
        delta = Math.Clamp(delta, 0, .1f); // Window 이동/중단 후 큰 시간 점프 방지
        Pickaxe.Flash = Math.Max(0, Pickaxe.Flash - delta);
        FeedbackTime = Math.Max(0, FeedbackTime - delta);
        if (Phase == GamePhase.Title) { if (input.Start) Reset(); return; }
        if (Phase == GamePhase.Result)
        {
            if (input.Restart) Phase = GamePhase.Title;
            return;
        }
        Pickaxe.X = Math.Clamp(input.MouseX, 90, Rules.Width - 90);
        if (input.TogglePower) { Pickaxe.Strong = !Pickaxe.Strong; Sound?.Invoke(SoundCue.Mode); }
        if (Phase == GamePhase.Ready) AttachBall();
        if (input.Swing) Swing();
        if (Phase != GamePhase.Play) return;
        accumulator += delta;
        while (accumulator >= Rules.Step && Phase == GamePhase.Play)
        {
            accumulator -= Rules.Step;
            Simulate(Rules.Step);
        }
    }

    private void AttachBall() => Ball.Position = new(Pickaxe.X, Rules.PaddleY - 30);

    private void Swing()
    {
        bool strong = Pickaxe.Strong;
        float halfWidth = Pickaxe.HalfWidth;
        Pickaxe.LastStrong = strong; Pickaxe.Flash = .18f; Pickaxe.Strong = false;
        Oxygen = Math.Max(0, Oxygen - (strong ? Rules.StrongCost : Rules.NormalCost));
        Sound?.Invoke(strong ? SoundCue.StrongSwing : SoundCue.Swing);
        if (Oxygen <= 0) { Finish(false); return; }
        bool hit = Phase == GamePhase.Ready ||
            (Math.Abs(Ball.Position.X - Pickaxe.X) <= halfWidth + Rules.Radius &&
             Math.Abs(Ball.Position.Y - Rules.PaddleY) <= 64);
        if (!hit) { Notify("헛스윙 · 산소가 소모됩니다"); return; }
        float offset = Math.Clamp((Ball.Position.X - Pickaxe.X) / halfWidth, -1, 1);
        float sign = offset == 0 ? (Ball.Velocity.X < 0 ? -1 : 1) : Math.Sign(offset);
        float angle = strong ? offset * .25f : sign * (.35f + Math.Abs(offset) * .55f);
        Ball.Velocity = new(MathF.Sin(angle) * Rules.Speed, -MathF.Cos(angle) * Rules.Speed);
        Ball.Power = strong ? 2 : 1;
        Phase = GamePhase.Play;
        Notify(strong ? "강타! POWER 2" : "타격! POWER 1");
    }

    private void Simulate(float dt)
    {
        Elapsed += dt;
        Oxygen = Math.Max(0, Oxygen - Rules.Drain * dt);
        if (Oxygen <= 0) { Finish(false); return; }
        if (Oxygen <= 20)
        {
            breathTimer += dt;
            if (breathTimer >= 5) { Sound?.Invoke(SoundCue.Breath); breathTimer = 0; }
        }
        else breathTimer = 0;
        Vector2 old = Ball.Position;
        int bounces = BallPhysics.Move(Ball, dt);
        for (int i = 0; i < bounces; i++) Sound?.Invoke(SoundCue.Bounce);
        foreach (Rock rock in Rocks)
        {
            if (rock.Health == 0 && !rock.Tank) continue;
            if (!BallPhysics.Overlaps(Ball, rock)) continue;
            if (rock.Tank)
            {
                rock.Tank = false; Oxygen = Math.Min(Rules.OxygenMax, Oxygen + Rules.TankRecovery); breathTimer = 0;
                Sound?.Invoke(SoundCue.Oxygen); Notify("산소 회복 +20"); continue;
            }
            int damage = Math.Min(Ball.Power, rock.Health);
            rock.Health -= damage; Ball.Power -= damage;
            if (rock.Health == 0)
            {
                Score += rock.Hard ? 40 : 20;
                Sound?.Invoke(SoundCue.Break);
                if (rock.Row == 0) { Finish(true); return; }
            }
            if (Ball.Power > 0) continue;
            BallPhysics.Reflect(Ball, rock, old);
            Sound?.Invoke(SoundCue.Bounce);
            break;
        }
    }

    private void Notify(string text) { Feedback = text; FeedbackTime = 1.2f; }
    private void Finish(bool won)
    {
        if (Phase == GamePhase.Result) return;
        Won = won; Phase = GamePhase.Result;
        if (won) Score += (int)(Oxygen * 20);
        Best = Math.Max(Best, Score);
        Sound?.Invoke(won ? SoundCue.Win : SoundCue.Lose);
    }
}
