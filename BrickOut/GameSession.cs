using System.Numerics;

// Rendering/입력 장치와 독립적인 게임 규칙. 좌표는 1200 x 900 기준입니다.
namespace EscapeMine;

public enum GamePhase { Title, Ready, Play, Result }
public enum SoundCue { Swing, StrongSwing, Mode, Break, Bounce, Oxygen, Breath, Win, Lose }
public readonly record struct GameInput(float MouseX, bool Start = false, bool Swing = false,
    bool TogglePower = false, bool Restart = false);

public static class Rules
{
    public const float Width = 1200, Height = 900, PaddleY = 750, Floor = 804;
    public const float Radius = 11, Speed = 510, OxygenMax = 100, Drain = .25f;
    public const float NormalCost = 1, StrongCost = 2.5f, TankRecovery = 20;
    public const int Columns = 14, Rows = 8;
    public const float Tile = 64, Gap = 4, FieldX = 126, FieldY = 60;
    public const float Step = 1f / 240;
}

public sealed class Ball
{
    public Vector2 Position { get; internal set; }
    public Vector2 Velocity { get; internal set; }
    public int Power { get; internal set; }
}

public sealed class Pickaxe
{
    public float X { get; internal set; } = Rules.Width / 2;
    public bool Strong { get; internal set; }
    public float Flash { get; internal set; }
    public bool LastStrong { get; internal set; }
    public float HalfWidth => Strong ? 36 : 88;
}

public sealed class Rock
{
    public int Column { get; init; }
    public int Row { get; init; }
    public bool Hard { get; init; }
    public int Health { get; internal set; }
    public bool Tank { get; internal set; }
    public float X => Rules.FieldX + Column * (Rules.Tile + Rules.Gap);
    public float Y => Rules.FieldY + Row * (Rules.Tile + Rules.Gap);
}

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
        for (int row = 0; row < Rules.Rows; row++)
        for (int col = 0; col < Rules.Columns; col++)
        {
            // 최상단은 반드시 돌. 나머지 행은 산소통 하나씩을 빈 칸에 배치합니다.
            bool tank = row > 0 && col == (row * 5 + 2) % Rules.Columns;
            bool hard = random.NextDouble() < .27;
            Rocks.Add(new Rock { Row = row, Column = col, Hard = hard,
                Health = tank ? 0 : hard ? 2 : 1, Tank = tank });
        }
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
        Ball.Position += Ball.Velocity * dt;
        if (Ball.Position.X < Rules.Radius || Ball.Position.X > Rules.Width - Rules.Radius)
        {
            Ball.Position = new(Math.Clamp(Ball.Position.X, Rules.Radius, Rules.Width - Rules.Radius), Ball.Position.Y);
            Ball.Velocity = new(-Ball.Velocity.X, Ball.Velocity.Y);
            Sound?.Invoke(SoundCue.Bounce);
        }
        if (Ball.Position.Y < Rules.Radius || Ball.Position.Y > Rules.Floor - Rules.Radius)
        {
            Ball.Position = new(Ball.Position.X, Math.Clamp(Ball.Position.Y, Rules.Radius, Rules.Floor - Rules.Radius));
            Ball.Velocity = new(Ball.Velocity.X, -Ball.Velocity.Y);
            Sound?.Invoke(SoundCue.Bounce);
        }
        foreach (Rock rock in Rocks)
        {
            if (rock.Health == 0 && !rock.Tank) continue;
            float nearX = Math.Clamp(Ball.Position.X, rock.X, rock.X + Rules.Tile);
            float nearY = Math.Clamp(Ball.Position.Y, rock.Y, rock.Y + Rules.Tile);
            if (Vector2.DistanceSquared(Ball.Position, new(nearX, nearY)) > Rules.Radius * Rules.Radius) continue;
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
            // 240Hz 고정 step(이동 2.125px) + 진입 면 보정으로 관통/중복 접촉 방지.
            if (old.Y >= rock.Y + Rules.Tile)
            {
                Ball.Position = new(Ball.Position.X, rock.Y + Rules.Tile + Rules.Radius + .1f);
                Ball.Velocity = new(Ball.Velocity.X, Math.Abs(Ball.Velocity.Y));
            }
            else if (old.Y <= rock.Y)
            {
                Ball.Position = new(Ball.Position.X, rock.Y - Rules.Radius - .1f);
                Ball.Velocity = new(Ball.Velocity.X, -Math.Abs(Ball.Velocity.Y));
            }
            else
            {
                bool left = old.X < rock.X + Rules.Tile / 2;
                Ball.Position = new(left ? rock.X - Rules.Radius - .1f : rock.X + Rules.Tile + Rules.Radius + .1f, Ball.Position.Y);
                Ball.Velocity = new(left ? -Math.Abs(Ball.Velocity.X) : Math.Abs(Ball.Velocity.X), Ball.Velocity.Y);
            }
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
